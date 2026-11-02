using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ae.Dns.Protocol;
using Ae.Dns.Protocol.Enums;
using PCL.Core.Utils.Exts;

namespace PCL.Core.IO.Net.Dns;

public sealed class WeightedDnsRacerClient : IDnsClient
{
    // 成功小幅加分、失败大幅扣分：坏上游（被阻断、持续报错）会迅速跌出
    // 候选集，权重自动收敛到当前环境下的可用上游；环境变化（如上游恢复）
    // 后跌到谷底的上游仍可凭随机 tiebreak 重新获得竞速机会
    private const int SuccessReward = 1;
    private const int FailurePenalty = 10;
    private const int WeightFloor = -100;

    private readonly (IDnsClient Client, int Weight)[] _clients;
    private readonly int _concurrentCount;
    private readonly object _lock = new();
    private readonly Random _random = new();

    public WeightedDnsRacerClient(int concurrentCount, params IDnsClient[] clients)
    {
        if (clients is null || clients.Length < concurrentCount)
            throw new ArgumentException("Not enough clients.");

        _concurrentCount = concurrentCount;
        _clients = clients.Select(static c => (Client: c, Weight: 0)).ToArray();
    }

    public async Task<DnsMessage> Query(DnsMessage query, CancellationToken ct = default)
    {
        int[] candidateIndexes;
        lock (_lock)
        {
            // 权重降序取候选集；并列时随机排序，保证初始同权重的上游
            // 都能轮流参与竞速，避免列表末尾的上游被饿死
            candidateIndexes = _clients
                .Select(static (_, i) => i)
                .OrderByDescending(i => _clients[i].Weight)
                .ThenBy(_ => _random.Next())
                .Take(_concurrentCount)
                .ToArray();
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tasks = new Task<DnsMessage>[candidateIndexes.Length];

        for (var i = 0; i < candidateIndexes.Length; i++)
        {
            var index = candidateIndexes[i];
            var task = _QueryScoredAsync(index, _clients[index].Client, query, cts.Token);
            task.Forget();
            tasks[i] = task;
        }

        var winnerTask = await tasks.WhenAnySuccessAsync().ConfigureAwait(false);
        if (winnerTask is null) throw new InvalidOperationException("All queries failed.");

        cts.Cancel();
        return await winnerTask.ConfigureAwait(false);
    }

    private void _Record(int index, int delta)
    {
        lock (_lock)
        {
            _clients[index] = (_clients[index].Client,
                Math.Clamp(_clients[index].Weight + delta, WeightFloor, int.MaxValue));
        }
    }

    // ServFail/Refused 等错误状态不算"成功"——应继续竞速等待其他可能给出
    // 正确应答的上游；NoError（含空应答）与 NXDomain 是明确的否定/肯定结果，
    // 视为有效应答立即采用，避免普通域名的 SRV/否定查询被拖到整体超时
    private async Task<DnsMessage> _QueryScoredAsync(int index, IDnsClient client, DnsMessage query, CancellationToken ct)
    {
        try
        {
            var response = await client.Query(query, ct).ConfigureAwait(false);
            if (response.Header?.ResponseCode is not (DnsResponseCode.NoError or DnsResponseCode.NXDomain)
                || response.Answers is null)
                throw new InvalidOperationException(
                    $"Unusable DNS response (code: {response.Header?.ResponseCode}).");
            _Record(index, +SuccessReward);
            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // 真实失败（响应错误/异常）扣分；因赢家已产生或调用方取消
            // 而被取消的不扣分——那不代表该上游不可用
            _Record(index, -FailurePenalty);
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var (client, _) in _clients)
            client?.Dispose();
    }
}

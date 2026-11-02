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
    // 成功小幅加分、失败大幅扣分，坏上游会自动跌出候选集
    private const int SuccessReward = 1;
    private const int FailurePenalty = 10;
    private const int WeightFloor = -100;

    private readonly (IDnsClient Client, int Weight)[] _clients;
    private readonly int _concurrentCount;
    private readonly TimeSpan _queryTimeout;
    private readonly object _lock = new();
    private readonly Random _random = new();

    public WeightedDnsRacerClient(int concurrentCount, TimeSpan queryTimeout, params IDnsClient[] clients)
    {
        if (clients is null || clients.Length < concurrentCount)
            throw new ArgumentException("Not enough clients.");

        _concurrentCount = concurrentCount;
        _queryTimeout = queryTimeout;
        _clients = clients.Select(static c => (Client: c, Weight: 0)).ToArray();
    }

    public async Task<DnsMessage> Query(DnsMessage query, CancellationToken ct = default)
    {
        int[] candidateIndexes;
        lock (_lock)
        {
            // 并列权重随机排序，避免同权重的上游被饿死
            candidateIndexes = _clients
                .Select(static (_, i) => i)
                .OrderByDescending(i => _clients[i].Weight)
                .ThenBy(_ => _random.Next())
                .Take(_concurrentCount)
                .ToArray();
        }

        using var timeoutCts = new CancellationTokenSource(_queryTimeout);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var tasks = new Task<DnsMessage>[candidateIndexes.Length];

        for (var i = 0; i < candidateIndexes.Length; i++)
        {
            var index = candidateIndexes[i];
            var task = _QueryScoredAsync(index, _clients[index].Client, query, timeoutCts, cts.Token);
            task.Forget();
            tasks[i] = task;
        }

        Task<DnsMessage> winnerTask;
        try
        {
            winnerTask = await tasks.WhenAnySuccessAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 内建超时：悬挂候选已在 _QueryScoredAsync 内扣分
            throw;
        }
        catch (Exception ex) when (ct.IsCancellationRequested)
        {
            // 传播调用方取消（含被 AggregateException 包装的情况）
            throw new OperationCanceledException("DNS query canceled by caller.", ex, ct);
        }
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
    private async Task<DnsMessage> _QueryScoredAsync(
        int index, IDnsClient client, DnsMessage query, CancellationTokenSource timeoutCts, CancellationToken linkedCt)
    {
        try
        {
            var response = await client.Query(query, linkedCt).ConfigureAwait(false);
            if (response.Header?.ResponseCode is not (DnsResponseCode.NoError or DnsResponseCode.NXDomain)
                || response.Answers is null)
                throw new InvalidOperationException(
                    $"Unusable DNS response (code: {response.Header?.ResponseCode}).");
            _Record(index, +SuccessReward);
            return response;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            // 内建超时取消：悬挂候选扣分（调用方主动取消时 timeoutCts
            // 未到期，不走此分支）
            _Record(index, -FailurePenalty);
            throw;
        }
        catch (Exception)
        {
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

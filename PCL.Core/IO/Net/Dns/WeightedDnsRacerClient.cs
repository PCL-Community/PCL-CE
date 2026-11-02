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
    private readonly (IDnsClient Client, byte Weight)[] _clients;
    private readonly int _concurrentCount;
    private bool _updateWeigh = true;
    private readonly object _lock = new();

    public WeightedDnsRacerClient(int concurrentCount, params IDnsClient[] clients)
    {
        if (clients is null || clients.Length < concurrentCount)
            throw new ArgumentException("Not enough clients.");

        _concurrentCount = concurrentCount;
        _clients = clients.Select(static c => (Client: c, Weight: (byte)0)).ToArray();
    }

    public async Task<DnsMessage> Query(DnsMessage query, CancellationToken ct = default)
    {
        (IDnsClient Client, byte Weight)[] candidates;
        lock (_lock)
        {
            candidates = _clients
                .OrderByDescending(static x => x.Weight)
                .Take(_concurrentCount)
                .ToArray();
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tasks = new Task<DnsMessage>[candidates.Length];

        for (var i = 0; i < candidates.Length; i++)
        {
            var task = _QueryValidatedAsync(candidates[i].Client, query, cts.Token);
            task.Forget();
            tasks[i] = task;
        }

        var winnerTask = await tasks.WhenAnySuccessAsync().ConfigureAwait(false);
        if (winnerTask is null) throw new InvalidOperationException("All queries failed.");

        var winner = candidates[Array.IndexOf(tasks, winnerTask)].Client;

        if (_updateWeigh)
        {
            lock (_lock)
            {
                for (var i = 0; i < _clients.Length; i++)
                {
                    if (_clients[i].Client == winner)
                    {
                        if (_clients[i].Weight < 255)
                            _clients[i] = (_clients[i].Client, (byte)(_clients[i].Weight + 1));
                        else
                            _updateWeigh = false;
                        break;
                    }
                }
            }
        }

        cts.Cancel();
        return await winnerTask.ConfigureAwait(false);
    }

    // ServFail/Refused 等错误状态不算"成功"——应继续竞速等待其他可能给出
    // 正确应答的上游；NoError（含空应答）与 NXDomain 是明确的否定/肯定结果，
    // 视为有效应答立即采用，避免普通域名的 SRV/否定查询被拖到整体超时
    private static async Task<DnsMessage> _QueryValidatedAsync(IDnsClient client, DnsMessage query, CancellationToken ct)
    {
        var response = await client.Query(query, ct).ConfigureAwait(false);
        if (response.Header?.ResponseCode is not (DnsResponseCode.NoError or DnsResponseCode.NXDomain)
            || response.Answers is null)
            throw new InvalidOperationException(
                $"Unusable DNS response (code: {response.Header?.ResponseCode}).");
        return response;
    }

    public void Dispose()
    {
        foreach (var (client, _) in _clients)
            client?.Dispose();
    }
}

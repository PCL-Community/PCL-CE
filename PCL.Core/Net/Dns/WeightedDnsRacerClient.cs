using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ae.Dns.Protocol;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Net.Dns;

public sealed class WeightedDnsRacerClient : IDnsClient
{
    private readonly (IDnsClient Client, byte Weight)[] _clients;
    private readonly int _concurrentCount;
    private bool _updateWeigh = true;
    private readonly object _lock = new();

    public WeightedDnsRacerClient(int concurrentCount, params IDnsClient[] clients)
    {
        if (clients == null || clients.Length < concurrentCount)
            throw new ArgumentException("Not enough clients.");

        _concurrentCount = concurrentCount;
        _clients = clients.Select(c => (Client: c, Weight: (byte)0)).ToArray();
    }

    public async Task<DnsMessage> Query(DnsMessage query, CancellationToken ct = default)
    {
        (IDnsClient Client, byte Weight)[] candidates;
        lock (_lock)
        {
            candidates = _clients
                .OrderByDescending(x => x.Weight)
                .Take(_concurrentCount)
                .ToArray();
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tasks = new Task<DnsMessage>[candidates.Length];

        for (var i = 0; i < candidates.Length; i++)
        {
            var task = candidates[i].Client.Query(query, cts.Token);
            _ = task.ContinueWith(static _ => { }, TaskContinuationOptions.OnlyOnFaulted);
            tasks[i] = task;
        }

        var winnerTask = await tasks.WhenAnySuccess().ConfigureAwait(false);
        if (winnerTask == null) throw new InvalidOperationException("All queries failed.");

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

    public void Dispose()
    {
        foreach (var (client, _) in _clients)
            client?.Dispose();
    }
}

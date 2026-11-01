using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net.Http;
using PCL.Core.Utils.Hash;

namespace PCL.Core.App.Essentials;

internal static class BuildProvenance
{
    // 注意：把这个常量改成 false 就可以关掉检测，但是我不建议任何人关掉它
    // 为啥呢？因为这个检测不会因为你们 patch download 就自爆，
    // 只是检测到程序不是官方构建版之后关掉 Sentry 回报，以此减少我们的统计压力
    // 如果你正在制作 PCL-CE 的 fork 而不是进行二进制修补，你可以放心关闭、忽略甚至移除此功能，
    // 因为通过代码构建的 PCL-CE 或其 fork 一般不会带有我们的 Sentry 汇报端点，此功能就无用了
    public const bool Enabled = true;

    // 如果你在做 PCL-CE fork 并且有自己的 Sentry，记得改掉这里的地址！
    private const string ReleaseApiUrl = "https://api.github.com/repos/PCL-Community/PCL-CE/releases/tags/v";
    private const string DigestPrefix = "sha256:";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);

    public static async Task<bool> IsTrustedAsync()
    {
        if (!Enabled) return true;
        if (Basics.Metadata.Version.BranchCode != 0) return true;
        try
        {
            var body = await _FetchReleaseAsync(Basics.VersionName).ConfigureAwait(false);
            var digests = _ParseDigests(body);
            if (digests.Length == 0) return true;
            var selfHash = await _ComputeSelfHashAsync().ConfigureAwait(false);
            return Array.Exists(digests, digest => string.Equals(digest, selfHash, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static async Task<string> _FetchReleaseAsync(string version)
    {
        using var source = new CancellationTokenSource(RequestTimeout);
        using var response = await HttpRequest.Create(ReleaseApiUrl + Uri.EscapeDataString(version))
            .SendAsync(retryTimes: 1, cancellationToken: source.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.AsStringAsync(source.Token).ConfigureAwait(false);
    }

    private static async Task<string> _ComputeSelfHashAsync()
    {
        await using var stream = new FileStream(
            Basics.ExecutablePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var hash = await SHA256Provider.Instance.ComputeHashAsync(stream).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static string[] _ParseDigests(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("assets", out var assets)) return Array.Empty<string>();
            if (assets.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
            var digests = new List<string>();
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.ValueKind != JsonValueKind.Object) continue;
                if (!asset.TryGetProperty("digest", out var digest)) continue;
                if (digest.ValueKind != JsonValueKind.String) continue;
                var value = digest.GetString();
                if (value is null) continue;
                if (!value.StartsWith(DigestPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                digests.Add(value[DigestPrefix.Length..]);
            }
            return digests.ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}

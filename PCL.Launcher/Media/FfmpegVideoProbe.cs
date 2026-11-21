using System.Text.RegularExpressions;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Media;

/// <summary>视频的显示尺寸。</summary>
public readonly record struct VideoSize(int Width, int Height);

/// <summary>通过 FFmpeg 探测视频流的原始显示尺寸。</summary>
public sealed partial class FfmpegVideoProbe(IExternalCommandRunner runner)
{
    private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(5);

    private readonly IExternalCommandRunner _runner =
        runner ?? throw new ArgumentNullException(nameof(runner));

    /// <summary>探测视频的第一个视频流尺寸。</summary>
    public async Task<VideoSize?> ProbeAsync(
        string executablePath,
        string videoPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _runner
                .RunAsync(
                    executablePath,
                    [
                        "-hide_banner",
                        "-nostdin",
                        "-protocol_whitelist", "file,pipe",
                        "-i", videoPath
                    ],
                    _probeTimeout,
                    cancellationToken)
                .ConfigureAwait(false);

            return Parse(result.StandardError) ?? Parse(result.StandardOutput);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Media", $"探测视频尺寸失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>从 FFmpeg 的流信息输出中解析视频尺寸。</summary>
    public static VideoSize? Parse(string output)
    {
        var match = _VideoSizePattern().Match(output);
        if (!match.Success) return null;
        if (!int.TryParse(match.Groups[1].Value, out var width)
            || !int.TryParse(match.Groups[2].Value, out var height)
            || width <= 0
            || height <= 0)
            return null;

        return new VideoSize(width, height);
    }

    [GeneratedRegex(
        @"Video:[^\r\n]*?(\d{2,5})x(\d{2,5})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _VideoSizePattern();
}
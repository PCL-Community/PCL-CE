using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.Media;

/// <summary>视频的显示尺寸。</summary>
public readonly record struct VideoSize(int Width, int Height);

/// <summary>通过 FFmpeg 探测视频流的原始显示尺寸。</summary>
public sealed partial class FfmpegVideoProbe(IExternalCommandRunner runner)
{
    private static readonly TimeSpan _ProbeTimeout = TimeSpan.FromSeconds(5);

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
                    _ProbeTimeout,
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

        var displayWidth = width;
        var displayHeight = height;

        var hasSar = _TryParseRatio(_SarPattern(), output, out var numerator, out var denominator);
        var hasDar = !hasSar
            && _TryParseRatio(_DarPattern(), output, out numerator, out denominator);
        if (hasSar || hasDar)
        {
            var displayAspect = numerator / (double)denominator;
            displayWidth = hasSar
                ? (int)Math.Round(width * displayAspect)
                : (int)Math.Round(height * displayAspect);
            displayWidth = Math.Max(1, displayWidth);
        }

        if (_TryParseRotation(output, out var rotation) && _IsQuarterTurn(rotation))
            (displayWidth, displayHeight) = (displayHeight, displayWidth);

        return new VideoSize(displayWidth, displayHeight);
    }

    private static bool _TryParseRatio(
        Regex pattern,
        string output,
        out int numerator,
        out int denominator)
    {
        var match = pattern.Match(output);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, out numerator)
            || !int.TryParse(match.Groups[2].Value, out denominator)
            || numerator <= 0
            || denominator <= 0)
        {
            numerator = 0;
            denominator = 0;
            return false;
        }

        return true;
    }

    private static bool _TryParseRotation(string output, out double rotation)
    {
        var match = _DisplayMatrixRotationPattern().Match(output);
        if (!match.Success) match = _RotateMetadataPattern().Match(output);
        if (!match.Success
            || !double.TryParse(
                match.Groups[1].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out rotation))
        {
            rotation = 0d;
            return false;
        }

        return true;
    }

    private static bool _IsQuarterTurn(double rotation)
    {
        var normalized = rotation % 360d;
        if (normalized < 0d) normalized += 360d;
        return Math.Abs(normalized - 90d) < 0.5d
            || Math.Abs(normalized - 270d) < 0.5d;
    }

    [GeneratedRegex(
        @"Video:[^\r\n]*?(\d{2,5})x(\d{2,5})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _VideoSizePattern();

    [GeneratedRegex(
        @"\bSAR\s+(\d+):(\d+)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _SarPattern();

    [GeneratedRegex(
        @"\bDAR\s+(\d+):(\d+)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _DarPattern();

    [GeneratedRegex(
        @"Display Matrix:\s*rotation of\s*(-?\d+(?:\.\d+)?)\s*degrees",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _DisplayMatrixRotationPattern();

    [GeneratedRegex(
        @"^\s*rotate\s*:\s*(-?\d+(?:\.\d+)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex _RotateMetadataPattern();
}
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

/// <summary>视频流的尺寸、帧率和色彩信息。</summary>
public sealed record VideoInfo(
    VideoSize Size,
    double? FrameRate,
    string? PixelFormat,
    string? ColorRange,
    string? ColorSpace);

/// <summary>通过 FFmpeg 探测视频流的显示信息。</summary>
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
        var info = await ProbeInfoAsync(executablePath, videoPath, cancellationToken)
            .ConfigureAwait(false);
        return info?.Size;
    }

    /// <summary>探测视频的第一个视频流信息。</summary>
    public async Task<VideoInfo?> ProbeInfoAsync(
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

            return ParseInfo(result.StandardError) ?? ParseInfo(result.StandardOutput);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Media", $"探测视频信息失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>从 FFmpeg 的流信息输出中解析视频尺寸。</summary>
    public static VideoSize? Parse(string output)
    {
        return ParseInfo(output)?.Size;
    }

    /// <summary>从 FFmpeg 的流信息输出中解析视频信息。</summary>
    public static VideoInfo? ParseInfo(string output)
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

        var frameRate = _ParseFrameRate(output);
        var color = _ParseColorInfo(output);
        return new VideoInfo(
            new VideoSize(displayWidth, displayHeight),
            frameRate,
            color.PixelFormat,
            color.ColorRange,
            color.ColorSpace);
    }

    private static double? _ParseFrameRate(string output)
    {
        var match = _FrameRatePattern().Match(output);
        return !match.Success
            || !double.TryParse(
                match.Groups[1].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var frameRate)
            || frameRate <= 0d
                ? null
                : frameRate;
    }

    private static (string? PixelFormat, string? ColorRange, string? ColorSpace)
        _ParseColorInfo(string output)
    {
        var match = _ColorInfoPattern().Match(output);
        if (!match.Success)
            return (null, null, null);

        var pixelFormat = match.Groups[1].Value.ToLowerInvariant();
        var metadata = match.Groups[2].Value.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(metadata))
            return (pixelFormat, null, null);

        string? colorRange = null;
        string? colorSpace = null;
        foreach (
            var part in metadata.Split(
                [',', ' ', '/'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            switch (part)
            {
                case "tv" or "pc":
                    colorRange = part;
                    break;
                case "bt2020nc":
                    colorSpace = "bt2020";
                    break;
                case "bt709" or "bt601" or "bt2020" or "smpte170m" or "bt470bg":
                    colorSpace = part;
                    break;
            }

        return (pixelFormat, colorRange, colorSpace);
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
        @"(\d+(?:\.\d+)?)\s+fps\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _FrameRatePattern();

    [GeneratedRegex(
        @"Video:[^\r\n]*?,\s*([A-Za-z0-9_]+)(?:\(([^)]*)\))?,\s*\d{2,5}x\d{2,5}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _ColorInfoPattern();

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
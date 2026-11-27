using System.Text;
using System.Text.RegularExpressions;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Media;
using PCL.Core.Platform;

namespace PCL.Core.UI.Media;

/// <summary>按配置路径、应用目录、PATH 和常见安装位置查找 FFmpeg。</summary>
public sealed partial class FfmpegLocator(IExternalCommandRunner? runner)
{
    private readonly Lock _cacheLock = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    private FfmpegProbeResult? _cached;
    private string? _cachedInput;

    public FfmpegLocator() : this(ApplicationRuntime.GetService<IExternalCommandRunner>())
    {
    }

    internal IExternalCommandRunner? CommandRunner => runner;

    /// <summary>检测 FFmpeg。</summary>
    /// <param name="configuredPath">用户配置的路径；留空时自动查找。</param>
    /// <param name="force">是否忽略已成功缓存的检测结果。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<FfmpegProbeResult> DetectAsync(
        string? configuredPath,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var input = _Normalize(configuredPath);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_cacheLock)
            {
                if (!force
                    && _cached is { IsAvailable: true }
                    && _cachedInput == input
                    && _Exists(_cached.ExecutablePath))
                    return _cached;
            }

            var result = await _DetectCoreAsync(input, cancellationToken).ConfigureAwait(false);
            lock (_cacheLock)
            {
                _cachedInput = input;
                _cached = result;
            }

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>清除已缓存的检测结果。</summary>
    public void Invalidate()
    {
        lock (_cacheLock)
        {
            _cachedInput = null;
            _cached = null;
        }
    }

    private async Task<FfmpegProbeResult> _DetectCoreAsync(
        string configuredPath,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var candidate = _ResolveConfigured(configuredPath);
            var probe = await _ProbeAsync(candidate, cancellationToken).ConfigureAwait(false);
            return probe.IsAvailable
                ? probe
                : probe with
                {
                    Availability = FfmpegAvailability.Invalid,
                    Error = probe.Error ?? configuredPath
                };
        }

        var candidates = new List<string>();
        var local = Path.Combine(
            Paths.Data,
            "ffmpeg",
            OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        if (File.Exists(local)) candidates.Add(local);

        candidates.Add(OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
                candidates.Add(Path.Combine(programFiles, "ffmpeg", "bin", "ffmpeg.exe"));

            var localAppData =
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
                candidates.Add(
                    Path.Combine(
                        localAppData,
                        "Microsoft",
                        "WinGet",
                        "Links",
                        "ffmpeg.exe"));
        }
        else if (OperatingSystem.IsMacOS())
        {
            candidates.AddRange(
            [
                "/opt/homebrew/bin/ffmpeg",
                "/usr/local/bin/ffmpeg",
                "/opt/local/bin/ffmpeg"
            ]);
        }
        else if (OperatingSystem.IsLinux())
        {
            candidates.AddRange(
            [
                "/usr/bin/ffmpeg",
                "/usr/local/bin/ffmpeg",
                "/snap/bin/ffmpeg"
            ]);

            var userProfile =
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
                candidates.Add(
                    Path.Combine(userProfile, ".local", "bin", "ffmpeg"));
        }

        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        string? lastError = null;
        foreach (var candidate in candidates.Distinct(comparer))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var probe = await _ProbeAsync(candidate, cancellationToken).ConfigureAwait(false);
            if (probe.IsAvailable) return probe;
            lastError = probe.Error ?? lastError;
        }

        return new FfmpegProbeResult(FfmpegAvailability.NotFound, Error: lastError);
    }

    private async Task<FfmpegProbeResult> _ProbeAsync(
        string executablePath,
        CancellationToken cancellationToken)
    {
        if (runner is null)
            return new FfmpegProbeResult(
                FfmpegAvailability.NotFound,
                Error: Lang.Text("Setup.Ui.Background.Ffmpeg.ServiceUnavailable"));

        if (!_Exists(executablePath))
            return new FfmpegProbeResult(
                FfmpegAvailability.NotFound,
                executablePath,
                Error: executablePath);

        try
        {
            var result = await runner
                .RunAsync(
                    executablePath,
                    ["-hide_banner", "-version"],
                    TimeSpan.FromSeconds(5),
                    cancellationToken)
                .ConfigureAwait(false);
            var output = string.IsNullOrWhiteSpace(result.StandardOutput)
                ? result.StandardError
                : result.StandardOutput;
            if (!result.Succeeded)
                return new FfmpegProbeResult(
                    FfmpegAvailability.NotFound,
                    executablePath,
                    Error: _TrimError(output));

            var match = _VersionPattern().Match(output);
            if (!match.Success)
                return new FfmpegProbeResult(
                    FfmpegAvailability.NotFound,
                    executablePath,
                    Error: _TrimError(output));

            return new FfmpegProbeResult(
                FfmpegAvailability.Available,
                executablePath,
                match.Groups[1].Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new FfmpegProbeResult(
                FfmpegAvailability.NotFound,
                executablePath,
                Error: _TrimError(ex.Message));
        }
    }

    private bool _Exists(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return false;
        if (File.Exists(executablePath)) return true;
        if (Path.IsPathRooted(executablePath)
            || executablePath.Contains(Path.DirectorySeparatorChar)
            || executablePath.Contains(Path.AltDirectorySeparatorChar))
            return false;
        return runner?.ExistsOnPath(executablePath) == true;
    }

    private static string _ResolveConfigured(string configuredPath)
    {
        if (Directory.Exists(configuredPath))
            return Path.Combine(
                configuredPath,
                OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        return configuredPath;
    }

    private static string _Normalize(string? configuredPath)
    {
        var path = (configuredPath ?? string.Empty).Trim().Trim('"');
        if (path.Length == 0) return string.Empty;
        if (path.StartsWith('~')) path = Paths.ExpandUserPath(path);
        return _ExpandEnvironmentVariables(path).Trim();
    }

    private static string _ExpandEnvironmentVariables(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path);
        if (OperatingSystem.IsWindows()) return expanded;

        var builder = new StringBuilder(expanded.Length);
        for (var i = 0; i < expanded.Length; i++)
        {
            if (expanded[i] != '$' || i + 1 >= expanded.Length)
            {
                builder.Append(expanded[i]);
                continue;
            }

            if (expanded[i + 1] == '{')
            {
                var end = expanded.IndexOf('}', i + 2);
                if (end < 0)
                {
                    builder.Append(expanded[i]);
                    continue;
                }

                var name = expanded[(i + 2)..end];
                var value = Environment.GetEnvironmentVariable(name);
                if (value is null)
                    builder.Append(expanded, i, end - i + 1);
                else
                    builder.Append(value);

                i = end;
                continue;
            }

            var nameStart = i + 1;
            var nameEnd = nameStart;
            while (nameEnd < expanded.Length
                   && (char.IsLetterOrDigit(expanded[nameEnd])
                       || expanded[nameEnd] == '_'))
                nameEnd++;
            if (nameEnd == nameStart)
            {
                builder.Append(expanded[i]);
                continue;
            }

            var variable = Environment.GetEnvironmentVariable(
                expanded[nameStart..nameEnd]);
            if (variable is null)
                builder.Append(expanded, i, nameEnd - i);
            else
                builder.Append(variable);

            i = nameEnd - 1;
        }

        return builder.ToString();
    }

    private static string _TrimError(string text)
    {
        var value = text.Trim();
        return value.Length <= 500
            ? value
            : value[..500];
    }

    [GeneratedRegex(
        @"ffmpeg version\s+(\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex _VersionPattern();
}
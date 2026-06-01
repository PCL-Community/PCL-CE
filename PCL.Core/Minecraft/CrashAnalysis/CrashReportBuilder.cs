using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PCL.Core.App.Localization;

namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     <p>构建错误报告压缩包内容。</p>
///     <p>
///         该类只返回内存中的 <see cref="CrashReportPackage" />，不选择保存路径、不写 zip、
///         不打开资源管理器，也不弹出提示。UI 层负责把 Entries 写入真正的 zip 文件。
///     </p>
/// </summary>
public sealed class CrashReportBuilder
{
    /// <summary>
    ///     根据分析报告生成导出文件条目，并对敏感信息进行脱敏。
    /// </summary>
    public static CrashReportPackage Build(CrashAnalysisReport report, CrashReportBuildOptions options)
    {
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var entries =
        (
            from sourceFile in report.Logs.ReportSourceFiles
            where !string.IsNullOrWhiteSpace(sourceFile.Content)
            let fileName = _GetReportFileName(sourceFile, usedNames)
            let content = _Sanitize(sourceFile.Content, options)
            select new CrashReportEntry { FileName = fileName, Content = CrashTextUtils.Utf8Bytes(content) }
        ).ToList();

        if (!string.IsNullOrWhiteSpace(report.Request.LatestLaunchScript))
            entries.Add(new CrashReportEntry
            {
                FileName = _MakeUnique(Lang.Text("Crash.Export.File.LaunchScript"), usedNames),
                Content = CrashTextUtils.Utf8Bytes(_Sanitize(report.Request.LatestLaunchScript,
                    options with { AccessTokenMask = 'F' }))
            });

        if (report.Request.EnvironmentInfo is { } environmentInfo)
            entries.Add(new CrashReportEntry
            {
                FileName = _MakeUnique(Lang.Text("Crash.Export.File.Environment"), usedNames),
                Content = CrashTextUtils.Utf8Bytes(_BuildEnvironmentReport(environmentInfo))
            });

        return new CrashReportPackage(entries);
    }

    private static string _GetReportFileName(CrashLogFile sourceFile, HashSet<string> usedNames)
    {
        var fileName = sourceFile.Kind switch
        {
            CrashLogKind.CapturedGameOutput => Lang.Text("Crash.Export.File.RawOutput"),
            CrashLogKind.LauncherLog => Lang.Text("Crash.Export.File.LauncherLog"),
            _ => Path.GetFileName(sourceFile.DisplayName)
        };

        if (string.IsNullOrWhiteSpace(fileName)) fileName = "log.txt";
        return _MakeUnique(fileName, usedNames);
    }

    private static string _MakeUnique(string fileName, HashSet<string> usedNames)
    {
        if (usedNames.Add(fileName)) return fileName;

        var extension = Path.GetExtension(fileName);
        var name = Path.GetFileNameWithoutExtension(fileName);
        for (var index = 2;; index++)
        {
            var candidate = $"{name}-{index}{extension}";
            if (usedNames.Add(candidate)) return candidate;
        }
    }

    /// <summary>
    ///     对导出日志中的用户名和 access token 进行脱敏。
    /// </summary>
    private static string _Sanitize(string content, CrashReportBuildOptions options)
    {
        var result = options.AccessTokens
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Aggregate(content, (current, token) =>
                current.Replace(
                    token,
                    new string(options.AccessTokenMask, Math.Min(token.Length, 16)),
                    StringComparison.Ordinal
                )
            );

        return options.UserNames
            .Where(static userName => !string.IsNullOrWhiteSpace(userName))
            .Aggregate(result, (current, userName) =>
                current.Replace(
                    userName,
                    new string('*', Math.Min(userName.Length, 16)),
                    StringComparison.OrdinalIgnoreCase
                )
            );
    }

    private static string _BuildEnvironmentReport(CrashEnvironmentInfo info)
    {
        var builder = new StringBuilder();
        _Append(builder, "Crash.Environment.LauncherVersion", info.LauncherVersion);
        _Append(builder, "Crash.Environment.LauncherId", info.LauncherId);
        builder.AppendLine();
        builder.AppendLine(Lang.Text("Crash.Environment.ProfileTitle"));
        _Append(builder, "Crash.Environment.ProfileName", info.AccountName);
        _Append(builder, "Crash.Environment.AuthType", info.AuthType);
        builder.AppendLine();
        builder.AppendLine(Lang.Text("Crash.Environment.InstanceTitle"));
        _Append(builder, "Crash.Environment.Java", info.JavaInfo);
        _Append(builder, "Crash.Environment.Log4jNoLookups", info.Log4JNoLookups?.ToString());
        _Append(builder, "Crash.Environment.MinecraftFolder", info.MinecraftFolder);
        _Append(builder, "Crash.Environment.AllocatedMemory", info.AllocatedMemory);
        builder.AppendLine();
        builder.AppendLine(Lang.Text("Crash.Environment.SystemTitle"));
        _Append(builder, "Crash.Environment.OperatingSystem", info.OperatingSystem);
        _Append(builder, "Crash.Environment.SystemArchitecture",
            $"64-bit: {!(info.Is32BitSystem ?? false)}, ARM64: {info.IsArm64System}");
        _Append(builder, "Crash.Environment.Cpu", info.CpuName);
        _Append(builder, "Crash.Environment.Memory", info.SystemMemoryMb?.ToString());

        for (var i = 0; i < info.Gpus.Count; i++)
        {
            var gpu = info.Gpus[i];
            builder.AppendLine(Lang.Text("Crash.Environment.Gpu", i, gpu.Name ?? "", gpu.MemoryMb?.ToString() ?? "",
                gpu.DriverVersion ?? ""));
        }

        return builder.ToString();
    }

    private static void _Append(StringBuilder builder, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) builder.AppendLine(Lang.Text(key, value));
    }
}

/// <summary>
///     构建错误报告时使用的脱敏选项。
/// </summary>
public sealed record CrashReportBuildOptions
{
    public IReadOnlyList<string> UserNames { get; init; } = [];
    public IReadOnlyList<string> AccessTokens { get; init; } = [];
    public char AccessTokenMask { get; init; } = '*';
}

/// <summary>
///     错误报告包的内存表示，包含将要写入 zip 的全部条目。
/// </summary>
public sealed record CrashReportPackage(IReadOnlyList<CrashReportEntry> Entries);

/// <summary>
///     错误报告 zip 中的单个文件条目。
/// </summary>
public sealed record CrashReportEntry
{
    public required string FileName { get; init; }
    public required byte[] Content { get; init; }
}
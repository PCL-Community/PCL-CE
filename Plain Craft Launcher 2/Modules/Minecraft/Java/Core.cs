using System.Diagnostics.CodeAnalysis;
using System.IO;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java.UserPreference;
using PCL.Core.Utils;

namespace PCL;

public static partial class JavaRuntime
{
    /// <summary>
    ///     防止多个需要 Java 的部分同时要求下载 Java。
    /// </summary>
    public static readonly object JavaLock = new();

    /// <summary>
    ///     目前所有可用的 Java。
    /// </summary>
    public static JavaManager Javas => JavaService.JavaManager;

    /// <summary>
    ///     根据要求返回最适合的 Java，若找不到则返回 Nothing。
    ///     最小与最大版本在与输入相同时也会通过。
    ///     必须在工作线程调用，且必须包括 SyncLock JavaLock。
    /// </summary>
    public static JavaEntry JavaSelect(
        string cancelException,
        Version? minVersion = null,
        Version? maxVersion = null,
        McInstance? relatedInstance = null,
        bool enforceVersionRange = false,
        IReadOnlySet<int>? compatibleMajors = null)
    {
        Common.Log(
            $"[Java] 要求选择合适 Java，"
            + $"要求最低版本 {(minVersion is not null ? minVersion.ToString() : "未指定")}，"
            + $"要求选择的最高版本 {(maxVersion is not null ? maxVersion.ToString() : "未指定")}，"
            + $"关联实例 {(relatedInstance is not null ? relatedInstance.Name : "未指定")}");

        if (TrySelectInstanceJava(
                relatedInstance,
                minVersion,
                maxVersion,
                compatibleMajors,
                out var instanceJava))
            return instanceJava;

        if (TrySelectGlobalJava(
                minVersion,
                maxVersion,
                enforceVersionRange,
                compatibleMajors,
                out var globalJava))
            return globalJava;

        return SelectAutomaticJava(minVersion, maxVersion, compatibleMajors);
    }

    private static bool TrySelectInstanceJava(
        McInstance? relatedInstance,
        Version? minVersion,
        Version? maxVersion,
        IReadOnlySet<int>? compatibleMajors,
        [NotNullWhen(true)] out JavaEntry? selected)
    {
        selected = null;

        if (relatedInstance?.PathInstance is null)
            return false;

        var rawPreference = Config.Instance.SelectedJava[relatedInstance.PathInstance];
        if (string.IsNullOrWhiteSpace(rawPreference))
        {
            Common.Log($"[Java] 实例 '{relatedInstance.Name}' 无 Java 偏好配置，使用自动选择策略");
            return false;
        }

        var preference = GetInstanceJavaPreference(relatedInstance);
        if (preference is null)
        {
            Common.Log($"[Java] 实例 '{relatedInstance.Name}' 未指定 Java 偏好（空值），使用自动选择策略");
            return false;
        }

        switch (preference)
        {
            case ExistingJava existingJava:
            {
                var candidate = Javas.AddOrGet(existingJava.JavaExePath);
                if (candidate is null || !candidate.IsEnabled)
                {
                    Common.Log(
                        $"[Java] 警告：实例指定的 Java 路径无效或不可用: {existingJava.JavaExePath}");
                    return false;
                }

                if (!IsVersionSuitable(candidate.Installation.Version, minVersion, maxVersion)
                    || !IsAllowedMajor(candidate, compatibleMajors))
                    HintService.Hint(
                        _GetJavaRangeWarning(
                            "Minecraft.Launch.Java.Compatibility.InstanceSelectedOutOfRange",
                            candidate.Installation.Version,
                            minVersion,
                            maxVersion));

                Common.Log(
                    $"[Java] 返回实例 '{relatedInstance.Name}' 指定的 Java: {candidate}");
                selected = candidate;
                return true;
            }

            case UseRelativePath relativePath:
            {
                var absolutePath = Path.GetFullPath(
                    Path.Combine(
                        Basics.ExecutableDirectory,
                        relativePath.RelativePath));

                if (!Files.IsPathWithinDirectory(absolutePath, Basics.ExecutableDirectory))
                {
                    Common.Log($"[Java] 警告：实例相对路径指定的 Java 无效: {absolutePath}");
                    return false;
                }

                var candidate = Javas.Get(absolutePath);
                if (candidate is null || !candidate.IsEnabled)
                    return false;

                if (!IsVersionSuitable(candidate.Installation.Version, minVersion, maxVersion)
                    || !IsAllowedMajor(candidate, compatibleMajors))
                    HintService.Hint(
                        _GetJavaRangeWarning(
                            "Minecraft.Launch.Java.Compatibility.RelativePathSelectedOutOfRange",
                            candidate.Installation.Version,
                            minVersion,
                            maxVersion),
                        HintType.Error);

                Common.Log(
                    $"[Java] 返回实例 '{relatedInstance.Name}' 相对路径指定的 Java ({relativePath.RelativePath}): {candidate}");
                selected = candidate;
                return true;
            }

            case UseGlobalPreference:
                Common.Log(
                    $"[Java] 实例 '{relatedInstance.Name}' 配置为使用全局 Java 设置，继续检查全局配置");
                return false;

            default:
                Common.Log($"[Java] 警告：未知的 Java 偏好类型 '{preference}'，跳过处理");
                return false;
        }
    }

    private static bool TrySelectGlobalJava(
        Version? minVersion,
        Version? maxVersion,
        bool enforceVersionRange,
        IReadOnlySet<int>? compatibleMajors,
        [NotNullWhen(true)] out JavaEntry? selected)
    {
        selected = null;
        var globalJavaPath = Config.Launch.SelectedJava;

        if (string.IsNullOrWhiteSpace(globalJavaPath))
        {
            Common.Log("[Java] 无全局 Java 配置，使用自动选择策略");
            return false;
        }

        globalJavaPath = globalJavaPath.Trim();
        var candidate = Javas.AddOrGet(globalJavaPath);

        if (candidate is null || !candidate.IsEnabled)
        {
            Common.Log($"[Java] 警告：全局指定的 Java 路径无效或不可用: {globalJavaPath}");
            return false;
        }

        var versionSuitable =
            IsVersionSuitable(candidate.Installation.Version, minVersion, maxVersion)
            && IsAllowedMajor(candidate, compatibleMajors);

        if (enforceVersionRange && !versionSuitable)
        {
            Common.Log(
                $"[Java] 全局指定的 Java 版本不满足强制范围要求，忽略并继续自动搜索: {candidate}");
            return false;
        }

        if (!versionSuitable)
            HintService.Hint(
                _GetJavaRangeWarning(
                    "Minecraft.Launch.Java.Compatibility.GlobalSelectedOutOfRange",
                    candidate.Installation.Version,
                    minVersion,
                    maxVersion));

        Common.Log($"[Java] 返回全局指定的 Java: {candidate}");
        selected = candidate;
        return true;
    }

    private static JavaEntry? SelectAutomaticJava(
        Version? minVersion,
        Version? maxVersion,
        IReadOnlySet<int>? compatibleMajors)
    {
        Common.Log("[Java] 开始自动搜索符合版本要求的 Java 运行时");
        Javas.CheckAllAvailability();

        var requiredMin = minVersion ?? new Version(1, 0, 0);
        var requiredMax = maxVersion ?? new Version(999, 999, 999);
        var candidates = FilterByCompatibleMajors(
            Javas
                .SelectSuitableJavaAsync(requiredMin, requiredMax)
                .GetAwaiter()
                .GetResult(),
            compatibleMajors);
        var selected = candidates.FirstOrDefault();

        if (selected is null && candidates.Length == 0)
        {
            Common.Log("[Java] 未找到符合版本要求的 Java，触发全盘重新扫描");
            Javas.ScanJavaAsync().GetAwaiter().GetResult();
            candidates = FilterByCompatibleMajors(
                Javas
                    .SelectSuitableJavaAsync(requiredMin, requiredMax)
                    .GetAwaiter()
                    .GetResult(),
                compatibleMajors);
            selected = candidates.FirstOrDefault();
        }

        Common.Log(
            selected is not null
                ? $"[Java] 返回自动选择的 Java: {selected}"
                : "[Java] 最终未能确定可用的 Java 运行时");

        return selected;
    }

    private static JavaEntry[] FilterByCompatibleMajors(
        JavaEntry[] entries,
        IReadOnlySet<int>? compatibleMajors)
    {
        return compatibleMajors is null
            ? entries
            :
            [
                .. entries.Where(entry => compatibleMajors
                    .Contains(JavaManager.NormalizeVersion(entry.Installation.Version).Major))
            ];
    }

    private static bool IsAllowedMajor(
        JavaEntry entry,
        IReadOnlySet<int>? compatibleMajors)
    {
        return compatibleMajors is null
            || compatibleMajors.Contains(
                JavaManager.NormalizeVersion(entry.Installation.Version).Major);
    }

    private static bool IsVersionSuitable(
        Version version,
        Version? minVersion,
        Version? maxVersion)
    {
        return (minVersion is null || version >= minVersion)
            && (maxVersion is null || version <= maxVersion);
    }

    private static string _GetJavaRangeWarning(
        string key,
        Version selectedVersion,
        Version? minVersion,
        Version? maxVersion)
    {
        return Lang.Text(
            key,
            selectedVersion,
            minVersion?.ToString() ?? Lang.Text("Minecraft.Launch.Java.Compatibility.NoMinimum"),
            maxVersion?.ToString() ?? Lang.Text("Minecraft.Launch.Java.Compatibility.NoMaximum"));
    }

    public static JavaPreference GetInstanceJavaPreference(McInstance instance)
    {
        var rawPreference = Config.Instance.SelectedJava[instance.PathInstance];
        JavaPreference? preference = null;

        if (!string.IsNullOrEmpty(rawPreference))
            try
            {
                preference = JsonSerializer.Deserialize<JavaPreference>(
                    rawPreference,
                    JsonCompat.SerializerOptions);
            }
            catch (JsonException)
            {
            }

        if (preference is null)
        {
            var trimmed = rawPreference?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                preference = new AutoSelect();
            else if (trimmed == "使用全局设置")
                preference = new UseGlobalPreference();
            else
                preference = new ExistingJava(trimmed);
        }

        if ((preference is ExistingJava java && !Path.IsPathRooted(java.JavaExePath))
            || (preference is UseRelativePath path
                && !Files.IsPathWithinDirectory(path.RelativePath, Basics.ExecutableDirectory)))
            return new UseGlobalPreference();

        return preference;
    }

    /// <summary>
    ///     是否强制指定了 64 位 Java。如果没有强制指定，返回是否安装了 64 位 Java。
    /// </summary>
    public static bool IsGameSet64BitJava(McInstance? relatedVersion = null)
    {
        try
        {
            var userSetup = Config.Launch.SelectedJava;
            if (userSetup.StartsWith('{'))
            {
                var json = Common.GetJson(userSetup);
                userSetup = $"{json["Path"]}java.exe";
                Config.Launch.SelectedJava = userSetup;
            }

            if (relatedVersion is not null)
            {
                var instancePreference = GetInstanceJavaPreference(relatedVersion);

                switch (instancePreference)
                {
                    case AutoSelect:
                        return Javas.Existing64BitJava();

                    case ExistingJava existingJava:
                    {
                        var java = Javas.AddOrGet(existingJava.JavaExePath);
                        return java is not null && java.Installation.Is64Bit;
                    }

                    case UseRelativePath relativePath:
                    {
                        var javaExePath = Path.GetFullPath(relativePath.RelativePath);
                        if (Files.IsPathWithinDirectory(javaExePath, Basics.ExecutableDirectory))
                        {
                            var java = Javas.Get(javaExePath);
                            return java is not null && java.Installation.Is64Bit;
                        }

                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(userSetup) && !File.Exists(userSetup))
            {
                Config.Launch.SelectedJava = "";
                userSetup = string.Empty;
            }

            if (string.IsNullOrEmpty(userSetup))
                return Javas.Existing64BitJava();

            var selectedJava = Javas.AddOrGet(userSetup);
            return selectedJava is not null && selectedJava.Installation.Is64Bit;
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "检查 Java 类别时出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Launch.Java.Compatibility.CheckFailed"));

            if (relatedVersion is not null)
                Config.Instance.SelectedJava[relatedVersion.PathInstance] = "使用全局设置";

            Config.Launch.SelectedJava = "";
        }

        return true;
    }
}
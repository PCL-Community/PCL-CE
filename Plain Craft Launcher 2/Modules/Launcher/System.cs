using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL;

public static partial class Launcher
{
    private static bool _isTaskTempCleared;
    private static bool _isTaskTempClearing;

    [DllImport("user32")]
    public static extern int SetForegroundWindow(nint hWnd);

    /// <summary>
    ///     将特定程序设置为使用高性能显卡启动。
    ///     如果失败，则抛出异常。
    /// </summary>
    public static void SetGpuPreference(string executable, bool wantHighPerformance = true)
    {
        const string gpuPreferenceRegistryKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
        const string highPerformancePreference = "GpuPreference=2;";
        const string defaultPreference = "GpuPreference=0;";

        var isCurrentHighPerformance = false;

        using (var readOnlyKey = Registry.CurrentUser.OpenSubKey(gpuPreferenceRegistryKey, false))
        {
            if (readOnlyKey is null)
            {
                Common.Log("[System] 需要创建显卡设置的父级键");
                Registry.CurrentUser.CreateSubKey(gpuPreferenceRegistryKey);
            }
            else
            {
                var currentValue = readOnlyKey.GetValue(executable)?.ToString();
                isCurrentHighPerformance = currentValue == highPerformancePreference;
            }
        }

        Common.Log(
            $"[System] 当前程序 ({executable}) 的显卡设置为高性能: {isCurrentHighPerformance}");

        if (isCurrentHighPerformance == wantHighPerformance)
            return;

        using var writeKey = Registry.CurrentUser.OpenSubKey(gpuPreferenceRegistryKey, true);
        writeKey?.SetValue(
            executable,
            wantHighPerformance ? highPerformancePreference : defaultPreference);

        Common.Log($"[System] 已调整程序 ({executable}) 显卡设置: {wantHighPerformance}");
    }

    /// <summary>
    ///     对替换标记进行处理。会对替换内容使用 EscapeHandler 进行转义。
    /// </summary>
    public static string? ArgumentReplace(
        string? text,
        Func<string, string>? escapeHandler = null,
        bool replaceTime = true)
    {
        if (text is null)
            return null;

        text = ArgumentReplaceBase(text, Replacer);

        if (replaceTime)
            text = ArgumentReplaceTime(text, Replacer);

        text = ArgumentReplaceMinecraft(text, Replacer);
        text = ArgumentReplaceInstance(text, Replacer);
        text = ArgumentReplaceLogin(text, Replacer);

        return ArgumentReplaceAdvanced(text, Replacer);

        string Replacer(string? value)
        {
            if (value is null)
                return "";

            if (escapeHandler is null)
                return value;

            if (value.Contains(":\\"))
                value = Common.ShortenPath(value);

            return escapeHandler(value);
        }
    }

    private static string ArgumentReplaceBase(
        string text,
        Func<string?, string> replacer)
    {
        text = text.Replace("{pcl_version}", replacer(Common.VersionBaseName));
        text = text.Replace("{pcl_version_code}", replacer(Common.VersionCode.ToString()));
        text = text.Replace("{pcl_version_branch}", replacer(Common.VersionBranchName));
        text = text.Replace("{pcl_branch}", replacer(Common.VersionBranchName));
        text = text.Replace("{identify}", replacer(Identify.LauncherId));
        text = text.Replace("{path}", replacer(Basics.ExecutableDirectory));
        text = text.Replace("{path_with_name}", replacer(Basics.ExecutableName));
        text = text.Replace("{path_temp}", replacer(Common.pathTemp));

        return text;
    }

    private static string ArgumentReplaceTime(
        string text,
        Func<string?, string> replacer)
    {
        text = text.Replace("{date}", replacer(Lang.Date(DateTime.Now, "d")));
        text = text.Replace("{time}", replacer(Lang.Date(DateTime.Now, "T")));

        return text;
    }

    private static string ArgumentReplaceMinecraft(
        string text,
        Func<string?, string> replacer)
    {
        text = text.Replace(
            "{java}",
            replacer(GameLaunch.mcLaunchJavaSelected?.Installation.JavaFolder));
        text = text.Replace("{minecraft}", replacer(GameFolders.mcFolderSelected));

        return text;
    }

    private static string ArgumentReplaceInstance(
        string text,
        Func<string?, string> replacer)
    {
        var instance = InstanceList.McMcInstanceSelected;

        if (instance is null)
        {
            text = text.Replace("{version_path}", replacer(null));
            text = text.Replace("{verpath}", replacer(null));
            text = text.Replace("{version_indie}", replacer(null));
            text = text.Replace("{verindie}", replacer(null));
            text = text.Replace("{name}", replacer(null));
            text = text.Replace("{version}", replacer(null));

            return text;
        }

        text = text.Replace("{version_path}", replacer(instance.PathInstance));
        text = text.Replace("{verpath}", replacer(instance.PathInstance));
        text = text.Replace("{version_indie}", replacer(instance.PathIndie));
        text = text.Replace("{verindie}", replacer(instance.PathIndie));
        text = text.Replace("{name}", replacer(instance.Name));

        var vanillaName = instance.Info.VanillaName;
        var versionName = vanillaName is "unknown" or "old" or "pending"
            ? instance.Name
            : vanillaName;

        return text.Replace("{version}", replacer(versionName));
    }

    private static string ArgumentReplaceLogin(
        string text,
        Func<string?, string> replacer)
    {
        var loginLoader = GameLaunch.mcLoginLoader;

        if (loginLoader.State != Common.LoadState.Finished)
        {
            text = text.Replace("{user}", replacer(null));
            text = text.Replace("{uuid}", replacer(null));
            text = text.Replace("{login}", replacer(null));

            return text;
        }

        text = text.Replace("{user}", replacer(loginLoader.output.Name));
        text = text.Replace("{uuid}", replacer(loginLoader.output.Uuid.ToLower()));

        text = loginLoader.input?.LoginType switch
        {
            GameLaunch.McLoginType.Legacy => text.Replace("{login}", replacer("离线")),
            GameLaunch.McLoginType.Ms => text.Replace("{login}", replacer("正版")),
            GameLaunch.McLoginType.Auth => text.Replace("{login}", replacer("Authlib-Injector")),
            _ => throw new ArgumentOutOfRangeException()
        };

        return text;
    }

    private static string ArgumentReplaceAdvanced(
        string text,
        Func<string?, string> replacer)
    {
        text = text.RegexReplaceEach(@"\{hint\}", _ => replacer(PageToolsTest.GetRandomHint()));
        text = text.RegexReplaceEach(@"\{cave\}", _ => replacer(PageToolsTest.GetRandomCave()));
        text = text.RegexReplaceEach(
            @"\{setup:([a-zA-Z0-9]+)\}",
            m =>
            {
                if (ConfigService.TryGetConfigItemNoType(m.Groups[1].Value, out var item)
                    && item.Source != ConfigSource.SharedEncrypt)
                    return replacer(
                        item
                            .GetValueNoType(InstanceList.McMcInstanceSelected?.PathInstance)
                            ?.ToString()
                        ?? "");

                return replacer("");
            });

        text = text.RegexReplaceEach(
            @"\{varible:([^:\}]+)(?::([^\}]+))?\}",
            m => replacer(CustomEvent.GetCustomVariable(m.Groups[1].Value, m.Groups[2].Value)));

        text = text.RegexReplaceEach(
            @"\{variable:([^:\}]+)(?::([^\}]+))?\}",
            m => replacer(CustomEvent.GetCustomVariable(m.Groups[1].Value, m.Groups[2].Value)));

        return text;
    }

    /// <summary>
    ///     尝试清理任务缓存文件夹。
    ///     在整次运行中只会实际清理一次。
    /// </summary>
    public static void TryClearTaskTemp()
    {
        if (_isTaskTempCleared)
        {
            while (_isTaskTempClearing)
                Thread.Sleep(1);

            return;
        }

        _isTaskTempCleared = true;
        _isTaskTempClearing = true;

        try
        {
            Common.Log("[System] 开始清理任务缓存文件夹");
            Common.DeleteDirectory(
                Path.Combine(SystemPaths.DriveLetter, "ProgramData", "PCL", "TaskTemp"));
            Common.DeleteDirectory($@"{Common.pathTemp}TaskTemp\");
            Common.Log("[System] 已清理任务缓存文件夹");
        }
        catch (Exception ex)
        {
            Common.Log(ex, "清理任务缓存文件夹失败");
        }
        finally
        {
            _isTaskTempClearing = false;
        }
    }

    /// <summary>
    ///     申请一个可用于任务缓存的临时文件夹，以 \ 结尾。这些文件夹无需进行后续清理。
    ///     若所有缓存位置均没有权限，会抛出异常。
    /// </summary>
    /// <param name="requireNonSpace">是否要求路径不包含空格。</param>
    public static string RequestTaskTempFolder(bool requireNonSpace = false)
    {
        TryClearTaskTemp();

        try
        {
            var tempFolder =
                $@"{Common.pathTemp}TaskTemp\{Common.GetUuid()}-{RandomUtils.NextInt(0, 1000000)}\";

            if (!requireNonSpace || !tempFolder.Contains(' '))
            {
                Directory.CreateDirectory(tempFolder);
                Common.CheckPermissionWithException(tempFolder);
                return tempFolder;
            }
        }
        catch
        {
            // ignored
        }

        var fallbackFolder = Path.Combine(
            SystemPaths.DriveLetter,
            "ProgramData",
            "PCL",
            "TaskTemp",
            $"{Common.GetUuid()}-{RandomUtils.NextInt(0, 1000000)}");

        Directory.CreateDirectory(fallbackFolder);
        Common.CheckPermission(fallbackFolder);

        return fallbackFolder;
    }
}

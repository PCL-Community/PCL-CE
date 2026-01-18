using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PCL.Core.App;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Utils;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;

namespace PCL.Core.Minecraft.Launch.Utils;

public class GameEnvReplacer(IMcInstance instance, JavaInfo selectedJava) {
    private readonly IJsonBasedInstance _jsonBasedInstance = (IJsonBasedInstance)instance;
    
    // ReSharper disable InconsistentNaming
    // 常量定义
    private const string LAUNCHER_NAME = "PCLCE";
    private const string LAUNCHER_VERSION = "409";
    private const string DEFAULT_USER_TYPE = "msa";
    private const int MINECRAFT_LEGACY_VERSION_BUILD = 12;
    private const int JAVA_VERSION_8 = 8;
    // ReSharper restore InconsistentNaming
    
    /// <summary>
    /// 应用参数替换
    /// </summary>
    public static string ApplyReplacements(string argument, Dictionary<string, string> replacements) {
        return replacements.Aggregate(argument, (current, replacement) =>
            current.Replace(replacement.Key, replacement.Value));
    }
    
    
    /// <summary>
    /// 构建参数替换字典
    /// </summary>
    public async Task<Dictionary<string, string>> BuildArgumentReplacementsAsync() {
        var gameArguments = new Dictionary<string, string> {
            // 基础路径参数
            ["${classpath_separator}"] = ";",
            ["${natives_directory}"] = GetNativesFolder(),
            ["${library_directory}"] = Path.Combine(instance.Folder.Path, "libraries"),
            ["${libraries_directory}"] = Path.Combine(instance.Folder.Path, "libraries"),
            ["${game_directory}"] = instance.IsolatedPath.TrimEnd('\\'),
            ["${assets_root}"] = Path.Combine(instance.Folder.Path, "assets"),

            // 启动器信息
            ["${launcher_name}"] = LAUNCHER_NAME,
            ["${launcher_version}"] = LAUNCHER_VERSION, // TODO: 等待迁移

            // 版本信息
            ["${version_name}"] = instance.Name,
            ["${version_type}"] = GetVersionType(),

            // 用户信息
            ["${user_properties}"] = "{}",
            ["${user_type}"] = DEFAULT_USER_TYPE,

            // 资源相关
            ["${game_assets}"] = Path.Combine(instance.Folder.Path, "assets", "virtual", "legacy"),
            ["${assets_index_name}"] = McLaunchUtils.GetAssetsIndexName(_jsonBasedInstance),

            // ClassPath
            ["${classpath}"] = await BuildClassPathAsync(),
        };

        // 添加窗口尺寸参数
        var gameSize = CalculateGameWindowSize();
        gameArguments["${resolution_width}"] = $"{Math.Round(gameSize.Width)}";
        gameArguments["${resolution_height}"] = $"{Math.Round(gameSize.Height)}";

        return gameArguments;
    }
    
    /// <summary>
    /// 获取版本类型信息
    /// </summary>
    private string GetVersionType() {
        var argumentInfo = Config.Instance.TypeInfo[instance.Path];
        return string.IsNullOrEmpty(argumentInfo) ? Config.Launch.TypeInfo : argumentInfo;
    }

    /// <summary>
    /// 计算游戏窗口大小
    /// </summary>
    private Size CalculateGameWindowSize() {
        var gameSize = Config.Launch.GameWindowMode switch {
            2 => CalculateMainWindowSize(),
            3 => new Size(Math.Max(100, Config.Launch.GameWindowWidth),
                Math.Max(100, Config.Launch.GameWindowHeight)),
            _ => new Size(854, 480)
        };

        return ApplyDpiFixIfNeeded(gameSize);
    }
    
    /// <summary>
    /// 计算主窗口大小
    /// </summary>
    private static Size CalculateMainWindowSize() {
        // TODO: 实现与启动器窗口尺寸一致的逻辑
        var result = new Size(854, 480);
        result.Height -= 29.5 * WindowInterop.GetSystemDpi() / 96; // 标题栏高度
        return result;
    }

    /// <summary>
    /// 应用 DPI 修复（如果需要）
    /// </summary>
    private Size ApplyDpiFixIfNeeded(Size gameSize) {
        if (NeedsDpiFix()) {
            McLaunchUtils.Log($"应用窗口大小 DPI 修复（Java 版本：{selectedJava.Version.Revision}）");
            var dpiScale = WindowInterop.GetSystemDpi() / 96.0;
            gameSize.Width /= dpiScale;
            gameSize.Height /= dpiScale;
        }

        return gameSize;
    }

    /// <summary>
    /// 判断是否需要 DPI 修复
    /// </summary>
    private bool NeedsDpiFix() {
        return instance.InstanceInfo.McVersionBuild <= MINECRAFT_LEGACY_VERSION_BUILD &&
               selectedJava.JavaMajorVersion <= JAVA_VERSION_8 &&
               selectedJava.Version.Revision is >= 200 and <= 321 &&
               !instance.InstanceInfo.HasPatch("optifine") &&
               !instance.InstanceInfo.HasPatch("forge");
    }

    /// <summary>
    /// 获取 Natives 文件夹路径
    /// </summary>
    private string GetNativesFolder() {
        var primaryPath = Path.Combine(instance.Path, instance.Name, "-natives");
        if (EncodingUtils.IsDefaultEncodingGbk() || primaryPath.IsASCII()) {
            return primaryPath;
        }

        var fallbackPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft", "bin", "natives");
        if (fallbackPath.IsASCII()) {
            return fallbackPath;
        }

        return Path.Combine(SystemPaths.DriveLetter, "ProgramData", "PCL", "natives");
    }
    
    /// <summary>
    /// 构建 ClassPath 字符串
    /// </summary>
    private async Task<string> BuildClassPathAsync() {
        var cpStrings = new List<string> {
            await LaunchEnvUtils.ExtractRetroWrapperAsync(instance)
        };

        // TODO: 等待实例下载部分实现
        /*
        var libList = McLibListGet(instance, true);
        string? optiFineCp = null;

        foreach (var library in libList)
        {
            if (library.IsNatives) continue;

            if (library.Name?.Contains("com.cleanroommc:cleanroom:0.2") == true)
            {
                cpStrings.Insert(0, library.LocalPath); // Cleanroom 必须在第一位
            }
            else if (library.Name == "optifine:OptiFine")
            {
                optiFineCp = library.LocalPath;
            }
            else
            {
                cpStrings.Add(library.LocalPath);
            }
        }

        if (optiFineCp != null)
        {
            cpStrings.Insert(cpStrings.Count - 2, optiFineCp); // OptiFine 放在倒数第二位
        }
        */

        return string.Join(";", cpStrings);
    }
}

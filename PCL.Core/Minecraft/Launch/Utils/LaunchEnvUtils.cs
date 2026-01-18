using System;
using System.IO;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Instance.Interface;

namespace PCL.Core.Minecraft.Launch.Utils;

public static class LaunchEnvUtils {
    private const string JavaWrapperResource = "Resources/java-wrapper.jar";
    private const string DebugLegacyLog4j2ConfigResource = "Resources/log4j2-legacy-debug.xml";
    private const string DebugLog4j2ConfigResource = "Resources/log4j2-debug.xml";
    private const string LinkDResource = "Resources/linkd.exe";

    private static readonly object ExtractJavaWrapperLock = new();
    private static readonly object ExtractLegacyDebugLog4j2ConfigLock = new();
    private static readonly object ExtractDebugLog4j2ConfigLock = new();
    private static readonly object ExtractLinkDLock = new();

    public static string ExtractJavaWrapper() => ExtractFile(JavaWrapperResource, "JavaWrapper.jar", ExtractJavaWrapperLock);
    public static string ExtractLegacyDebugLog4j2Config() => ExtractFile(DebugLegacyLog4j2ConfigResource, "log4j2-legacy-debug.xml", ExtractLegacyDebugLog4j2ConfigLock);
    public static string ExtractDebugLog4j2Config() => ExtractFile(DebugLog4j2ConfigResource, "log4j2-debug.xml", ExtractDebugLog4j2ConfigLock);
    public static string ExtractLinkD() => ExtractFile(LinkDResource, "linkd.exe", ExtractLinkDLock);

    private static string ExtractFile(string resourceName, string fileName, object lockObj) {
        var filePath = Path.Combine(FileService.TempPath, fileName);
        LogWrapper.Info(resourceName, $"选定路径：{filePath}");

        lock (lockObj) {
            try {
                WriteResourceToFile(resourceName, filePath);
            } catch (Exception ex) {
                if (File.Exists(filePath)) {
                    LogWrapper.Warn(ex, $"{resourceName} 文件释放失败，尝试删除后重试");
                    File.Delete(filePath);
                    try {
                        WriteResourceToFile(resourceName, filePath);
                    } catch (Exception ex2) {
                        var fallbackPath = Path.Combine(FileService.TempPath, $"{Path.GetFileNameWithoutExtension(fileName)}2{Path.GetExtension(fileName)}");
                        LogWrapper.Warn(ex2, $"{resourceName} 重试失败，尝试新路径：{fallbackPath}");
                        WriteResourceToFile(resourceName, fallbackPath);
                        filePath = fallbackPath;
                    }
                } else {
                    throw new FileNotFoundException($"释放 {resourceName} 失败", ex);
                }
            }
        }
        return filePath;
    }

    private static void WriteResourceToFile(string resourceName, string path) {
        using var sourceStream = Basics.GetResourceStream(resourceName);
        if (sourceStream == null) {
            throw new FileNotFoundException($"资源 {resourceName} 未找到。");
        }

        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096);
        sourceStream.CopyTo(fileStream);
    }

    public static bool NeedRetroWrapper(IMcInstance mcNoPatchesInstance) {
        var versionInfo = mcNoPatchesInstance.InstanceInfo;

        var isOldVersion = versionInfo.McVersionMinor < 6 && versionInfo.McVersionMinor != 99;
        var isSpecificVersion = versionInfo.ReleaseTime >= new DateTime(2013, 6, 25) && versionInfo.McVersionMinor == 99;
        var isRwEnabled = !Config.Launch.DisableRw && !Config.Instance.DisableRw[mcNoPatchesInstance.Path];

        return (isOldVersion || isSpecificVersion) && isRwEnabled;
    }
    
    public static async Task<string> ExtractRetroWrapperAsync(IMcInstance instance) {
        // RetroWrapper 释放
        if (!NeedRetroWrapper(instance)) {
            return string.Empty;
        }
        
        var wrapperPath = Path.Combine(instance.Folder.Path, "libraries/retrowrapper/RetroWrapper.jar");
        try {
            await Files.WriteFileAsync(wrapperPath, Basics.GetResourceStream("Resources/retro-wrapper.jar"));
            return wrapperPath;
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "RetroWrapper 释放失败");
        }

        return string.Empty;
    }
}

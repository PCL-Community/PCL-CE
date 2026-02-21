using System;
using System.IO;
using PCL.Core.App;
using PCL.Core.Logging;

namespace PCL.Core.Minecraft.Launch.Utils;

public static class LaunchEnvUtils {
    private const string DebugLegacyLog4J2ConfigResource = "Resources/log4j2-legacy-debug.xml";
    private const string DebugLog4J2ConfigResource = "Resources/log4j2-debug.xml";

    private static readonly object _ExtractLegacyDebugLog4J2ConfigLock = new();
    private static readonly object _ExtractDebugLog4J2ConfigLock = new();

    public static string ExtractLegacyDebugLog4j2Config() => _ExtractFile(DebugLegacyLog4J2ConfigResource, "log4j2-legacy-debug.xml", _ExtractLegacyDebugLog4J2ConfigLock);
    public static string ExtractDebugLog4j2Config() => _ExtractFile(DebugLog4J2ConfigResource, "log4j2-debug.xml", _ExtractDebugLog4J2ConfigLock);

    private static string _ExtractFile(string resourceName, string fileName, object lockObj) {
        var filePath = Path.Combine(Paths.Temp, fileName);
        LogWrapper.Info(resourceName, $"选定路径：{filePath}");

        lock (lockObj) {
            try {
                _WriteResourceToFile(resourceName, filePath);
            } catch (Exception ex) {
                if (File.Exists(filePath)) {
                    LogWrapper.Warn(ex, $"{resourceName} 文件释放失败，尝试删除后重试");
                    File.Delete(filePath);
                    try {
                        _WriteResourceToFile(resourceName, filePath);
                    } catch (Exception ex2) {
                        var fallbackPath = Path.Combine(Paths.Temp, $"{Path.GetFileNameWithoutExtension(fileName)}2{Path.GetExtension(fileName)}");
                        LogWrapper.Warn(ex2, $"{resourceName} 重试失败，尝试新路径：{fallbackPath}");
                        _WriteResourceToFile(resourceName, fallbackPath);
                        filePath = fallbackPath;
                    }
                } else {
                    throw new FileNotFoundException($"释放 {resourceName} 失败", ex);
                }
            }
        }
        return filePath;
    }

    private static void _WriteResourceToFile(string resourceName, string path) {
        using var sourceStream = Basics.GetResourceStream(resourceName);
        if (sourceStream == null) {
            throw new FileNotFoundException($"资源 {resourceName} 未找到。");
        }

        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096);
        sourceStream.CopyTo(fileStream);
    }
}

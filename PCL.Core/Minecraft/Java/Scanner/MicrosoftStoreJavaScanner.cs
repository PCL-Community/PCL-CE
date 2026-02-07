using PCL.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace PCL.Core.Minecraft.Java.Scanner;
public class MicrosoftStoreJavaScanner : IJavaScanner
{
    private const string StorePackagePath =
        @"Packages\Microsoft.4297127D64EC6_8wekyb3d8bbwe\LocalCache\Local\runtime";

    public void Scan(ICollection<string> results)
    {
        try
        {
            var basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                StorePackagePath);

            if (!Directory.Exists(basePath)) return;

            // 第一级：java-runtime* 目录
            foreach (var runtimeDir in Directory.EnumerateDirectories(basePath))
            {
                if (!Path.GetFileName(runtimeDir).StartsWith("java-runtime", StringComparison.OrdinalIgnoreCase))
                    continue;

                // 第二级：架构目录 (windows-x64等)
                foreach (var archDir in Directory.EnumerateDirectories(runtimeDir))
                {
                    // 第三级：版本目录
                    foreach (var versionDir in Directory.EnumerateDirectories(archDir))
                    {
                        var javaExe = Path.Combine(versionDir, "bin", "java.exe");
                        if (File.Exists(javaExe))
                        {
                            LogWrapper.Info($"[Java] 检测到 Microsoft Store Java: {javaExe}");
                            results.Add(javaExe);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "Microsoft Store Java 扫描失败");
        }
    }
}

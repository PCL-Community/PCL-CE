using MFTLib;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.Utils.OS;
using PCL.Core.App.IoC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PCL.Core.Minecraft.Java.Scanner;

internal class MftJavaScanner : IJavaScanner
{
    private static readonly string[] _SkipDirectoryNames =
    [
        "$Recycle.Bin", "$RECYCLE.BIN",
        "Temp", "tmp",
        "Cache", "caches",
        "chrome", "chromium", "edge", "firefox", "opera",
    ];

    public void Scan(ICollection<string> results)
    {
        if (!OperatingSystem.IsWindows()) return;

        if (!ProcessInterop.IsAdmin())
        {
            LogWrapper.Info("[Java] 非管理员模式，跳过基于 MFT 的 Java 扫描");
            _ = Task.Run(async () =>
            {
                await Lifecycle.WaitForStateAsync(LifecycleState.WindowCreated);
                HintWrapper.Show("非管理员模式，已禁用 MFT 快速搜索", HintTheme.Info);
            });
            return;
        }

        try
        {
            _ScanViaMft(results);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "MFT 扫描失败");
        }
    }

    private static void _ScanViaMft(ICollection<string> results)
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => d.Name.TrimEnd('\\'));

        foreach (var drive in drives)
        {
            var driveLetter = drive[0].ToString();
            try
            {
                using var volume = MftVolume.Open(driveLetter);
                var records = volume.FindByName(
                    "java.exe",
                    MatchFlags.Contains | MatchFlags.ResolvePaths,
                    out _);

                foreach (var record in records)
                {
                    if (record.IsDirectory) continue;

                    var fullPath = record.FullPath;
                    if (string.IsNullOrEmpty(fullPath)) continue;
                    if (_IsSkippedPath(fullPath)) continue;
                    if (!fullPath.Contains(Path.Combine("bin", "java.exe"), StringComparison.OrdinalIgnoreCase)) continue;
                    if (!File.Exists(fullPath)) continue;

                    results.Add(fullPath);
                }
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "Java", $"MFT 扫描跳过驱动器 {driveLetter}: {ex.Message}");
                continue;
            }
        }
    }

    private static bool _IsSkippedPath(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => _SkipDirectoryNames.Contains(part, StringComparer.OrdinalIgnoreCase));
    }
}
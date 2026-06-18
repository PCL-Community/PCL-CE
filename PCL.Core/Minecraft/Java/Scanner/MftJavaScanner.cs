using MFTLib;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.Utils.OS;
using PCL.Core.App.IoC;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PCL.Core.App.Localization;

namespace PCL.Core.Minecraft.Java.Scanner;

internal class MftJavaScanner : IJavaScanner
{
    private static readonly HashSet<string> _SkipDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$Recycle.Bin",
        "$RECYCLE.BIN",
        "Temp",
        "tmp",
        "Cache",
        "caches",
        "chrome",
        "chromium",
        "edge",
        "firefox",
        "opera",
    };

    public void Scan(ICollection<string> results)
    {
        if (!OperatingSystem.IsWindows()) return;

        if (!ProcessInterop.IsAdmin())
        {
            LogWrapper.Info("[Java] 非管理员模式，跳过基于 MFT 的 Java 扫描");
            _ = Task.Run(async () =>
            {
                try
                {
                    await Lifecycle.WaitForStateAsync(LifecycleState.WindowCreated);
                    HintWrapper.Show(Lang.Text("Setup.Launch.Java.Hint.MftJavaScanner"), HintTheme.Info);
                }
                catch (Exception ex)
                {
                    LogWrapper.Warn(ex, "Java", "非管理员提示任务失败");
                }
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
            .Select(d => d.Name.TrimEnd('\\'))
            .Where(d => d.Length > 0)
            .ToList();

        if (drives.Count == 0) return;

        var parallelResults = new ConcurrentBag<string>();

        Parallel.ForEach(drives, drive =>
        {

            var driveLetter = drive[..1];
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
                    if (_SkipDirectoryNames.Overlaps(fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))) continue;
                    if (!fullPath.EndsWith(Path.Combine("bin", "java.exe"), StringComparison.OrdinalIgnoreCase)) continue;
                    if (!File.Exists(fullPath)) continue;

                    parallelResults.Add(fullPath);
                }
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "Java", $"MFT 扫描跳过驱动器 {driveLetter}");
            }
        });

        foreach (var path in parallelResults)
        {
            results.Add(path);
        }
    }
}
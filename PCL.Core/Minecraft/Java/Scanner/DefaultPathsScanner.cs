using PCL.Core.App;
using PCL.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace PCL.Core.Minecraft.Java.Scanner;

public class DefaultPathsScanner : IJavaScanner
{
    private const int MaxSearchDepth = 8;

    public void Scan(ICollection<string> results)
    {
        try
        {
            var searchRoots = _GetSearchRoots();
            LogWrapper.Info($"[Java] 对下列目录进行广度关键词搜索:{Environment.NewLine}{string.Join(Environment.NewLine, searchRoots)}");

            foreach (var root in searchRoots)
            {
                _BfsSearch(root, results);
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "默认路径扫描失败");
        }
    }

    private static HashSet<string> _GetSearchRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Path.Combine(Basics.ExecutableDirectory, "PCL")
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var keyFolders = new[] { "Program Files", "Program Files (x86)" };
            var drives = DriveInfo.GetDrives()
                .Where(d => d.DriveType.Equals(DriveType.Fixed) && d.IsReady)
                .Select(d => d.Name);

            foreach (var drive in drives)
            {
                foreach (var folder in keyFolders)
                {
                    roots.Add(Path.Combine(drive, folder));
                }

                // 根目录关键词搜索
                try
                {
                    var rootDirs = Directory.EnumerateDirectories(drive)
                        .Where(dir => JavaConsts.MostPossibleKeywords.Any(k =>
                            Path.GetFileName(dir).Contains(k, StringComparison.OrdinalIgnoreCase)));

                    foreach (var dir in rootDirs)
                        roots.Add(dir);
                }
                catch (UnauthorizedAccessException) { /* 忽略无权限目录 */ }
                catch (IOException) { /* 忽略IO错误 */ }
            }
        }
        else
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if (!string.IsNullOrEmpty(programFiles) && Directory.Exists(programFiles))
                roots.Add(programFiles);
            if (!string.IsNullOrEmpty(programFilesX86) && Directory.Exists(programFilesX86))
                roots.Add(programFilesX86);
        }

        return roots;
    }

    private static void _BfsSearch(string rootPath, ICollection<string> results)
    {
        if (!Directory.Exists(rootPath)) return;

        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((rootPath, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth > MaxSearchDepth || !Directory.Exists(current)) continue;

            try
            {
                // 深度0时只遍历含关键词的目录
                var dirsToScan = depth == 0
                    ? Directory.EnumerateDirectories(current)
                        .Where(dir => _ShouldScanDirectory(dir))
                    : Directory.EnumerateDirectories(current);

                foreach (var dir in dirsToScan)
                {
                    var javaExe = Path.Combine(dir, "java.exe");
                    if (File.Exists(javaExe))
                    {
                        results.Add(javaExe);
                    }
                    else
                    {
                        queue.Enqueue((dir, depth + 1));
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
            {
                LogWrapper.Debug($"跳过目录 {current}: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Java", $"搜索目录 {current} 时出错");
            }
        }
    }

    private static bool _ShouldScanDirectory(string path)
    {
        var name = Path.GetFileName(path);
        if (JavaConsts.ExcludeFolderNames.Any(ex => name.Contains(ex, StringComparison.OrdinalIgnoreCase)))
            return false;

        return JavaConsts.AllKeyworkds.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
}
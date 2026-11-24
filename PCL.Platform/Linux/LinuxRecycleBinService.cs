using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using PCL.Core.IO;
using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Platform;

public sealed class LinuxRecycleBinService : IRecycleBinService
{
    private static readonly uint UserId = OperatingSystem.IsLinux() ? _GetUserId() : 0;

    public bool IsSupported(string? path)
    {
        if (!_Exists(path)) return false;
        var full = Path.GetFullPath(path!);
        return _CanDelete(full)
            && (_CanUseGio() || _TryResolveTrash(full, out _, out _));
    }

    public RecycleBinOutcome Move(string? path, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path) || !_Exists(path))
        {
            error = "路径不存在";
            return RecycleBinOutcome.Failed;
        }

        var full = Path.GetFullPath(path);
        if (!_CanDelete(full))
        {
            error = "没有删除权限";
            return RecycleBinOutcome.Failed;
        }

        if (_TryGioTrash(full, out var gioError))
            return RecycleBinOutcome.Moved;

        if (!_TryResolveTrash(full, out var trashRoot, out var topDir))
        {
            error = string.IsNullOrWhiteSpace(gioError) ? "无法确定回收站位置" : gioError;
            return RecycleBinOutcome.Unsupported;
        }

        return _MoveToTrash(full, trashRoot, topDir, out error);
    }

    private static bool _TryGetTopDirTrash(string topDir, out string trashRoot)
    {
        trashRoot = string.Empty;
        var shared = Path.Combine(topDir, ".Trash");
        if (_IsValidSharedTrash(shared))
        {
            var sharedUser = Path.Combine(
                shared,
                UserId.ToString(CultureInfo.InvariantCulture));
            if (_IsUsableTrashDirectory(sharedUser))
            {
                trashRoot = sharedUser;
                return true;
            }
        }

        var own = Path.Combine(
            topDir,
            $".Trash-{UserId.ToString(CultureInfo.InvariantCulture)}");
        if (_IsUsableTrashDirectory(own))
        {
            trashRoot = own;
            return true;
        }

        return false;
    }

    private static bool _IsUsableTrashDirectory(string path)
    {
        return !_IsSymlink(path)
            && (!_Exists(path) || Directory.Exists(path))
            && _CanWriteDirectoryOrParent(path);
    }

    private static bool _TryGioTrash(string path, out string? error)
    {
        error = null;
        if (!_CanUseGio()) return false;

        try
        {
            var info = new ProcessStartInfo("gio")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            info.ArgumentList.Add("trash");
            info.ArgumentList.Add("--");
            info.ArgumentList.Add(path);

            using var process = Process.Start(info);
            if (process is null) return false;

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    // 进程可能刚好退出。
                }

                error = "gio trash 超时";
                return false;
            }

            _ = outputTask.GetAwaiter().GetResult();
            var standardError = errorTask.GetAwaiter().GetResult().Trim();
            if (process.ExitCode == 0) return true;

            error = standardError.Length > 0
                ? standardError
                : $"gio trash 返回 {process.ExitCode}";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool _CanUseGio()
    {
        return OperatingSystem.IsLinux() && CommandLocator.ExistsOnPath("gio");
    }

    private static bool _TryResolveTrash(
        string fullPath,
        out string trashRoot,
        out string? topDir)
    {
        trashRoot = string.Empty;
        topDir = null;
        if (!_CanDelete(fullPath)) return false;

        var dataHome = XdgPaths.DataHome;
        var pathRoot = _FindMountPoint(fullPath);
        var dataRoot = _FindMountPoint(dataHome);
        if (pathRoot is not null && string.Equals(pathRoot, dataRoot, StringComparison.Ordinal))
        {
            var homeTrash = Path.Combine(dataHome, "Trash");
            if (_CanWriteDirectoryOrParent(homeTrash))
            {
                trashRoot = homeTrash;
                return true;
            }
        }

        if (pathRoot is null) return false;
        if (!_TryGetTopDirTrash(pathRoot, out var topTrash)) return false;

        trashRoot = topTrash;
        topDir = pathRoot;
        return true;
    }

    private static RecycleBinOutcome _MoveToTrash(
        string fullPath,
        string trashRoot,
        string? topDir,
        out string? error)
    {
        error = null;
        try
        {
            var filesDir = Path.Combine(trashRoot, "files");
            var infoDir = Path.Combine(trashRoot, "info");
            _CreatePrivateDirectory(trashRoot);
            _CreatePrivateDirectory(filesDir);
            _CreatePrivateDirectory(infoDir);

            var baseName = Path.GetFileName(
                Path.TrimEndingDirectorySeparator(fullPath));
            if (string.IsNullOrEmpty(baseName)) baseName = "item";

            var trashPath = topDir is null
                ? _EncodeAbsolutePath(fullPath)
                : _EncodeRelativePath(topDir, fullPath);

            for (var index = 0; index < 10000; index++)
            {
                var name = index == 0 ? baseName : $"{baseName}.{index + 1}";
                var infoPath = Path.Combine(infoDir, $"{name}.trashinfo");
                if (_Exists(infoPath)) continue;

                var destination = Path.Combine(filesDir, name);
                if (_Exists(destination)) continue;

                try
                {
                    _WriteTrashInfo(infoPath, trashPath);
                }
                catch (IOException) when (File.Exists(infoPath))
                {
                    continue;
                }
                catch
                {
                    _TryDelete(infoPath);
                    throw;
                }

                try
                {
                    if (Directory.Exists(fullPath))
                        Directory.Move(fullPath, destination);
                    else
                        File.Move(fullPath, destination);
                    return RecycleBinOutcome.Moved;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    _TryDelete(infoPath);
                    return RecycleBinOutcome.Failed;
                }
            }

            error = "无法生成唯一的回收站条目名称";
            return RecycleBinOutcome.Failed;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return RecycleBinOutcome.Failed;
        }
    }

    private static void _WriteTrashInfo(string path, string trashPath)
    {
        using var stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
            });
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.WriteLine("[Trash Info]");
        writer.Write("Path=");
        writer.WriteLine(trashPath);
        writer.Write("DeletionDate=");
        writer.WriteLine(
            DateTime.Now.ToString(
                "yyyy-MM-ddTHH:mm:ss",
                CultureInfo.InvariantCulture));
    }

    private static void _CreatePrivateDirectory(string path)
    {
        Directory.CreateDirectory(path);
        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // 文件系统不支持 Unix 权限时保持默认权限。
        }
    }

    private static bool _IsValidSharedTrash(string path)
    {
        try
        {
            if (!Directory.Exists(path) || _IsSymlink(path)) return false;
            return File.GetUnixFileMode(path).HasFlag(UnixFileMode.StickyBit);
        }
        catch
        {
            return false;
        }
    }

    private static bool _CanWriteDirectoryOrParent(string path)
    {
        var current = path;
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.Exists(current)) return Directories.CanWrite(current);
            current = Path.GetDirectoryName(current);
        }

        return false;
    }

    private static bool _CanDelete(string path)
    {
        var parent = Path.GetDirectoryName(
            Path.TrimEndingDirectorySeparator(path));
        return !string.IsNullOrEmpty(parent) && Directories.CanWrite(parent);
    }

    private static string? _FindMountPoint(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            string? best = null;
            foreach (var drive in DriveInfo.GetDrives())
            {
                string name;
                try
                {
                    name = Path.TrimEndingDirectorySeparator(drive.Name);
                }
                catch
                {
                    continue;
                }

                if (name.Length == 0)
                    name = Path.DirectorySeparatorChar.ToString();
                if (!_IsUnder(full, name)) continue;
                if (best is null || name.Length > best.Length) best = name;
            }

            return best ?? Path.GetPathRoot(full);
        }
        catch
        {
            return null;
        }
    }

    private static bool _IsUnder(string path, string root)
    {
        if (string.Equals(path, root, StringComparison.Ordinal)) return true;
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static bool _IsSymlink(string path)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path)
                ? new DirectoryInfo(path)
                : new FileInfo(path);
            return info.LinkTarget is not null;
        }
        catch
        {
            return false;
        }
    }

    private static bool _Exists(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            return File.Exists(path) || Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static string _EncodeAbsolutePath(string path)
    {
        return string.Join(
            '/',
            path.Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString));
    }

    private static string _EncodeRelativePath(string topDir, string fullPath)
    {
        var relative = Path.GetRelativePath(topDir, fullPath);
        return string.Join(
            '/',
            relative.Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString));
    }

    private static void _TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 清理失败不改变删除结果。
        }
    }

    private static uint _GetUserId()
    {
        return geteuid();
    }

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint geteuid();
}
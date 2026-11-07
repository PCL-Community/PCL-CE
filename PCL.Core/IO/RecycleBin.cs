using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PCL.Core.IO;

/// <summary>
///     系统回收站。
///     把文件或目录移入操作系统回收站；无法提供回收站的平台或卷会明确返回 <see cref="Outcome.Unsupported" />，
///     由调用方决定是否改走永久删除并给出与事实一致的提示，避免出现“实际永久删除却提示已放入回收站”。
/// </summary>
public static class RecycleBin
{
    /// <summary>移入回收站的结果。</summary>
    public enum Outcome
    {
        /// <summary>已移入回收站，可从系统回收站还原。</summary>
        Moved,

        /// <summary>当前平台或该路径所在卷没有可用的回收站，调用方应改走永久删除并明确告知用户。</summary>
        Unsupported,

        /// <summary>移入回收站失败，原因见错误信息。</summary>
        Failed
    }

    public static bool IsSupported(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && (File.Exists(path) || Directory.Exists(path))
            && (OperatingSystem.IsWindows()
                || OperatingSystem.IsMacOS()
                || (OperatingSystem.IsLinux()
                    &&
                    // freedesktop 规范的主目录回收站只覆盖与主目录同卷的路径；
                    // 其他卷需要按 topdir 建 .Trash-<uid>，这里不做，交给调用方按永久删除处理。
                    _IsUnderHome(path)));
    }

    public static Outcome Move(string? path, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "路径为空";
            return Outcome.Failed;
        }

        var isDirectory = Directory.Exists(path);
        if (!isDirectory && !File.Exists(path))
        {
            error = "路径不存在";
            return Outcome.Failed;
        }

        if (!IsSupported(path)) return Outcome.Unsupported;

        try
        {
            if (OperatingSystem.IsWindows())
                return _MoveWindows(path, out error) ? Outcome.Moved : Outcome.Failed;
            if (OperatingSystem.IsMacOS())
                return _MoveMac(path, isDirectory, out error) ? Outcome.Moved : Outcome.Failed;
            return _MoveFreeDesktop(path, isDirectory, out error) ? Outcome.Moved : Outcome.Failed;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return Outcome.Failed;
        }
    }

    private static bool _IsUnderHome(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            return false;

        var full = Path.GetFullPath(path);
        if (full.Length == home.Length)
            return string.Equals(full, home, StringComparison.Ordinal);

        return full.StartsWith(
            home.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.Ordinal);
    }

    #region macOS

    private static bool _MoveMac(string path, bool isDirectory, out string? error)
    {
        error = null;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var trash = Path.Combine(home, ".Trash");
        Directory.CreateDirectory(trash);
        var destination = _UniqueDestination(
            trash,
            Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)));
        try
        {
            if (isDirectory) Directory.Move(path, destination);
            else File.Move(path, destination);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    #endregion

    #region Linux / freedesktop.org

    private static bool _MoveFreeDesktop(string path, bool isDirectory, out string? error)
    {
        error = null;
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(dataHome))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            dataHome = Path.Combine(userProfile, ".local", "share");
        }

        var trashRoot = Path.Combine(dataHome, "Trash");
        var filesDir = Path.Combine(trashRoot, "files");
        var infoDir = Path.Combine(trashRoot, "info");
        Directory.CreateDirectory(filesDir);
        Directory.CreateDirectory(infoDir);

        var full = Path.GetFullPath(path);
        var name = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(name)) name = "item";
        var destination = _UniqueDestination(filesDir, name);
        var destinationName = Path.GetFileName(destination);

        try
        {
            if (isDirectory) Directory.Move(full, destination);
            else File.Move(full, destination);
        }
        catch (IOException ex)
        {
            error = ex.Message;
            return false;
        }

        var infoPath = Path.Combine(infoDir, $"{destinationName}.trashinfo");
        try
        {
            var content = new StringBuilder()
                .AppendLine("[Trash Info]")
                .Append("Path=")
                .AppendLine(Uri.EscapeDataString(full).Replace("%2F", "/"))
                .Append("DeletionDate=")
                .AppendLine(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"))
                .ToString();
            File.WriteAllText(infoPath, content, new UTF8Encoding(false));
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _TryRollback(destination, full, isDirectory);
            try
            {
                if (File.Exists(infoPath)) File.Delete(infoPath);
            }
            catch
            {
                // 回滚时的清理失败不影响主流程结论。
            }

            return false;
        }
    }

    private static string _UniqueDestination(string directory, string name)
    {
        var candidate = Path.Combine(directory, name);
        var index = 2;
        while (File.Exists(candidate) || Directory.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{name}.{index}");
            index++;
        }

        return candidate;
    }

    private static void _TryRollback(string movedPath, string originalPath, bool isDirectory)
    {
        try
        {
            if (isDirectory) Directory.Move(movedPath, originalPath);
            else File.Move(movedPath, originalPath);
        }
        catch
        {
            // 回滚失败时保持回收站内文件不动，避免二次丢失。
        }
    }

    #endregion

    #region Windows

    private const uint FoDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [SuppressMessage(
        "Interoperability",
        "SYSLIB1054:使用 LibraryImport 生成 P/Invoke",
        Justification = "需要双 NUL 结尾的字符串，DllImport 的行为已按要求固定。")]
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    private static extern int SHFileOperation(ref ShFileOpStruct fileOp);

    private static bool _MoveWindows(string path, out string? error)
    {
        error = null;
        var operation = new ShFileOpStruct
        {
            hwnd = IntPtr.Zero,
            wFunc = FoDelete,
            pFrom = $"{path}\0",
            pTo = null,
            fFlags = FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi,
            fAnyOperationsAborted = 0,
            hNameMappings = IntPtr.Zero,
            lpszProgressTitle = null
        };

        var result = SHFileOperation(ref operation);
        if (result == 0 && operation.fAnyOperationsAborted == 0) return true;
        error = $"SHFileOperation 返回 {result}";
        return false;
    }

    #endregion
}
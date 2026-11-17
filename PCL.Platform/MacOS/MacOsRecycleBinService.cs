using System.Runtime.InteropServices;
using PCL.Core.Platform;
using PlainToolkit.ObjectiveC.Runtime;

namespace PCL.Platform;

public sealed class MacOsRecycleBinService : IRecycleBinService
{
    private static readonly ObjCSel DefaultManager =
        ObjCSel.FromString("defaultManager");

    private static readonly ObjCSel FileUrlWithPath =
        ObjCSel.FromString("fileURLWithPath:");

    private static readonly ObjCSel TrashItemAtUrl =
        ObjCSel.FromString("trashItemAtURL:resultingItemURL:error:");

    private static readonly ObjCSel Release = ObjCSel.FromString("release");
    private static bool _frameworksLoaded;

    public bool IsSupported(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path));
    }

    public RecycleBinOutcome Move(string? path, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            error = "路径不存在";
            return RecycleBinOutcome.Failed;
        }

        try
        {
            if (TryTrashItem(path))
                return RecycleBinOutcome.Moved;
        }
        catch
        {
            // 原生废纸篓不可用时回退到旧移动逻辑
        }

        var isDirectory = Directory.Exists(path);
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
            return RecycleBinOutcome.Moved;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return RecycleBinOutcome.Failed;
        }
    }

    private static bool TryTrashItem(string path)
    {
        EnsureFrameworks();

        ObjCString? pathString = null;
        try
        {
            pathString = ObjCString.FromString(path);
            var url = ObjCObject
                .GetClass("NSURL")
                .SendMessage<ObjCString, ObjCObject>(FileUrlWithPath, pathString.Value);
            if (url.IsNull) return false;

            var fileManager = ObjCObject
                .GetClass("NSFileManager")
                .SendMessage<ObjCObject>(DefaultManager);
            if (fileManager.IsNull) return false;

            return fileManager.SendMessage<ObjCObject, nint, nint, bool>(
                TrashItemAtUrl,
                url,
                0,
                0);
        }
        finally
        {
            if (pathString is { } pathValue) ReleaseString(pathValue);
        }
    }

    private static void EnsureFrameworks()
    {
        if (_frameworksLoaded) return;

        NativeLibrary.Load(
            "/System/Library/Frameworks/Foundation.framework/Foundation");
        NativeLibrary.Load(
            "/System/Library/Frameworks/AppKit.framework/AppKit");
        _frameworksLoaded = true;
    }

    private static void ReleaseString(ObjCString value)
    {
        if (!value.IsNull)
            new ObjCObject(value.Value).SendMessage(Release);
    }

    private static string _UniqueDestination(string directory, string name)
    {
        var candidate = Path.Combine(directory, name);
        var index = 2;
        while (File.Exists(candidate) || Directory.Exists(candidate))
            candidate = Path.Combine(directory, $"{name}.{index++}");
        return candidate;
    }
}
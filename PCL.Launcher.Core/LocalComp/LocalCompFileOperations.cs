using PCL.Core.Platform;

namespace PCL;

/// <summary>本地资源/数据包页共用的文件操作。页面只保留自身特有的展示与流程。</summary>
public static class LocalCompFileOperations
{
    public static string GetEntryPath(LocalComp.LocalCompFile entry)
    {
        return entry.IsFolder ? entry.ActualPath : entry.path;
    }

    public static string CsvEscape(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',')
            || value.Contains('"')
            || value.Contains('\r')
            || value.Contains('\n')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
    }

    public static long GetSafeSize(LocalComp.LocalCompFile entry)
    {
        try
        {
            return entry.IsFolder ? 0L : new FileInfo(GetEntryPath(entry)).Length;
        }
        catch
        {
            return 0L;
        }
    }

    public static DateTime GetSafeWriteTime(LocalComp.LocalCompFile entry)
    {
        try
        {
            return entry.IsFolder
                ? Directory.GetLastWriteTime(GetEntryPath(entry))
                : File.GetLastWriteTime(GetEntryPath(entry));
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    public static void DeleteFileToRecycleBin(string path)
    {
        if (!File.Exists(path))
            return;
        if (ApplicationRuntime.Current.Platform.RecycleBin.IsSupported(path)
            && ApplicationRuntime.Current.Platform.RecycleBin.Move(path, out _)
            == RecycleBinOutcome.Moved)
            return;
        File.Delete(path);
    }

    public static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // 删除失败不应阻断页面刷新。
        }
    }

    public static IReadOnlyList<string> GetSelectedShareIds(
        IEnumerable<LocalComp.LocalCompFile> entries,
        IReadOnlyCollection<string> selected,
        Func<LocalComp.LocalCompFile, string> keySelector)
    {
        return
        [
            .. entries
                .Where(entry => selected.Contains(keySelector(entry)) && entry.Comp is not null)
                .Select(entry => entry.Comp!.Id)
        ];
    }

    public static List<Comp.CompProject> GetSelectedProjects(
        IEnumerable<LocalComp.LocalCompFile> entries,
        IReadOnlyCollection<string> selected,
        Func<LocalComp.LocalCompFile, string> keySelector)
    {
        return
        [
            .. entries
                .Where(entry => selected.Contains(keySelector(entry)) && entry.Comp is not null)
                .Select(entry => entry.Comp!)
        ];
    }
}
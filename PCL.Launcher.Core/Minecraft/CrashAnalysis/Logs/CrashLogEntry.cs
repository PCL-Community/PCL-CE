namespace PCL;

internal sealed class CrashLogEntry(
    string fullPath,
    IReadOnlyList<string> lines)
{
    public string FullPath { get; } = fullPath;

    public IReadOnlyList<string> Lines { get; } = lines;

    public string FileName => Path.GetFileName(FullPath);

    /// <summary>
    /// 按任意换行符（CRLF、LF、CR）拆分文本。
    /// </summary>
    public static string[] SplitLines(string text)
    {
        return text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
    }
}
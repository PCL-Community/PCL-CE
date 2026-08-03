using System.IO;

namespace PCL;

internal sealed class CrashLogEntry(
    string fullPath,
    IReadOnlyList<string> lines)
{
    public string FullPath { get; } = fullPath;

    public IReadOnlyList<string> Lines { get; } = lines;

    public string FileName => Path.GetFileName(FullPath);
}
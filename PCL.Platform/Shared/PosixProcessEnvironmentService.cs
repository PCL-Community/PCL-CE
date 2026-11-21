using System.Diagnostics;
using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>Unix 环境变量名为 PATH。</summary>
public sealed class PosixProcessEnvironmentService : IProcessEnvironmentService
{
    public void PrependPathVariable(ProcessStartInfo startInfo, string path)
    {
        var separator = Path.PathSeparator;
        startInfo.Environment.TryGetValue("PATH", out var currentPath);
        var paths = new List<string>((currentPath ?? string.Empty).Split(separator))
        {
            path
        };
        startInfo.Environment["PATH"] = string.Join(separator, paths.Distinct());
    }
}
using System.Diagnostics;
using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>Windows 环境变量名为 Path。</summary>
public sealed class WindowsProcessEnvironmentService : IProcessEnvironmentService
{
    public void PrependPathVariable(ProcessStartInfo startInfo, string path)
    {
        var separator = Path.PathSeparator;
        var paths = new List<string>(
            (startInfo.EnvironmentVariables["Path"] ?? string.Empty).Split(separator))
        {
            path
        };
        startInfo.EnvironmentVariables["Path"] =
            string.Join(separator, paths.Distinct());
    }
}
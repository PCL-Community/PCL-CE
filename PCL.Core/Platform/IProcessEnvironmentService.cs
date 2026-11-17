using System.Diagnostics;

namespace PCL.Core.Platform;

/// <summary>平台相关的进程环境变量处理。</summary>
public interface IProcessEnvironmentService
{
    void PrependPathVariable(ProcessStartInfo startInfo, string path);
}
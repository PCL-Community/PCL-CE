using System.IO;
using System.Threading.Tasks;

namespace PCL.Core.Net.NDownload;

/// <summary>
/// 下载写入器。
/// </summary>
public interface IDlWriter
{
    /// <summary>
    /// 是否支持并行写入，即是否支持多次调用 <see cref="CreateStreamAsync"/>。
    /// </summary>
    public bool IsSupportParallel { get; }

    /// <summary>
    /// 创建写入流。
    /// </summary>
    /// <returns>写入流</returns>
    public Task<Stream> CreateStreamAsync();

    /// <summary>
    /// 停止写入并释放资源。
    /// </summary>
    public Task StopAsync();

    /// <summary>
    /// 完成写入，用于执行某些并行操作的收尾工作 (例如合并文件)。
    /// </summary>
    public Task FinishAsync();
}

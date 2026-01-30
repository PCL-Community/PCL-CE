using System.Threading.Tasks;

namespace PCL.Core.Net.NDownload;

/// <summary>
/// 下载连接，负责与服务器进行通信。
/// </summary>
public interface IDlConnection
{
    /// <summary>
    /// 开始连接，发起与服务器的通信。
    /// </summary>
    /// <param name="beginOffset">起始偏移，为 <c>0</c> 表示不使用分块</param>
    /// <returns>连接信息</returns>
    public Task<NDlConnectionInfo> StartAsync(long beginOffset);

    /// <summary>
    /// 停止连接，同时停止服务器通信并释放资源。
    /// </summary>
    /// <returns></returns>
    public Task StopAsync();

    /// <summary>
    /// 读取指定长度的数据，若无法继续读取则返回空数组。
    /// </summary>
    /// <param name="length">读取长度</param>
    /// <returns>字节数组形式的数据</returns>
    public Task<byte[]> ReadAsync(int length);
}

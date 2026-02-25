namespace PCL.Core.IO.Download.Core;

/// <summary>
/// 下载器状态
/// </summary>
public enum DownloadState
{
    /// <summary>
    /// 正在准备
    /// </summary>
    Preparing,

    /// <summary>
    /// 正在获取文件信息
    /// /// </summary>
    Probing,

    /// <summary>
    /// 等待有可用的空闲工作器
    /// </summary>
    Waiting,

    /// <summary>
    /// 下载中
    /// </summary>
    Downloading,

    /// <summary>
    /// 下载完成
    /// </summary>
    Completed,

    /// <summary>
    /// 下载失败
    /// </summary>
    Failed,

    /// <summary>
    /// 下载已被取消
    /// </summary>
    Canceled
}
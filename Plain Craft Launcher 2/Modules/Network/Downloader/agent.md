# Modules/Network/Downloader

下载器实现，命名空间 `PCL.Network`。在 `Requester`/`ModNet` 之上提供文件与资源的实际下载能力（支持分片与自适应）。

## 本目录直接文件

- `FileDownloader.cs`：静态文件下载工具（默认 30s 超时、最多 3 次重试）。
- `DownloadResourceManager.cs`：下载资源管理（调度与生命周期）。
- `DownloadRequestFactory.cs`：构造下载请求（`DownloadFile`/请求参数）。
- `AdaptiveRangeDownloader.cs`：自适应分片（Range）下载器，按网络情况调整并发分片。

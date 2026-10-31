# Modules/Network/Loaders

下载加载器任务，命名空间 `PCL.Network.Loaders`，基于 `ModLoader.LoaderBase` 实现可并行/可取消的下载任务。

## 本目录直接文件

- `LoaderDownload.cs`：下载加载器。管理 `SafeList<DownloadFile>` 的多文件并发下载、进度（`Progress`）、失败计数与取消（`CancellationTokenSource`）。
- `LoaderDownloadUnc.cs`：针对 UNC / 特殊路径的下载加载器实现。

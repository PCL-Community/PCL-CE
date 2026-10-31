# Modules/Network/Management

网络任务管理，命名空间 `PCL.Network`。

## 本目录直接文件

- `NetManager.cs`：单例（`Instance`）网络任务管理器。维护进行中的下载文件集合 `Files`（带 `LockFiles`）与下载任务列表 `Tasks`（`ModBase.SafeList<LoaderDownload>`，带 `LockRemain`），并提供剩余文件/任务计数。

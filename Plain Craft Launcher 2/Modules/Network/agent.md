# Modules/Network

网络层容器（本目录无直接 .cs，逻辑由子目录组织），命名空间 `PCL.Network`。统一封装 HTTP 请求、下载、加载器任务与连接管理，依赖 `PCL.Core.IO.Net.Http` 等底层能力。

## 子目录导航

- [Facade](./Facade/agent.md)：网络门面与全局参数（ModNet）。
- [Http](./Http/agent.md)：HTTP 请求封装（Requester、请求签名）。
- [Loaders](./Loaders/agent.md)：下载加载器任务（LoaderDownload 等）。
- [Management](./Management/agent.md)：网络任务管理（NetManager）。
- [Models](./Models/agent.md)：网络数据模型（下载文件、请求参数等）。
- [Downloader](./Downloader/agent.md)：下载器实现（分片/自适应下载）。

# IO/Download 目录

下载抽象层，位于命名空间 `PCL.Core.IO.Download`。定义与具体实现无关的下载连接、资源映射与写入器接口，并描述连接信息与来源报告，便于上层（资源项目、更新等）以统一方式触发下载。

## 本目录模块

- **IDlConnection.cs**：下载连接接口。
- **IDlResourceMapping.cs**：下载资源映射接口（URL/本地路径映射）。
- **IDlWriter.cs**：下载写入器接口（将流写入目标）。
- **NDlConnectionInfo.cs**：下载连接信息模型。
- **NDlFactory.cs**：下载工厂（创建连接）。
- **NDlSourceReport.cs**：下载来源报告（来源可用性/速度等）。

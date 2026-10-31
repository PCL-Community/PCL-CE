# IO/Net 目录

网络基础层，位于命名空间 `PCL.Core.IO.Net`。提供网络状态/接口辅助、Socket 扩展与 HTTP/DNS/端口转发子能力的统一入口。

## 本目录模块

- **`NetworkService.cs`** — 网络服务 `NetworkService`（标注 `[LifecycleService]`，随应用生命周期启动）：`GetClient` 获取共享 `HttpClient`，`GetRetryPolicy` 获取重试策略，聚合网络状态与连接能力。
- **`NetworkHelper.cs`** — 网络辅助 `NetworkHelper`：`IsNetworkAvailable` 检测联网状态，`NewTcpPort` 申请一个可用 TCP 端口。
- **`NetworkInterfaceUtils.cs`** — 网络接口工具 `NetworkInterfaceUtils`：`GetAvailableInterface` 选择可用网卡，`GetIPv6Status` 返回 `IPv6Status`（IPv6 可用性判定）。
- **`SocketExtension.cs`** — Socket 扩展 `SocketExtensions`：`CloseGracefully` 优雅关闭 Socket（先关闭发送/接收再释放，减少 RST）。

## 直接代码要点

- **运行流程**：`NetworkService` 随生命周期初始化，向下载/HTTP 子系统提供共享客户端与重试策略；连接前可用 `NetworkHelper.IsNetworkAvailable` 与 `NetworkInterfaceUtils` 预判网络与 IPv6 状态；Socket 使用完毕后走 `CloseGracefully`。
- **输入输出规范**：`NewTcpPort` 返回可用端口号；`GetIPv6Status` 返回枚举/状态对象；`GetClient` 返回可复用的 `HttpClient`。
- **使用约束与注意事项**：`HttpClient` 应复用而非频繁新建；`NetworkService` 的生命周期由 IoC 管理，勿手动重复初始化；IPv6 检测结果受系统与网卡影响；Socket 必须成对关闭/释放。

## 子目录导航

- [Dns](./Dns/agent.md)：DNS 查询与竞速。
- [Http](./Http/agent.md)：HTTP 客户端扩展与本地服务器。
- [SocketForward](./SocketForward/agent.md)：TCP 端口转发。

# IO/Net/SocketForward 目录

TCP 端口转发子系统，位于命名空间 `PCL.Core.IO.Net.SocketForward`。提供本地/远程 TCP 流量的转发能力（用于联机穿透等场景）。

## 本目录模块

- **TcpForwardBuilder.cs**：TCP 转发构建器（链式配置并启动）。
- **TcpForwardConfig.cs**：转发配置（监听端口、目标地址等）。
- **TcpForwardWorker.cs**：转发工作线程（实际搬运流量）。

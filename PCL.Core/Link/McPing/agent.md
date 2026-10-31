# Link/McPing 目录

Minecraft 服务器 ping 探测子系统，位于命名空间 `PCL.Core.Link.McPing`。实现对 MC 服务器（含旧版协议）的状态/信息探测。

## 本目录模块

- **`IMcPingService.cs`** — ping 服务接口 `IMcPingService`：定义 `PingAsync` 等探测契约。
- **`McPingService.cs`** — 标准协议实现 `McPingService`：`PingAsync` 按现代协议握手并读取状态，`Dispose` 释放连接。
- **`LegacyMcPingService.cs`** — 旧版协议实现 `LegacyMcPingService`：`PingAsync` 使用 Legacy 握手/查询，`Dispose` 释放。
- **`McPingServiceFactory.cs`** — 服务工厂 `McPingServiceFactory`：`CreateService` 创建标准实现、`CreateLegacyService` 创建旧版实现，按目标协议选择。

## 直接代码要点

- **运行流程**：调用方经 `McPingServiceFactory` 选择实现 → `PingAsync` 建立 TCP 连接并完成握手 → 读取服务器状态（版本、玩家数、MOTD 等）→ 返回结果模型（见 `Model`）→ `Dispose` 释放。
- **输入输出规范**：输入为服务器主机/端口；输出为 ping 结果模型；异步方法支持取消/超时。
- **使用约束与注意事项**：现代与旧版协议不通用，需按服务器版本选择；网络不可达/超时应返回失败而非崩溃；`Dispose` 后不可复用；结果字段随协议版本可能缺失。

## 子目录导航

- [Model](./Model/agent.md)：ping 结果数据模型。

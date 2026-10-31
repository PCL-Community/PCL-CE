# Link/Scaffolding/Client 目录

脚手架客户端，位于命名空间 `PCL.Core.Link.Scaffolding.Client`。实现联机大厅的客户端连接与握手。

## 本目录模块

- **`ScaffoldingClient.cs`** — 脚手架客户端 `ScaffoldingClient`：`ConnectAsync` 建立连接并完成握手，`SendRequestAsync` 发送请求并等待响应，`DisposeAsync` 异步释放；内部以 `ClientState` 状态机约束调用顺序。

## 直接代码要点

- **运行流程**：`ConnectAsync`（状态 Handshaking→已连接）→ `SendRequestAsync` 按请求类型经 `Framing` 写帧、读响应并反序列化 → `DisposeAsync` 关闭连接并释放资源；状态迁移由 `ClientState` 约束。
- **输入输出规范**：请求为 `Requests` 中的请求对象（带 `RequestType`），响应为对应类型；异步方法支持 `CancellationToken`。
- **使用约束与注意事项**：必须先 `ConnectAsync` 成功后才能 `SendRequestAsync`（否则状态机拒绝）；请求/响应需串行化，避免并发写帧交错；`DisposeAsync` 后不可复用；网络异常需上层处理。

## 子目录导航

- [Abstractions](./Abstractions/agent.md)：请求/响应抽象。
- [Framing](./Framing/agent.md)：协议帧读写。
- [Models](./Models/agent.md)：大厅与玩家模型。
- [Requests](./Requests/agent.md)：具体请求消息。

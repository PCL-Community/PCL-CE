# Link/Scaffolding 目录

连接脚手架（Scaffolding）子系统，位于命名空间 `PCL.Core.Link.Scaffolding`。为联机大厅客户端与服务端之间提供握手协议、邀请码、玩家列表管理与工厂。底层消息的帧封装见于 `Client/Framing`，请求/响应处理见于 `Client` 与 `Server`。

## 本目录模块

- **`ScaffoldingFactory.cs`** — 脚手架工厂 `ScaffoldingFactory`：`CreateServer` 创建服务端，并定义 `ScaffoldingClientEntity` / `ScaffoldingServerEntity` 两类实体，统一客户端/服务端的装配入口。
- **`LobbyCodeGenerator.cs`** — 大厅邀请码生成器 `LobbyCodeGenerator`：`Generate` 生成邀请码、`TryParse` 解析邀请码（含格式校验）。
- **`PlayerListHandler.cs`** — 玩家列表处理 `PlayerListHandler`：`Sort` 对玩家列表排序（如按在线/名称），供 UI 与同步使用。

## 直接代码要点

- **运行流程**：`ScaffoldingFactory` 创建客户端/服务端实体 → 客户端经握手加入大厅（首个 `PlayerPingRequest`）→ 服务端跟踪玩家并经 `PlayerListHandler.Sort` 输出有序列表 → 邀请码由 `LobbyCodeGenerator` 生成/解析用于加入。
- **输入输出规范**：邀请码为字符串，`TryParse` 返回是否成功及解析结果；玩家列表为模型集合，排序返回有序集合。
- **使用约束与注意事项**：邀请码格式与版本需匹配；玩家列表并发修改时需加锁/快照；工厂创建的实体需按生命周期释放（见 `Client`/`Server`）。

## 子目录导航

- [Client](./Client/agent.md)：脚手架客户端与协议帧封装。
- [EasyTier](./EasyTier/agent.md)：脚手架内 EasyTier 相关实体与信息。
- [Exceptions](./Exceptions/agent.md)：脚手架异常。
- [Server](./Server/agent.md)：脚手架服务端。

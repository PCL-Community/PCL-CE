# Link/Scaffolding/Server/Abstractions 目录

脚手架服务端抽象，位于命名空间 `PCL.Core.Link.Scaffolding.Server.Abstractions`。本目录定义服务端处理管线的基础契约：请求处理器接口 `IRequestHandler` 与共享上下文 `IServerContext`，是 `Server/Handlers` 各具体处理器与 `ScaffoldingServer` 调度逻辑之间的桥梁。

## 本目录模块

- **IRequestHandler.cs**：请求处理器接口（处理某类请求并返回响应）。
- **IServerContext.cs**：服务端上下文接口（处理器访问连接/状态）。

## 运行流程

- `ScaffoldingServer` 在构造时实例化一组 `IRequestHandler` 并以 `RequestType` 为键存入字典 `_handlers`；`ScaffoldingServerContext` 实现 `IServerContext` 作为共享上下文。
- 当客户端帧到达 `_HandleClientAsync`：
  1. `_TryParseFrame` 解析出 `(TypeInfo, Body)`。
  2. 依 `TypeInfo` 从 `_handlers` 取出对应 `IRequestHandler`。
  3. 调用 `handler.HandleAsync(requestBody, _context, sessionId, ct)`，得到 `(Status, Body)`。
  4. 服务端把 `[1 字节 Status][4 字节大端 Body 长度][Body]` 写回并 `FlushAsync`。
- 各 Handler 通过 `IServerContext` 读写玩家列表、获取主机名/端口/房间信息、通知玩家列表变化（触发 `PlayerProfilesPing` 事件）。

## 输入输出规范

### IRequestHandler（IRequestHandler.cs）
- `public interface IRequestHandler`。
- 成员：
  - `string RequestType { get; }`：该处理器负责的请求类型，例如 `"c:ping"`。须与客户端 `IRequest.RequestType`、服务端 `_handlers` 字典键完全一致。
  - `Task<(byte Status, ReadOnlyMemory<byte> Body)> HandleAsync(ReadOnlyMemory<byte> requestBody, IServerContext context, string sessionId, CancellationToken ct)`：处理请求。返回元组：`Status` 为单字节状态码（`0` 成功；`32` 为协议定义错误，如参数缺失/未就绪；`255` 为未知错误且 `Body` 携带 UTF-8 错误信息）；`Body` 为响应体原始字节（可为空）。`sessionId` 为 `ScaffoldingServer` 为每个 TCP 连接生成的 `Guid` 字符串，主要用于日志追踪。

### IServerContext（IServerContext.cs）
- `public interface IServerContext`，由 `ScaffoldingServerContext` 实现。成员：
  - `IReadOnlyList<PlayerProfile> PlayerProfiles { get; }`：当前所有已登记玩家档案（含 HOST 与 GUEST）。
  - `ConcurrentDictionary<string, TrackedPlayerProfile> TrackedPlayers { get; }`：以 `machine_id` 为键的在线玩家跟踪表（含 `LastSeenUtc`）。
  - `void OnPlayerProfilesChanged()`：通知玩家列表发生变化（实现内会触发 `PlayerProfilesPing` 事件，异步 `Task.Run` 派发）。
  - `event Action<IReadOnlyList<PlayerProfile>> PlayerProfilesPing`：玩家档案变化事件。
  - `int MinecraftServerProt { get; }`：正在运行的 Minecraft 服务器端口（来自 `ScaffoldingServerContext` 构造参数）。
  - `LobbyInfo UserLobbyInfo { get; }`：房间信息（房间码/网络名/密钥）。
  - `string PlayerName { get; }`：主机玩家名。

## 使用约束与注意事项

- 处理器应是无状态且线程安全的：`HandleAsync` 会被多个客户端连接并发调用（每连接一任务），所有可变共享状态都应通过 `IServerContext`（其内部 `TrackedPlayers` 为 `ConcurrentDictionary`）安全访问，不要在 Handler 内持有非线程安全的静态/实例可变字段。
- 状态码约定：成功一律返回 `0`；业务性失败返回 `32`（协议定义错误），例如 `PlayerPingHandler` 缺少 `machine_id`、`GetServerPortHandler` 端口未就绪（0）；`255` 预留为未知错误并可在 `Body` 写入 UTF-8 错误串（与服务端回写逻辑、客户端 `ScaffoldingRequestException` 配合）。新增 Handler 时建议沿用该约定。
- `sessionId` 仅用于日志与潜在按会话状态跟踪，当前框架未要求 Handler 按 session 维护状态；不要假设同一 `sessionId` 的多次请求之间有隐式顺序保证（每连接虽为顺序读帧，但不同连接并发）。
- `OnPlayerProfilesChanged()` 通过 `Task.Run` 异步派发事件，订阅者（`ScaffoldingServer.PlayerProfilePing`）回调中不应执行长时间阻塞操作，以免拖慢状态更新。
- `TrackedPlayers` 由 `PlayerPingHandler` 写入、`ScaffoldingServer._MonitorPlayerLivenessAsync` 清理（仅清 GUEST，超时 10s），Handler 直接操作该字典时应注意与清理任务的并发（用 `AddOrUpdate`/`TryRemove` 等原子操作）。
- 与 `Link` 其他子模块：本目录仅依赖 `Client.Models`（经 `Server` 层使用的 `PlayerProfile`/`LobbyInfo`/`TrackedPlayerProfile`）与标准库；EasyTier/Lobby 的具体耦合在 `ScaffoldingServerContext`/`ScaffoldingFactory`；McPing 未引用（依赖关系待确认）。

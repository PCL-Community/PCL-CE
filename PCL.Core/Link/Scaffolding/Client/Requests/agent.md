# Link/Scaffolding/Client/Requests 目录

脚手架客户端请求消息，位于命名空间 `PCL.Core.Link.Scaffolding.Client.Requests`。每个文件是一个 `IRequest<TResponse>` 的具体实现：定义请求类型标识（RequestType）、如何把请求体写入字节、以及如何把响应体解析为强类型结果。配合 `Client.Framing` 编解码、`ScaffoldingClient.SendRequestAsync` 发送。

## 本目录模块

- **PingRequest.cs**：Ping 请求（心跳/连通性）。
- **PlayerPingRequest.cs**：玩家 Ping 请求（单个玩家状态探活）。
- **GetPlayerProfileListRequest.cs**：获取玩家档案列表请求。
- **GetProtocolsRequest.cs**：获取支持协议列表请求。
- **GetServerProtRequest.cs**：获取服务端端口/协议信息请求（类型标识 `c:server_port`）。

## 运行流程

- 这些请求在 `ScaffoldingClient` 中被按需调用：
  - **握手/连接建立**：`ConnectAsync` 在置 `Handshaking` 状态后，首先发送一个 `PlayerPingRequest`（即"握手"的本质是向服务端登记本机玩家档案），成功后置 `Connected` 并启动心跳循环。
  - **心跳循环**：每 5 秒先发 `PlayerPingRequest`（探活+刷新 LastSeenUtc），再发 `GetPlayerProfileListRequest` 拉取全量玩家列表，触发 `Heartbeat` 事件。
  - **按需查询**：`GetProtocolsRequest`、`GetServerPortRequest` 由上层（`ScaffoldingFactory`/联机 UI）在建立连接后调用，用于协商协议能力、获取被转发的 Minecraft 服务器端口。
- 服务端对应 Handler（`Server/Handlers`）按 `RequestType` 分发：`c:ping→PingHandler`、`c:player_ping→PlayerPingHandler`、`c:player_profiles_list→GetPlayerProfileListHandler`、`c:protocols→GetProtocolsHandler`、`c:server_port→GetServerPortHandler`。

## 输入输出规范（请求类型对照表）

| 类 | RequestType | 请求体 | 响应类型 TResponse | 响应体解析 |
|---|---|---|---|---|
| `PingRequest` | `c:ping` | raw payload（字节） | `ReadOnlyMemory<byte>` | 原样回显 responseBody |
| `PlayerPingRequest` | `c:player_ping` | `PlayerProfile` JSON（snake_case，忽略 null，无 Kind） | `bool` | 响应体为空 → `true`（成功） |
| `GetPlayerProfileListRequest` | `c:player_profiles_list` | 空 | `IReadOnlyList<PlayerProfile>` | JSON 列表；null → 空集合 |
| `GetProtocolsRequest` | `c:protocols` | 客户端支持的协议串以 `\0` 连接（ASCII） | `IReadOnlyList<string>` | 以 `\0` 分隔（RemoveEmptyEntries）；空 → 空集合 |
| `GetServerPortRequest` | `c:server_port` | 空 | `ushort` | 2 字节大端 `UInt16`；长度≠2 → 抛 `InvalidOperationException` |

### 各请求要点
- **PingRequest**：`public sealed class PingRequest : IRequest<ReadOnlyMemory<byte>>`，构造 `PingRequest(ReadOnlyMemory<byte> payload)`；若 `payload.Length >= 32` 抛 `ArgumentException("Payload must be less than 32 bytes.")`。`WriteRequestBody` 直接写 payload；`ParseResponseBody` 返回 `responseBody`（服务端 `PingHandler` 原样回显）。
- **PlayerPingRequest**：`public sealed class PlayerPingRequest(string name, string machineId, string vendor) : IRequest<bool>`（主构造器）。内部构造 `PlayerProfile{Name,MachineId,Vendor}`（无 Kind）。使用 `_JsonOptions`（`JsonCompat.SerializerOptions` + `SnakeCaseLower` + `WhenWritingNull`）经 `Utf8JsonWriter` 序列化。`ParseResponseBody`：空体 → `true`。
- **GetPlayerProfileListRequest**：`public sealed class GetPlayerProfileListRequest : IRequest<IReadOnlyList<PlayerProfile>>`，请求体为空。`ParseResponseBody` 用同样的 `_JsonOptions` 反序列化；结果为 null 时返回 `ArraySegment<PlayerProfile>.Empty`。
- **GetProtocolsRequest**：`public sealed class GetProtocolsRequest(IEnumerable<string> supportProtocols) : IRequest<IReadOnlyList<string>>`。请求体：以 `\0` 连接后 ASCII 编码；常量分隔符 `_Separator = [(byte)'\0']`。响应体按 `\0` 分割（去空项）。
- **GetServerPortRequest**：`public sealed class GetServerPortRequest : IRequest<ushort>`，请求体为空。`ParseResponseBody`：若 `responseBody.Length != 2` 抛 `InvalidOperationException("Invalid response body for server port.")`，否则 `BinaryPrimitives.ReadUInt16BigEndian`。

## 使用约束与注意事项

- 类型标识必须与服务端 `_handlers` 字典键严格一致（目前硬编码于 `ScaffoldingServer` 构造函数与 `GetProtocolsHandler` 的协议清单中）。若新增请求类型，需**同时**更新 `ScaffoldingServer` 的 `_handlers` 注册与 `GetProtocolsHandler` 返回的清单，否则会出现"客户端可发但服务端无处理器"或"协议协商清单与实际不一致"。
- 序列化一致性：JSON 类请求（`PlayerPingRequest`、`GetPlayerProfileListRequest`）共用 `SnakeCaseLower + WhenWritingNull` 约定，且依赖 `Client.Models.PlayerProfile` 的 `[JsonPropertyName]`；修改任一端 JSON 结构需保证客户端/服务端同步。
- 状态前置：`ScaffoldingClient.SendRequestAsync` 要求 `_state >= Handshaking`，否则抛 `InvalidOperationException("Client is not connected.")`。因此这些请求只能在 `ConnectAsync` 之后（含握手）调用；不要在连接建立前直接 `new` 后发送。
- 重试/超时：本目录请求自身无超时逻辑，超时由调用方 `CancellationToken`（`SendRequestAsync` 透传）与 `ProtocolReader` 的等待行为共同决定。心跳失败会触发 `ServerShuttedDown` 并断开。
- 线程安全：请求实例（尤其被 `ScaffoldingClient` 复用的 `_playerPingRequest`）应为不可变；不要在并发调用间共享可写状态。
- 与 `Link` 其他子模块：本目录依赖 `Client.Abstractions`、`Client.Models`、`Utils`（JsonCompat）与 `System.Text.Json`；EasyTier/Lobby/McPing 不直接引用（依赖关系待确认）。实际建连/端口转发由 `ScaffoldingFactory` 借助 EasyTier 完成，本目录只负责"连上之后"的协议层交互。

# Link/Scaffolding/Client/Models 目录

脚手架客户端模型，位于命名空间 `PCL.Core.Link.Scaffolding.Client.Models`。本目录定义联机大厅、玩家档案等共享数据模型，被 `Client/Requests`（如 `PlayerPingRequest`、`GetPlayerProfileListRequest`）与 `Server/Abstractions`（`IServerContext`、各 `Handler`）共同引用，是客户端与服务端之间 JSON 交换的承载类型。

## 本目录模块

- **LobbyInfo.cs**：大厅信息模型。
- **LobbyType.cs**：大厅类型枚举。
- **PlayerKind.cs**：玩家种类枚举。
- **PlayerProfile.cs**：玩家档案模型（昵称、标识等）。

## 运行流程

- 这些模型是"玩家在线状态"与"房间标识"的载体：
  - `PlayerProfile` 由 `PlayerPingRequest`（客户端）序列化后上报，由 `PlayerPingHandler`（服务端）反序列化并写入 `TrackedPlayers`；`GetPlayerProfileListHandler` 再把全部 `PlayerProfile` 序列化返回给客户端，客户端经 `GetPlayerProfileListRequest.ParseResponseBody` 还原为列表并在 `Heartbeat` 事件中暴露。
  - `LobbyInfo` / `LobbyType` 由 `ScaffoldingFactory` 经 `LobbyCodeGenerator` 生成/解析，注入到 `ScaffoldingServerContext`（作为 `UserLobbyInfo`）并传给 EasyTier，是建立联机隧道所需的"房间坐标"。
- 数据流向：`PlayerPingRequest`（构造 `PlayerProfile`，不含 `Kind`）→ 网络 → `PlayerPingHandler`（反序列化 + 置 `Kind=GUEST` + 入表）→ `GetPlayerProfileListHandler`（读取 `PlayerProfiles`，含 `HOST`）→ 网络 → `GetPlayerProfileListRequest`（解析为 `IReadOnlyList<PlayerProfile>`）。

## 输入输出规范

### LobbyInfo（LobbyInfo.cs）
- `public record LobbyInfo(string FullCode, string NetworkName, string NetworkSecret)`。
- 三个字符串字段：完整房间码 `FullCode`、网络名 `NetworkName`、网络密钥 `NetworkSecret`。由 `LobbyCodeGenerator.Generate()/TryParse()` 生成与解析（逻辑在父目录 `Scaffolding/LobbyCodeGenerator.cs`）。

### LobbyType（LobbyType.cs）
- `public enum LobbyType { PCLCE, Terracotta, Scaffolding }`。
- 联机大厅来源/类型枚举，作为 `ScaffoldingFactory.CreateClientAsync(playerName, lobbyCode, LobbyType from, ...)` 的路由参数使用。

### PlayerKind（PlayerKind.cs）
- `public enum PlayerKind { HOST, GUEST }`，标记 `[JsonConverter(typeof(JsonStringEnumConverter))]` → 序列化/反序列化为字符串 `"HOST"`/`"GUEST"`（结合 `SnakeCaseLower` JSON 选项仍输出大写的枚举名）。
- 服务端据此区分主机与访客：`GetPlayerProfileListHandler` 构造 `HOST`，`PlayerPingHandler` 把上报玩家置为 `GUEST`；`ScaffoldingServer._MonitorPlayerLivenessAsync` 跳过 `HOST` 不清理。

### PlayerProfile（PlayerProfile.cs）
- `public record PlayerProfile`，JSON 字段映射（JsonPropertyName）：
  - `name`（string，必填）：玩家昵称。
  - `machine_id`（string，必填）：机器/启动器标识，取 `Utils.Secret.Identify.LauncherId`，是去重与跟踪的键（服务端 `TrackedPlayers` 的 key）。
  - `vendor`（string，必填）：客户端描述串，例如 `"PCL CE x.y, EasyTier z"`。
  - `kind`（PlayerKind?，可空，`JsonIgnore(WhenWritingNull)`）：玩家种类；客户端上报时通常省略（null），由服务端补全为 `GUEST`；主机档案由 `ScaffoldingServerContext`/`GetPlayerProfileListHandler` 显式设为 `HOST`。
- 序列化约定：配合 `JsonCompat.SerializerOptions` + `PropertyNamingPolicy = SnakeCaseLower`，字段名已显式 `[JsonPropertyName(...)]` 覆盖为 snake_case；`Kind` 为空时写出的 JSON 不包含该字段。

## 使用约束与注意事项

- `machine_id` 是玩家身份与在线跟踪的主键：服务端 `PlayerPingHandler` 明确要求非空，否则返回状态码 `32` 并忽略该请求；新增/修改玩家档案逻辑时必须保证该字段可靠填充。
- `PlayerProfile` 为 `record`：在 `PlayerPingHandler` 中通过 `profile with { Kind = PlayerKind.GUEST }` 做不可变拷贝生成访客档案，注意"with"会生成新实例，原 `profile` 仍保留上报字段（用于 `TrackedPlayerProfile.Profile` 覆盖更新）。
- JSON 端序/枚举：`PlayerKind` 走字符串枚举转换；JSON 默认 `WhenWritingNull` 忽略 `Kind`，服务端反序列化时 `Kind` 可能为 null，处理时须按"null 视为未分类/待补全"对待。
- `LobbyInfo` 含 `NetworkSecret` 等敏感信息（网络密钥），仅用于本机 EasyTier 隧道协商，不要落日志或外泄。
- 与 `Link` 其他子模块：`Models` 直接被 `Client.Requests`、`Server.Handlers` 与 `Server` 引用；`LobbyType`/`LobbyInfo` 与 `LobbyCodeGenerator`（同层）、EasyTier 绑定；McPing 子模块未引用（依赖关系待确认）。

# Link/Scaffolding/Server/Handlers 目录

脚手架服务端请求处理器，位于命名空间 `PCL.Core.Link.Scaffolding.Server.Handlers`。每个文件对应一类客户端请求（`RequestType`）的具体处理逻辑，均实现 `Server.Abstractions.IRequestHandler`，由 `ScaffoldingServer` 按 `RequestType` 字典分发。

## 本目录模块

- **PingHandler.cs**：Ping 请求处理（心跳应答）。
- **PlayerPingHandler.cs**：玩家 Ping 请求处理（登记/刷新在线玩家）。
- **GetPlayerProfileListHandler.cs**：获取玩家档案列表处理。
- **GetProtocolsHandler.cs**：获取协议列表处理。
- **GetServerPortHandler.cs**：获取服务端端口/协议信息处理（`RequestType` 为 `c:server_port`）。

## 运行流程

- 调度来自 `ScaffoldingServer._HandleClientAsync`：解析出帧后按 `TypeInfo` 查 `_handlers`，命中后调用对应 `HandleAsync(requestBody, _context, sessionId, ct)`，回写 `(Status, Body)`。
- 注册关系（在 `ScaffoldingServer` 构造函数中）：
  - `c:player_ping → PlayerPingHandler`
  - `c:server_port → GetServerPortHandler`
  - `c:player_profiles_list → GetPlayerProfileListHandler`
  - `c:protocols → GetProtocolsHandler`
  - `c:ping → PingHandler`
- 角色分工：
  - `PlayerPingHandler` 是"在线状态中枢"：客户端每次握手/心跳都发 `c:player_ping`，它据此新增或刷新 `TrackedPlayers` 的 `LastSeenUtc`，新玩家上线时调用 `context.OnPlayerProfilesChanged()`。
  - `GetPlayerProfileListHandler` 在收到 `c:player_profiles_list` 时，把 HOST 档案（由上下文构造）与 `context.PlayerProfiles`（全部在线玩家）序列化返回，是客户端刷新大厅成员列表的数据源。
  - `GetServerPortHandler` 透出 `context.MinecraftServerProt`（被 EasyTier 端口转发后的 Minecraft 端口）。
  - `GetProtocolsHandler` 回显服务端支持的全部请求类型清单。
  - `PingHandler` 简单地把请求体原样回显，用于连通性探活。

## 输入输出规范

### PingHandler（PingHandler.cs）
- `RequestType = "c:ping"`。`HandleAsync` 直接返回 `(0, requestBody)`——把收到的请求体原样作为响应体回显。无业务逻辑、无异常分支。

### PlayerPingHandler（PlayerPingHandler.cs）
- `RequestType = "c:player_ping"`。
- 流程：
  1. 用 `_JsonOptions`（`JsonCompat.SerializerOptions` + `SnakeCaseLower` + `WhenWritingNull`）反序列化 `PlayerProfile`；失败（`JsonException`）→ 记 Warn 日志并返回 `(32, Empty)`。
  2. 若 `profile` 为 null 或 `MachineId` 为空 → 记 Warn 并返回 `(32, Empty)`。
  3. 构造 `guestProfile = profile with { Kind = PlayerKind.GUEST }`。
  4. `context.TrackedPlayers.AddOrUpdate(machineId, ...)`：首次出现则新建 `TrackedPlayerProfile{Profile, LastSeenUtc=UtcNow}` 并置 `listChanged=true`；已存在则更新 `Profile` 与 `LastSeenUtc`。
  5. 若 `listChanged` → `context.OnPlayerProfilesChanged()`。
  6. 成功返回 `(0, Empty)`。
- 依赖：`Client.Models`（PlayerProfile/PlayerKind）、`Logging.LogWrapper`、`Utils.JsonCompat`。

### GetPlayerProfileListHandler（GetPlayerProfileListHandler.cs）
- `RequestType = "c:player_profiles_list"`。
- 构造 `hostProfile`：`Name = context.PlayerName`、`MachineId = Utils.Secret.Identify.LauncherId`、`Vendor = $"PCL CE {Basics.VersionName}, EasyTier {EasyTierMetadata.CurrentEasyTierVer}"`、`Kind = PlayerKind.HOST`。
- 注意：该 Handler 的响应体**仅包含 `context.PlayerProfiles`**（即 `TrackedPlayers` 中已有档案，其中 HOST 由 `ScaffoldingServerContext.Create` 预置），并不额外把 `hostProfile` 并入返回集合（hostProfile 字段与 `Create` 中预置的 HOST 档案相同）。返回 `JsonSerializer.SerializeToUtf8Bytes(allProfiles, _JsonOptions)`，`(0, body)`。
- 依赖：`EasyTier.EasyTierMetadata`、`App.Basics`、`Utils.Secret.Identify`。

### GetProtocolsHandler（GetProtocolsHandler.cs）
- `RequestType = "c:protocols"`。
- 硬编码返回协议数组：`"c:ping"`, `"c:protocols"`, `"c:server_port"`, `"c:player_ping"`, `"c:player_profiles_list"`（以 `\0` 连接、ASCII 编码）。该清单与服务端 `_handlers` 注册键应保持一致；**新增请求类型时需同步更新此处与客户端的 `GetProtocolsRequest` 期望**。
- 依赖：仅标准库 `System.Text.Encoding`。

### GetServerPortHandler（GetServerPortHandler.cs）
- `RequestType = "c:server_port"`。
- 读取 `context.MinecraftServerProt`；若为 `0`（未就绪）→ 返回 `(32, Empty)`。否则把端口转 `ushort`，用 `BinaryPrimitives.WriteUInt16BigEndian` 写入 2 字节大端，返回 `(0, portBytes)`。
- 依赖：`Buffers.Binary`。

## 使用约束与注意事项

- 线程安全：所有 Handler 经 `ScaffoldingServer` 被多连接并发调用，必须只通过 `IServerContext`（尤其 `TrackedPlayers` 这个 `ConcurrentDictionary`）访问共享状态；`PlayerPingHandler` 已用 `AddOrUpdate` 保证原子性。不要在 Handler 中引入非线程安全的静态可变字段。
- 状态码一致性：成功返回 `0`；业务失败返回 `32`（缺 `machine_id`/端口未就绪）。`255` 预留未知错误。客户端 `ScaffoldingRequestException` 会把 `32–63` 归类为 "Protocol-defined error"。不要随意返回其他非 0 值破坏该语义。
- `GetProtocolsHandler` 清单是"硬维护"的：若增减请求类型，必须同步 `ScaffoldingServer._handlers` 注册、`GetProtocolsHandler` 数组，以及客户端 `GetProtocolsRequest` 的解析预期，否则协商结果与实际处理能力不一致。
- 玩家档案 JSON 约定：涉及 `PlayerProfile` 的序列化/反序列化均使用 `SnakeCaseLower + WhenWritingNull`，且 `PlayerKind` 为字符串枚举；Handler 与客户端请求（`PlayerPingRequest`/`GetPlayerProfileListRequest`）须保持同一套 `_JsonOptions` 基配置（`JsonCompat.SerializerOptions`），否则会出现字段名/枚举不匹配。
- 依赖关系：本目录直接依赖 `Server.Abstractions`、`Client.Models`、`EasyTier`（仅 `GetPlayerProfileListHandler` 用 `EasyTierMetadata`）、`App.Basics`、`Utils`（JsonCompat/Secret）、`Logging`。与 Lobby（`LobbyInfo` 经 `IServerContext.UserLobbyInfo`）间接相关；McPing 子模块未引用（依赖关系待确认）。

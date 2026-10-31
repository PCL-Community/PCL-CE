# Link/Scaffolding/Client/Abstractions 目录

脚手架客户端抽象，位于命名空间 `PCL.Core.Link.Scaffolding.Client.Abstractions`。本目录定义了"客户端请求"与"服务端响应"的核心契约（接口与记录类型），是 `Client/Requests`、`Client/Framing` 与 `Server/Abstractions` 之间共享的基础类型。

## 本目录模块

- **IRequest.cs**：客户端请求接口（所有请求消息的契约）。
- **ScaffoldingResponse.cs**：脚手架响应模型（服务端回执）。

## 运行流程

- 本目录是整套 Scaffolding 客户端/服务端协议的"契约层"：
  - `IRequest<TResponse>` 由 `Client/Requests` 下各具体请求类实现，定义了请求的"类型标识、写请求体、解析响应体"三要素。
  - `ScaffoldingResponse`（状态 + 原始响应体）是 `Client/Framing/ProtocolReader` 解析后的统一结果载体，再由调用方交给具体请求的 `ParseResponseBody` 反序列化为强类型 `TResponse`。
- 调用顺序（以 `ScaffoldingClient.SendRequestAsync` 为例）：
  1. 调用方构造某个 `IRequest<TResponse>`（如 `PingRequest`）。
  2. `ProtocolWriter` 用 `RequestType` 与 `WriteRequestBody` 将其编码为字节帧发往服务端。
  3. `ProtocolReader` 从流中读出一个 `ScaffoldingResponse`。
  4. `ScaffoldingClient` 调用 `request.ParseResponseBody(response.Body)` 得到 `TResponse` 返回。
- 服务端侧对应使用 `Server/Abstractions/IRequestHandler` 与 `IServerContext`，二者与本目录的 `IRequest`/`ScaffoldingResponse` 形成对称的"请求契约 / 响应契约"。

## 输入输出规范

### IRequest<TResponse>（IRequest.cs）
- 泛型接口，类型参数 `TResponse` 为期望的响应强类型。
- 关键成员：
  - `string RequestType { get; }`：请求类型字符串，例如 `"c:ping"`、`"c:player_ping"`。它既是线上帧的"类型标识字段"，也用于服务端路由（`ScaffoldingServer._handlers` 的字典键）。
  - `void WriteRequestBody(IBufferWriter<byte> writer)`：将请求体写入 `IBufferWriter<byte>`（注意：不含类型长度/类型字符串/长度前缀等帧头，这些由 `ProtocolWriter` 负责）。
  - `TResponse ParseResponseBody(ReadOnlyMemory<byte> responseBody)`：解析服务端回执中的响应体（原始字节）；`responseBody` 已在 `ProtocolReader` 处校验过 `Status == 0`。
- 实现约定：具体请求类应为不可变/密封（`sealed`）且线程安全（无共享可变状态），因为 `ScaffoldingClient` 会复用同一实例（如心跳用的 `_playerPingRequest`）。

### ScaffoldingResponse（ScaffoldingResponse.cs）
- `public record ScaffoldingResponse(byte Status, ReadOnlyMemory<byte> Body)`。
- `Status`：单字节状态码，`0` 表示成功；非 `0` 视为错误（`ProtocolReader` 会据此抛出 `ScaffoldingRequestException`）。
- `Body`：`ReadOnlyMemory<byte>`，成功时为响应体原始字节；错误时（如 `Status == 255`）可能携带 UTF-8 编码的服务端错误信息。

## 使用约束与注意事项

- 类型标识约定：`RequestType` 使用 ASCII 编码写入帧，长度上限为 `byte.MaxValue`（255）字节，服务端解析侧进一步限制为 128 字节（`ScaffoldingServer._TryParseFrame` 的 `maxTypeLength`）。新增请求类型时务必保持简短且全局唯一，并与服务端 `_handlers` 字典键保持一致。
- 序列化策略分散在各具体请求中（见 `Client/Requests`）：JSON 类请求统一使用 `JsonCompat.SerializerOptions` 基配置 + `SnakeCaseLower` 命名策略 + `WhenWritingNull` 忽略；纯字节请求（如 `PingRequest`、`GetServerPortRequest`）使用大端（Big-Endian）定长。抽象层本身不规定序列化方式，仅提供 `IBufferWriter<byte>` 出口。
- 错误语义：`Status != 0` 的响应不会到达 `ParseResponseBody`，而是被 `ProtocolReader` 提前抛出 `ScaffoldingRequestException`（含 `StatusCode` 与 `ServerMessage`）。因此具体请求的 `ParseResponseBody` 可以假设拿到的 `responseBody` 是"成功态"数据，无需再做状态码判断。
- 本目录文件为纯契约定义，不含网络/TCP 逻辑，不直接依赖 EasyTier/Lobby，但 `PlayerPingRequest` 等具体请求会在 `Client/Requests` 层依赖 `Client.Models` 的 `PlayerProfile`。
- 与 `Link` 其他子模块的依赖关系：本目录只依赖标准库与自身 `Client.Models`（经上层引用）；EasyTier/Lobby 的耦合在 `ScaffoldingFactory` 与 `ScaffoldingClient` 构造参数中体现，本目录本身不直接引用。McPing 子模块未在本目录引用（依赖关系待确认）。

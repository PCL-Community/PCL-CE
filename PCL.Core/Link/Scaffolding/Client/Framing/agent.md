# Link/Scaffolding/Client/Framing 目录

脚手架协议帧封装，位于命名空间 `PCL.Core.Link.Scaffolding.Client.Framing`。负责将请求/响应按私有协议编码为字节帧并解析回来。本目录是客户端与服务端之间"线格式（wire format）"的实现，对称地，服务端在 `ScaffoldingServer._TryParseFrame` 与回写逻辑中实现同一套格式。

## 本目录模块

- **ProtocolReader.cs**：协议帧读取器（从流中解析消息帧）。
- **ProtocolWriter.cs**：协议帧写入器（将消息编码为字节帧）。

## 运行流程

- 这两个静态类是 `ScaffoldingClient.SendRequestAsync` 的核心：
  1. 发送：`ProtocolWriter.WriteRequestAsync(pipeWriter, request)` 先把请求体写入临时 `ArrayBufferWriter<byte>`，再按"类型长度 + 类型字符串 + 长度前缀 + 体"的顺序写入 `PipeWriter` 并 `FlushAsync`。
  2. 接收：`ProtocolReader.ReadResponseAsync(pipeReader)` 从 `PipeReader` 循环读取，直到能拼出一个完整响应帧（`_TryParseResponse`）。
  3. 二者均由 `ScaffoldingClient` 通过 `_srLock`（SemaphoreSlim）串行化调用，保证同一连接上"一问一答"不会交错。
- 服务端对称流程：服务端在 `ScaffoldingServer._HandleClientAsync` 中用 `_TryParseFrame` 解析请求帧，并把响应写为 `["1 字节 status" + "4 字节大端 body 长度" + body]` 后 `FlushAsync`。

## 输入输出规范

### 线上帧格式（客户端发出 / 服务端回写共用）

请求帧（客户端 → 服务端）：
1. `1 字节`：请求类型字符串长度 `typeLen`（byte）。
2. `typeLen 字节`：请求类型字符串（ASCII，例如 `c:ping`）。
3. `4 字节`：请求体长度 `bodyLen`（`UInt32`，**大端** Big-Endian）。
4. `bodyLen 字节`：请求体（由各 `IRequest` 的 `WriteRequestBody` 决定格式）。

响应帧（服务端 → 客户端）：
1. `1 字节`：`Status`（0 成功）。
2. `4 字节`：`bodyLen`（`UInt32`，大端）。
3. `bodyLen 字节`：`Body`。当 `Status == 255` 时，`Body` 为 UTF-8 编码的错误信息。

### ProtocolWriter（ProtocolWriter.cs）
- `public static ValueTask WriteRequestAsync<T>(PipeWriter writer, IRequest<T> request, CancellationToken ct = default)`。
- 实现要点：
  - 请求体先经 `request.WriteRequestBody` 写入 `ArrayBufferWriter<byte>` 得到 `requestBody`。
  - 类型字符串用 `Encoding.ASCII.GetBytes(request.RequestType)`；若长度 `> byte.MaxValue`，抛出 `InvalidOperationException("Request type is too long.")`。
  - 依次写入：类型长度(1)、类型字符串、body 长度(4, 大端)，再写入 body（非空时）。
  - 末尾 `FlushAsync`，若 `result.IsCanceled` 则抛出 `OperationCanceledException`。
- 注意：写操作不经过 `IBufferWriter` 的"预留-提交"语义的复杂管理，而是直接 `GetSpan/Advance` 推进 `PipeWriter`。

### ProtocolReader（ProtocolReader.cs）
- `public static ValueTask<ScaffoldingResponse> ReadResponseAsync(PipeReader reader, CancellationToken ct = default)`。
- 实现要点：
  - 循环 `reader.ReadAsync`，用 `_TryParseResponse` 尝试解析；若数据不够则 `AdvanceTo(buffer.Start, buffer.End)` 保留数据继续等。
  - 头部固定 `5 字节`：`[0]=Status`，`[1..5]=bodyLen`（大端）。当 `buffer.Length < 5 + bodyLen` 时返回 false（等更多数据）。
  - 解析成功后 `reader.AdvanceTo(buffer.Start)` 消费整帧，`body = bodyBuffer.ToArray()`。
  - 若 `reader.ReadAsync` 返回 `IsCompleted` 且仍无法解析，抛出 `InvalidOperationException("Connection closed unexpectedly.")`。
- 错误分支：解析出 `Status != 0` 时，若 `Status == 255` 则把 `Body` 当 UTF-8 错误信息，抛出 `ScaffoldingRequestException(status, serverMessage)`；否则抛出 `ScaffoldingRequestException(status, null)`。
- 依赖：`Client.Abstractions.IRequest`（仅类型参数约束）、`Exceptions.ScaffoldingRequestException`。

## 使用约束与注意事项

- 线程安全：`ProtocolWriter`/`ProtocolReader` 均为 `static`，自身无状态，线程安全完全依赖调用方 `ScaffoldingClient` 的 `_srLock` 串行化（每次只处理一个未完成请求/响应对）。不要在未加锁的情况下并发对同一 `PipeWriter/PipeReader` 调用。
- 长度前缀为大端：与 `GetServerPortRequest` 的 `UInt16` 大端、`ScaffoldingServer` 回写响应头使用 `BinaryPrimitives.WriteUInt32BigEndian` 一致，切勿混淆端序。
- 类型字符串长度上限：客户端侧约束为 `byte.MaxValue`，但服务端 `_TryParseFrame` 限制为 128 字节且 `0` 为非法的类型长度。生产代码请求类型均远小于此值（如 `c:ping`），但新增类型时仍需注意。
- `ProtocolReader` 默认"阻塞式"等待整帧：若连接半开或对方不发完一帧，会一直挂起直到 `CancellationToken` 取消或连接关闭。调用方务必传入有效的 `ct`（如 `ScaffoldingClient.SendRequestAsync` 透传的 token）。
- 不要在本目录直接 `AdvanceTo` 消费流：所有缓冲区的推进都封装在两个静态方法内；外部只应传入 `PipeWriter`/`PipeReader`。
- 与 `Link` 其他子模块：本目录仅依赖 `System.IO.Pipelines`、`Client.Abstractions`、`Exceptions`，不直接依赖 EasyTier/Lobby/McPing（依赖关系待确认）。

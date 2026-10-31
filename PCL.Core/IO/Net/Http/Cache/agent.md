# IO/Net/Http/Cache 目录

HTTP 缓存子系统，位于命名空间 `PCL.Core.IO.Net.Http.Cache`。为 HTTP 响应提供缓存处理与缓存元数据。

## 本目录模块

- **HttpCacheHandler.cs**：HTTP 缓存处理器（插入 HttpClient 管道，命中/存储缓存）。
- **HttpCacheMetadata.cs**：缓存元数据（过期时间、ETag 等）。

## 运行流程

本目录是 PCL 网络层（`PCL.Core.IO.Net.Http`）的可选缓存中间件，以 `DelegatingHandler` 形式插入 `HttpClient` 管线，对"API/网络类"请求做透明缓存：

1. `HttpCacheHandler` 构造时接收下游 `HttpMessageHandler`（赋值给 `InnerHandler`）和 `ICacheService` 缓存服务实例。
2. 每次 `SendAsync` 取出请求的 `RequestUri` 字符串作为缓存标识：
   - 响应体键：`CacheKeys.ApiResponse("http", uri)`；
   - 元数据键：`CacheKeys.ApiResponseMeta("http", uri)`。
3. **命中检查**：用 `cacheKey` 读取 `byte[]` 响应体。命中（`CacheResult.Found`）时直接重建 `HttpResponseMessage`（`_DeserializeResponse`），添加 `X-Cache-Hit: HIT` 头并把 `RequestMessage` 指回当前请求，短路返回，**不再访问网络**。
4. **条件请求准备**：若响应体未命中但有元数据（`HttpCacheMetadata`），则把其中的 `ETag` 写入 `If-None-Match`、把 `LastModified` 解析后写入 `If-Modified-Since`，以便服务器做 304 校验。
5. **回源**：调用 `base.SendAsync` 真正发请求。
6. **No-Store 透传**：若响应 `Cache-Control: no-store`，直接返回，不做任何缓存。
7. **304 处理**：若服务器返回 `304 NotModified`，回退读取已缓存的响应体并同样以 `X-Cache-Hit: HIT` 返回；若连缓存体都无则继续按 miss 处理。
8. **写入缓存**：读取响应体字节，按 `Cache-Control` 计算 TTL（`_ComputeTtl`），写入响应体（`Group="http"`, `Tags="http-response"`）与元数据（`CachePolicy.NeverExpire`）。miss 时在响应上加 `X-Cache-Hit: MISS`。

上层调用通常由 `HttpRequest`/网络扩展在构建 `HttpClient` 时把本处理器串到链路中；缓存实际落地由 `PCL.Core.IO.Storage.Cache` 的 `CacheService` 完成。

## 输入输出规范

### HttpCacheHandler（`HttpCacheHandler.cs`）
- 构造函数：`HttpCacheHandler(HttpMessageHandler invoker, ICacheService cacheService)`——`invoker` 成为 `InnerHandler`，`cacheService` 存为只读字段。
- `protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)`：
  - 入参：`request.RequestUri` 必填（为空则直接透传下游）；
  - 依赖 `ICacheService.GetAsync<T>(key, ct)` / `SetAsync(key, value, policy, ct)`（见 `PCL.Core.IO.Storage.Cache`）；
  - 缓存值为 `byte[]`（响应体），元数据为 `HttpCacheMetadata`。
- `private static TimeSpan? _ComputeTtl(HttpResponseMessage response)`：优先取 `CacheControl.MaxAge`，其次 `SharedMaxAge`，都没有则回退 `TimeSpan.FromMinutes(5)`。
- `private static HttpResponseMessage _DeserializeResponse(byte[] data)`：重建 `HttpStatusCode.OK` + `ByteArrayContent(data)` 的响应。**注意**：重建时状态码恒为 200，原始状态码未保留（见下方注意事项）。

### HttpCacheMetadata（`HttpCacheMetadata.cs`）
- `public record HttpCacheMetadata`（位置记录，全 `init` 属性）：
  - `string? ETag`：实体标签（来自响应 `Headers.ETag.Tag`）；
  - `string? LastModified`：最后修改时间，写入时用 `response.Content.Headers.LastModified?.ToString("O")`（ISO 8601 往返格式）；
  - `int StatusCode`：原响应状态码的整数形式（写入时保存，但当前反序列化路径未读取使用）。
- 该记录以 `CachePolicy.NeverExpire` 持久化，作为后续条件请求的校验依据。

## 使用约束与注意事项

- **线程安全**：`HttpCacheHandler` 继承自 `DelegatingHandler`，通常作为单例复用，其并发安全性取决于注入的 `ICacheService` 实现（默认 `CacheService` 应保证线程安全）。避免在多线程下共享未完成的同一 `HttpRequestMessage`。
- **缓存内容范围**：只缓存响应**体字节**与少量元数据（ETag/LastModified/StatusCode），**不缓存响应头集合**；重建的响应仅含 `ByteArrayContent`，调用方不应依赖其它原始头。
- **状态码丢失**：`_DeserializeResponse` 始终生成 200，原 304 之外的非 200 缓存场景（如缓存了错误响应）会被误判为成功——当前设计假定缓存键仅用于成功响应，使用时需确保 key 不会命中错误响应。
- **`X-Cache-Hit` 叠加**：命中分支对重建响应使用 `Headers.Add`（追加）而非设值；同一响应对象被多次 add 不会重复（新建对象），但 304 分支对同一路径逻辑一致，不会重复。若将来复用响应对象需注意。
- **`LastModified` 解析**：使用 `DateTimeOffset.Parse(...)`，依赖写入时的 `"O"` 格式保证可往返解析；若元数据来自旧版本或非标准格式可能抛 `FormatException`（当前未捕获，需上层容错）。
- **依赖项**：`CacheKeys`、`ICacheService`、`CachePolicy`、`CacheResult` 均位于 `PCL.Core.IO.Storage.Cache`（及 `PCL.Core.IO.Storage`）；`CacheKeys.ApiResponse` / `ApiResponseMeta` 对 URL 段做 SHA256 前 32 字符哈希，故相同 URL 必然得到相同键。
- **条件请求副作用**：向请求注入 `If-None-Match`/`If-Modified-Since` 会改变服务器行为；若下游期望不带条件头，应关闭本处理器或确保元数据为空。

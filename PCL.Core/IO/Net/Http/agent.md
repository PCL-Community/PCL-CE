# IO/Net/Http 目录

HTTP 子系统，位于命名空间 `PCL.Core.IO.Net.Http`。围绕 `HttpClient` 提供大量扩展方法（内容/Cookie/头/响应/发送），并包含主机连接处理、代理管理与一个内嵌的本地 HTTP 服务器（用于路由/响应）。

## 本目录模块

- **`HttpRequest.cs`** — HTTP 请求封装 `HttpRequest`：`Create`/`CreateHead`/`CreatePost`/`CreatePut`/`CreateDelete` 构造请求，`GetStringAsync`/`GetJsonAsync`/`PostJsonAsync` 提供常用快捷调用。
- **`HttpResponseException.cs`** — HTTP 响应异常 `HttpResponseException`：在非成功状态码时携带响应信息抛出（`Dispose` 释放响应）。
- **`HostConnectionHandler.cs`** — 主机连接处理器 `HostConnectionHandler`：`GetConnectionAsync` 按主机复用/限制连接（连接池与并发控制）。
- **`HttpProxyManager.cs`** — 代理管理 `HttpProxyManager`：`RefreshSystemProxy` 刷新系统代理，`GetProxy` 取代理，`IsBypassed` 判断绕过，`Dispose` 释放；配合 `ProxyMode`/`ProxyProtocol`/`ProxyItem`。
- **`HttpBasicExtension.cs`** — 基础扩展 `HttpBasicExtension`：`WithHttpVersionOption` 设置 HTTP 版本选项。
- **`HttpContentExtension.cs`** — 内容扩展 `HttpContentExtension`：`WithContent`/`WithBinaryContent`/`WithJsonContent`/`WithFormContent` 构造请求体。
- **`HttpCookieExtension.cs`** — Cookie 扩展 `HttpCookieExtension`：`WithCookie` 附加 Cookie。
- **`HttpHeaderExtension.cs`** — 头扩展 `HttpHeaderHandler`：`WithHeader`/`WithHeaders`/`WithAuthentication`/`WithBearerToken`。
- **`HttpResponseExtension.cs`** — 响应扩展 `HttpResponseExtension`：`AsString(Async)`/`AsStreamAsync`/`AsByteArrayAsync`/`AsJsonAsync` 读取响应，`GetHeader`/`TryGetHeader`/`GetFirstHeaderValue` 取头，`EnsureSuccessStatusCode(WithContentAsync)` 校验状态。
- **`HttpSenderExtension.cs`** — 发送扩展 `HttpSenderExtension`：`SendAsync` 统一发送入口。
- **`HttpRoute.cs` / `HttpRouteResponse.cs`** — 本地路由 `HttpRoute` 与路由响应 `HttpRouteResponse`（`Text`/`Json`/`Redirect`/`Empty`/`Input`/`Pour`/`AsTask`）。
- **`HttpServer.cs`** — 内嵌 HTTP 服务器 `HttpServer`：`Register` 注册路由，`Start`/`Stop`/`Dispose` 控制生命周期。

## 直接代码要点

- **运行流程**：以 `HttpRequest.Create*` 构造请求 → 用 `With*` 扩展链式附加头/内容/Cookie/版本 → `HttpSenderExtension.SendAsync` 发送 → `HttpResponseExtension` 读取或 `EnsureSuccessStatusCode` 校验；本地场景由 `HttpServer` 注册 `HttpRoute` 并返回 `HttpRouteResponse`。
- **输入输出规范**：输入为 URL、头、内容（字符串/字节/JSON/表单）、Cookie；输出为字符串/流/字节/JSON 或异常；`HttpRouteResponse` 支持文本、JSON、重定向、空响应。
- **使用约束与注意事项**：`HttpClient` 应复用（见 `NetworkService`）；响应与异常需 `Dispose`；代理需在系统代理变化后 `RefreshSystemProxy`；内嵌服务器需在 `Dispose` 时停止监听，避免端口占用。

## 子目录导航

- [Cache](./Cache/agent.md)：HTTP 响应缓存。

# Modules/Network/Http

HTTP 请求封装，命名空间 `PCL.Network`。

## 本目录直接文件

- `Requester.cs`：静态请求类，封装 `HttpClient` 调用、`EnsureSuccess` 响应校验、浏览器 UA、超时/重试等通用请求逻辑。
- `RequestSigning.cs`：请求签名逻辑，为外发请求附加必要的签名/鉴权信息。

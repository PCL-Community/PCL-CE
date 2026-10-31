# Network 测试分组

本目录测试 PCL.Core 中的网络相关组件：DNS-over-HTTPS 查询、内嵌 HTTP 服务、DoH 连接。

## 本目录测试模块

- `DoHQueryTest.cs` — `DnsQuery.Instance` 的 DoH 查询：A/AAAA 解析（`QueryForIpAsync`）与 SRV 记录解析（`QueryAsync`），验证返回非空与记录类别（`PCL.Core.IO.Net.Dns`）。需联网。
- `WebServerTest.cs` — `HttpServer` 子类化并注册路由，验证 GET 文本/JSON 响应与默认 404/NoContent 行为；测试中以 `127.0.0.1:8080` 启动（`PCL.Core.IO.Net.Http`）。
- `DohConnection.cs` — DoH 连接建立与请求往返验证（`PCL.Core.IO.Net.Dns`）。

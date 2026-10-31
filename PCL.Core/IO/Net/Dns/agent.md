# IO/Net/Dns 目录

DNS 子系统，位于命名空间 `PCL.Core.IO.Net.Dns`。提供 DNS 查询、SRV 记录解析与多服务器竞速选择。

## 本目录模块

- **DnsQuery.cs**：DNS 查询封装。
- **DnsSrvResource.cs**：SRV 资源记录模型（服务发现）。
- **WeightedDnsRacerClient.cs**：加权 DNS 竞速客户端（向多个 DNS 并发查询并采用最快结果）。

# Modules/Network/Facade

网络门面，命名空间 `PCL.Network`。集中定义并管理全局网络下载参数。

## 本目录直接文件

- `ModNet.cs`：静态类，定义下载连接数限制（`NetTaskConnectionLimit`，默认 64）、单文件最大连接数（8）、每主机连接限制（256）、缓冲区预算（512MB）、下载临时后缀（`.PCLDownloading`）等全局常量与可配属性。

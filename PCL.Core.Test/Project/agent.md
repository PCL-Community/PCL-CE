# Project 测试分组

本目录测试 PCL.Core 中与外部项目平台（如 Modrinth）交互的逻辑。

## 本目录测试模块

- `Modrinth.cs` — 对 `api.modrinth.com/v2/project/sodium` 发起真实 HTTP 请求并校验响应可达（`PCL.Core.Project`，或对应 Modrinth 模型）。需联网；反序列化断言部分被注释。

# App/Configuration/Storage 测试

本目录测试 PCL.Core 中应用配置的文件持久化存储机制。

## 本目录测试模块

- `FileConfigStorageTest.cs` — `FileConfigStorage` 的键值读写与 `Stop()` 时落盘行为；使用内存版 `IKeyValueFileProvider` 桩验证 `SetValue` 后 `Stop` 能持久化排队的写入（`PCL.Core.App.Configuration.Storage`）。

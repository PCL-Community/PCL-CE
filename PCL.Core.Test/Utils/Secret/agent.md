# Utils/Secret 测试

本目录测试 PCL.Core 中的密钥管理与加密辅助逻辑（用户密钥文件的迁移、隔离与重建）。

## 本目录测试模块

- `EncryptHelperKeyTest.cs` — `EncryptHelper` 用户密钥文件（v1→v2 CNG 迁移、损坏/未知版本隔离重建、瞬时占用不轮换）；通过 `OverrideKeyFile` + `ResetKeyCache` 在临时目录隔离，并标记 `[DoNotParallelize]`（密钥缓存为进程级静态）（`PCL.Core.Utils.Secret`）。

# Encryption 测试

本目录测试 PCL.Core 中的对称加密提供程序（AES-GCM 与 ChaCha20 系列），验证加解密往返的字节一致性。

## 本目录测试模块

- `AesGcmTest.cs` — `AesGcmProvider` 对随机数据以 32 字节密钥加密后解密，逐字节比对一致性（`PCL.Core.Utils.Encryption`）。
- `ChaCha20.cs` — `ChaCha20SoftwareProvider` 加解密往返一致性验证。
- `ChaCha20Poly1305.cs` — `ChaCha20Poly1305Provider` 带认证的加解密往返验证。

（注：类名与文件同名，均为 `[TestClass]`，使用 `Core.Utils.Encryption.*.Instance` 单例。）

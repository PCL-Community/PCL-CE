# Utils/Encryption 目录

对称加密，位于命名空间 `PCL.Core.Utils.Encryption`。提供可替换的加密提供器实现。

## 本目录模块

- **IEncryptionProvider.cs**：加密提供器接口（加密/解密）。
- **AesCbcProvider.cs**：AES-CBC 加密提供器。
- **AesGcmProvider.cs**：AES-GCM 加密提供器（带认证）。
- **ChaCha20Poly1305Provider.cs**：ChaCha20-Poly1305 加密提供器（带认证）。
- **ChaCha20SoftwareProvider.cs**：ChaCha20 纯软件实现提供器。

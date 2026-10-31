# Utils/Hash 目录

哈希算法，位于命名空间 `PCL.Core.Utils.Hash`。提供常用哈希算法实现与缓存。

## 本目录模块

- **IHashProvider.cs**：哈希提供器接口。
- **MD5Provider.cs**：MD5 哈希提供器。
- **SHA1Provider.cs**：SHA1 哈希提供器。
- **SHA256Provider.cs**：SHA256 哈希提供器。
- **SHA512Provider.cs**：SHA512 哈希提供器。
- **MurmurHash2Provider.cs**：MurmurHash2 哈希提供器（快速非加密哈希）。
- **HashCache.cs**：哈希缓存（相同输入复用计算结果）。

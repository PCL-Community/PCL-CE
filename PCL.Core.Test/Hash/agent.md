# Hash 测试

本目录测试 PCL.Core 中的哈希算法与哈希缓存工具。

## 本目录测试模块

- `MurmurHash2Test.cs` — `MurmurHash2Provider` 对固定字符串的哈希值是否与已知向量一致，以及十六进制字符串互转（`PCL.Core.Utils.Hash` + `PCL.Core.Utils.Exts`）。
- `HashCacheTest.cs` — `HashCache` 哈希计算与缓存命中行为，临时目录隔离各用例（`PCL.Core.Utils.Exts` / `PCL.Core.Utils.Hash`）。

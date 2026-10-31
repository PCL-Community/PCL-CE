# App/Configuration/Storage 目录

配置持久化层，位于命名空间 `PCL.Core.App.Configuration.Storage`。为 `Configuration` 提供多种键值文件格式的读写能力与不同存储策略。

## 本目录模块

- **IKeyValueFileProvider.cs / IEnumerableKeyProvider.cs**：键值文件提供器与可枚举键提供器接口。
- **CommonFileProvider.cs**：通用文件提供器基类。
- **CatIniFileProvider.cs**：INI 格式文件提供器。
- **JsonFileProvider.cs**：JSON 格式文件提供器。
- **YamlFileProvider.cs**：YAML 格式文件提供器。
- **JsonToYamlConverter.cs / YamlNodeConverter.cs / YamlNodeEmitter.cs**：JSON 与 YAML 节点间的转换与 YAML 节点发射。
- **ConfigStorage.cs**：配置存储核心抽象。
- **FileConfigStorage.cs**：基于文件的配置存储。
- **EncryptedFileConfigStorage.cs**：加密文件配置存储。
- **DynamicCacheConfigStorage.cs**：带动态缓存的配置存储。
- **ConfigFileInitException.cs**：配置文件初始化异常。

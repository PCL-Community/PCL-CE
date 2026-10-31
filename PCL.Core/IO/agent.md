# IO 目录

输入输出层，位于命名空间 `PCL.Core.IO`。封装文件/目录/字节流/管道的基础操作，并在其下提供下载抽象、网络（DNS/HTTP/缓存/端口转发）与基于哈希和 SQLite 的存储缓存。

## 本目录模块

- **`ByteStream.cs`** — 字节流辅助 `ByteStream`：`GetReadableLength` 把字节数格式化为可读容量（B/KB/MB…），并承载流相关的小工具。
- **`Directories.cs`** — 目录异步操作 `Directories`：`CheckPermissionAsync` / `CheckPermissionWithExceptionAsync`（权限探测）、`DeleteDirectoryAsync`、`CopyDirectoryAsync`、`EnumerateFilesAsync`（异步枚举）。
- **`Files.cs`** — 文件综合工具 `Files`：`CreateShortcut`（创建快捷方式）、`ArePathsEqual`、`ExportAsZipArchiveAsync`（导出 ZIP）、`CopyFileAsync` / `CopyDirectoryAsync`、`ReadAllBytesOrEmptyAsync` / `ReadAllTextOrEmptyAsync` / `ReadFileToStreamOrEmptyAsync`（缺失时返回空的容错读取）、`WriteFileAsync`、`ExtractFileAsync`（解压）、`ComputeFileHashAsync` 与 `GetFileMD5Async`/`GetFileSHA1Async`/`GetFileSHA256Async`/`GetFileSHA512Async`、`GetFullPath`、`CheckFileBusyAsync`（占用检测）、`PasteFromClipboardAsync`（剪贴板粘贴）。
- **`PipeComm.cs`** — 命名管道通信 `PipeComm`：`StartPipeServer` 启动本地管道服务端，用于进程间通信。

## 直接代码要点

- **运行流程**：上层通过 `Files` / `Directories` 的异步 API 完成磁盘读写、复制、解压与哈希计算；哈希方法以流式读取方式计算；`PipeComm.StartPipeServer` 启动后监听本地管道连接。
- **输入输出规范**：输入多为路径字符串、`Stream`、`byte[]`；异步方法返回 `Task`/`Task<T>` 且通常接受 `CancellationToken`；哈希类方法输出十六进制字符串；容错读取在文件不存在时返回空集合/空串而非抛异常。
- **使用约束与注意事项**：写/删前可用 `CheckFileBusyAsync` 预检占用；跨平台路径需经 `GetFullPath` 规范化；批量目录操作注意异常中断后的残留；管道服务端需正确释放以避免句柄泄漏。

## 子目录导航

- [Download](./Download/agent.md)：下载抽象层（连接、资源映射、写入器）。
- [Net](./Net/agent.md)：网络层（DNS、HTTP、端口转发）。
- [Storage](./Storage/agent.md)：基于哈希的内容存储与缓存。

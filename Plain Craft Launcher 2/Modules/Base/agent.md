# Modules/Base

最底层的基础通用模块，命名空间 `PCL`，被其他所有模块广泛依赖。

## 本目录直接文件

- `ModBase.cs`：核心工具集。提供路径常量（如 `exePath`/`pathTemp`/`pathAppdata`）、日志记录（`Log`）、文件读写、线程辅助（`RunInNewThread`/`RunInUi`）、`SafeList` 线程安全集合、程序退出与返回值（`ProcessReturnValues`）、反馈信息（`FeedbackInfo`）等底层能力。
- `ModLoader.cs`：下载/加载任务基类与机制。定义 `LoaderBase` 及 `LoaderFolderRunType`（运行时机：更新后/强制等），为网络下载提供可复用任务框架。
- `ModSetup.cs`：安装与设置初始化逻辑（处理首次启动/目录准备等）。
- `ModAnimation.cs`：动画系统（`AniBeginControl` 等），控制界面过渡动画与帧率。
- `MyBitmap.cs`：万能图片类型转换工具类（带缓存），在 `System.Drawing` 与 WPF `BitmapSource` 间互转。

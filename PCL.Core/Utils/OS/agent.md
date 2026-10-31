# Utils/OS 目录

操作系统互操作，位于命名空间 `PCL.Core.Utils.OS`。封装 Windows 平台相关的剪贴板、拖拽、内核/NT/进程/窗口互操作、硬件与系统信息、注册表监控与系统主题。

## 本目录模块

- **ClipboardUtils.cs**：剪贴板工具（读写文本/文件）。
- **DragHelper.cs**：拖拽辅助（文件拖放）。
- **WindowInterop.cs**：窗口互操作（置顶、闪烁、句柄操作）。
- **ProcessInterop.cs**：进程互操作（启动/枚举/结束）。
- **KernelInterop.cs**：内核互操作（kernel32 调用）。
- **NtInterop.cs**：NT 互操作（ntdll 调用）。
- **EnvironmentInterop.cs**：环境互操作（环境变量、系统目录）。
- **SystemInfo.cs / HardwareInfo.cs**：系统信息与硬件信息（CPU/内存等）。
- **SystemPaths.cs**：系统路径（特殊目录）。
- **SystemTheme.cs**：系统主题检测。
- **RegistryChangeMonitor.cs**：注册表变更监控。
- **NetworkUtils.cs**：网络工具（系统级网络信息）。
- **AumidHelper.cs**：AUMID 辅助（应用用户模型 ID，用于 Toast/跳转列表）。

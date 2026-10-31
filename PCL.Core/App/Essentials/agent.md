# App/Essentials 目录

应用级核心服务集合，位于命名空间 `PCL.Core.App.Essentials`。这些服务在生命周期中承担启动、单实例、更新、遥测、进程间通信与主窗口管理等全局职责。

## 本目录模块

- **ApplicationService.cs**：应用级服务（应用生命周期/消息循环相关）。
- **MainWindowService.cs**：主窗口服务（窗口创建/管理）。
- **SingleInstanceService.cs**：单实例服务（保证启动器唯一运行实例）。
- **StartupService.cs / StartupValidation.cs**：启动服务与启动前校验。
- **UpdateService.cs / UpdateHelper.cs**：更新服务与更新辅助。
- **PromoteService.cs**：推广/公告服务。
- **TelemetryService.cs**：遥测服务（基于 Sentry 上报）。
- **RpcService.cs / RpcFunction.cs / RpcProperty.cs / RpcResponse.cs**：进程间 RPC 通信（函数调用、属性、响应）。

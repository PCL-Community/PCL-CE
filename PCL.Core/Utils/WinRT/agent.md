# Utils/WinRT 目录

WinRT 互操作，位于命名空间 `PCL.Core.Utils.WinRT`。提供与 Windows Runtime 交互的底层辅助（HString 处理），其下 `Interface` 定义所需的 WinRT COM 接口声明。

## 本目录模块

- **`HString.cs`** — WinRT HString 封装 `HString`：表示 WinRT 字符串句柄，配合辅助方法安全创建/释放。
- **`HStringHelper.cs`** — HString 辅助 `HStringHelper`：`ToHString`（托管串→HString）、`ToManagedString`（HString→托管串）、`DeleteHString`（释放）、`WithReference`（带引用执行）。
- **`WinRTInterop.cs`** — 互操作入口 `WinRTInterop`：`ActivateInstance` 激活 WinRT 实例、`GetActivationFactory` 取激活工厂，并以 `WindowsCreateString`/`WindowsCreateStringReference`/`WindowsDeleteString` 管理 HString；`HStringHeader` 描述其内存布局。

## 直接代码要点

- **运行流程**：托管代码 → `WinRTInterop.GetActivationFactory`/`ActivateInstance` 获取 WinRT 对象 → 参数以 `HStringHelper.ToHString` 封送 → 调用 COM 接口 → 结果转回托管串 → `DeleteHString` 释放。
- **输入输出规范**：输入为托管字符串/类型名；输出为 WinRT 对象或托管字符串；HString 必须成对创建/释放。
- **使用约束与注意事项**：WinRT 调用有 COM 线程模型要求（通常 STA/UI 线程）；`HString` 句柄泄漏会导致内存增长；仅 Windows 平台可用；接口声明须与 Windows SDK 版本匹配。

## 子目录导航

- [Interface](./Interface/agent.md)：WinRT COM 接口声明。

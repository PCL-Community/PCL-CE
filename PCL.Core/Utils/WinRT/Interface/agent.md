# Utils/WinRT/Interface 目录

WinRT COM 接口声明，位于命名空间 `PCL.Core.Utils.WinRT.Interface`。以 C# 互操作结构声明 Windows RT 的 COM 接口（用于底层 Toast 等能力），按 Windows SDK 命名空间分层。

## 本目录模块

- **`IInspectable.cs`** — WinRT 对象根接口 `IInspectable` 及其虚表 `IInspectableVtbl`：所有 WinRT 接口的基础（QueryInterface/AddRef/Release 及 GetIids/GetRuntimeClassName/GetTrustLevel）。

## 直接代码要点

- **运行流程**：具体 WinRT 接口（如 `Windows.UI.Notifications`）继承/复用 `IInspectable` 的虚表布局 → 通过 `WinRTInterop` 激活实例后按虚表函数指针调用。
- **输入输出规范**：接口以 `unsafe struct` + `delegate* unmanaged` 函数指针描述；调用需按 COM 约定传参与返回 HRESULT。
- **使用约束与注意事项**：虚表顺序必须与 Windows SDK 完全一致，错位会导致崩溃；引用计数需正确 AddRef/Release；仅 Windows 可用，且需匹配目标系统版本。

## 子目录导航

- [Windows](./Windows/agent.md)：Windows.* 命名空间下的接口声明。

# UI/Animation/UIAccessProvider 目录

UI 访问提供器，位于命名空间 `PCL.Core.UI.Animation.UIAccessProvider`。允许动画在非 UI 线程安全地访问并更新 UI 元素。

## 本目录模块

- **IUIAccessProvider.cs**：UI 访问提供器接口（在 UI 线程执行动作）。
- **WpfUIAccessProvider.cs**：基于 WPF Dispatcher 的访问提供器实现。

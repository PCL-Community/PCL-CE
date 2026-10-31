# UI/Converters 目录

值转换器，位于命名空间 `PCL.Core.UI.Converters`。供 WPF 绑定使用的 `IValueConverter` 实现。

## 本目录模块

- **NColorConverter.cs**：颜色转换器（`NColor` 与 `Brush`/各颜色类型互转）。
- **EasingConverter.cs**：缓动转换器（缓动类型/参数到 `IEasing` 实例）。
- **NullToVisibilityConverter.cs**：空值转可见性（null→Collapsed 等）。

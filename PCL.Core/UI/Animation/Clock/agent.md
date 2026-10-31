# UI/Animation/Clock 目录

动画时钟，位于命名空间 `PCL.Core.UI.Animation.Clock`。为动画提供时间源（帧计时）。

## 本目录模块

- **IClock.cs**：时钟接口（提供当前时间与帧回调）。
- **IUIClock.cs**：UI 时钟接口（与渲染帧同步）。
- **StopwatchClock.cs**：基于 Stopwatch 的时钟。
- **WinMMClock.cs**：基于 WinMM 高精度计时的时钟。
- **WpfCompositionTargetRenderingClock.cs**：基于 WPF 渲染帧的时钟。

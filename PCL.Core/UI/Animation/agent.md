# UI/Animation 目录

动画引擎子系统，位于命名空间 `PCL.Core.UI.Animation`。提供一套独立的属性动画框架（FromTo 动画），可在 WPF 与非 WPF 环境下驱动 UI/CL 对象的属性过渡。其底层依赖核心、时钟、缓动、值处理器与 UI 访问提供器。

## 本目录模块

本目录集中定义各类"从 A 过渡到 B"的动画实现，均以 `*FromToAnimation` 命名并实现统一的 `ComputeNextFrame` 计算入口：

- **DoubleFromToAnimation.cs** — 双精度数值 `double` 的 FromTo 动画。
- **PointFromToAnimation.cs** — `Point` 的 FromTo 动画。
- **ThicknessFromToAnimation.cs** — `Thickness` 的 FromTo 动画。
- **MatrixFromToAnimation.cs** — `Matrix` 的 FromTo 动画。
- **NColorFromToAnimation.cs** — 颜色 `NColor` 的 FromTo 动画（配合 `UI/NColor`）。
- **NRotateTransformFromToAnimation.cs** — 旋转变换 `NRotateTransform` 的 FromTo 动画。
- **NScaleTransformFromToAnimation.cs** — 缩放变换 `NScaleTransform` 的 FromTo 动画。

## 直接代码要点

- **运行流程**：动画由 `Core` 中的动画框架统一驱动 → `Clock` 提供时间进度 → `Easings` 计算缓动比例 → 各 `*FromToAnimation.ComputeNextFrame` 依据进度在起止值之间插值 → 经 `ValueProcessor` 处理并由 `UIAccessProvider` 写回目标对象属性。
- **输入输出规范**：输入为起止值、时长/进度与缓动函数；`ComputeNextFrame` 输出当前帧的计算值（具体签名与返回类型以基类/接口定义为准）。
- **使用约束与注意事项**：这些类型是"值计算器"，本身不持有线程/时钟，需由 `Core` 框架调度；对 UI 属性的写回必须在 UI 线程（经 `UIAccessProvider`）；`NColor`/`NRotateTransform` 等自定义值类型需保证插值语义正确。

## 子目录导航

- [Animatable](./Animatable/agent.md)：可动画对象抽象。
- [Clock](./Clock/agent.md)：动画时钟。
- [Core](./Core/agent.md)：动画核心框架。
- [Easings](./Easings/agent.md)：缓动函数库。
- [UIAccessProvider](./UIAccessProvider/agent.md)：UI 访问提供器。
- [ValueProcessor](./ValueProcessor/agent.md)：值处理器。

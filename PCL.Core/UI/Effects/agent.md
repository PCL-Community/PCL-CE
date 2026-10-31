# UI/Effects 目录

模糊特效，位于命名空间 `PCL.Core.UI.Effects`。提供 WPF 的模糊效果实现与高效采样处理器，用于背景模糊、控件模糊等视觉表现。

## 本目录模块

- **EnhancedBlurEffect.cs**：增强模糊效果。
- **OptimizedBlurEffect.cs**：优化模糊效果（性能优先）。
- **AdaptiveBlurEffect.cs**：自适应模糊效果（按内容/区域调整）。
- **SamplingBlurProcessor.cs**：采样模糊处理器（底层像素采样与卷积）。

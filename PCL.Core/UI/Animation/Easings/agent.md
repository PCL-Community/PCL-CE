# UI/Animation/Easings 目录

缓动函数库，位于命名空间 `PCL.Core.UI.Animation.Easings`。提供完整的缓动实现族：Back/Cubic/Quad/Quartic/Quintic/Sine/Circular/Exponential/Elastic/Bounce 各自的 EaseIn/EaseOut/EaseInOut，以及带幂次（WithPower）变体与组合缓动。

## 本目录模块

- **IEasing.cs**：缓动接口（输入 0–1 输出 0–1）。
- **Easing.cs**：缓动基类。
- **LinearEasing.cs**：线性缓动。
- **EasePower.cs**：通用幂次缓动基类。
- **CubicEaseIn.cs / CubicEaseOut.cs / CubicEaseInOut.cs**：三次缓动三态。
- **QuadEaseIn.cs / QuadEaseOut.cs / QuadEaseInOut.cs**：二次缓动三态。
- **QuarticEaseIn.cs / QuarticEaseOut.cs / QuarticEaseInOut.cs**：四次缓动三态。
- **QuinticEaseIn.cs / QuinticEaseOut.cs / QuinticEaseInOut.cs**：五次缓动三态。
- **SineEaseIn.cs / SineEaseOut.cs / SineEaseInOut.cs**：正弦缓动三态。
- **CircularEaseIn.cs / CircularEaseOut.cs / CircularEaseInOut.cs**：圆弧缓动三态。
- **ExponentialEaseIn.cs / ExponentialEaseOut.cs / ExponentialEaseInOut.cs**：指数缓动三态。
- **ElasticEaseIn.cs / ElasticEaseOut.cs / ElasticEaseInOut.cs**：弹性缓动三态。
- **BounceEaseIn.cs / BounceEaseOut.cs / BounceEaseInOut.cs**：弹跳缓动三态。
- **BackEaseIn.cs / BackEaseOut.cs / BackEaseInOut.cs**：回退缓动三态。
- **BackEaseWithPowerIn.cs / BackEaseWithPowerOut.cs / BackEaseWithPowerInOut.cs**：带幂次回退缓动三态。
- **CombinedEasing.cs / CompositeEasing.cs**：组合缓动（多段拼接/复合）。

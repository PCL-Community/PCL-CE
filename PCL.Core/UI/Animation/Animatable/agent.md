# UI/Animation/Animatable 目录

可动画对象抽象，位于命名空间 `PCL.Core.UI.Animation.Animatable`。定义可被动画引擎驱动的目标对象接口与多种实现。

## 本目录模块

- **IAnimatable.cs**：可动画对象接口。
- **ClrAnimatable.cs**：基于 CLR 属性（反射）的可动画对象。
- **WpfAnimatable.cs**：基于 WPF DependencyProperty 的可动画对象。
- **EmptyAnimatable.cs**：空实现（占位/无操作）。

# UI/Animation/ValueProcessor 目录

值处理器，位于命名空间 `PCL.Core.UI.Animation.ValueProcessor`。将动画计算出的中间值应用到具体的目标属性（按类型分派）。

## 本目录模块

- **IValueProcessor.cs**：值处理器接口。
- **ValueProcessorManager.cs**：值处理器管理器（按类型选择处理器）。
- **DoubleValueProcessor.cs**：双精度值处理器。
- **PointValueProcessor.cs**：点值处理器。
- **ThicknessValueProcessor.cs**：Thickness 值处理器。
- **MatrixValueProcessor.cs**：矩阵值处理器。
- **NColorValueProcessor.cs**：颜色值处理器。
- **NRotateTransformValueProcessor.cs**：旋转变换值处理器。
- **NScaleTransformValueProcessor.cs**：缩放变换值处理器。

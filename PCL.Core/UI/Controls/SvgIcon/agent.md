# UI/Controls/SvgIcon 目录

SVG 图标控件，位于命名空间 `PCL.Core.UI.Controls.SvgIcon`。实现在 WPF 中加载、解析与渲染 SVG 图标，支持样式与绘制选项，替代传统位图图标以适配主题与缩放。

## 本目录模块

- **SvgIcon.cs**：SVG 图标控件（依赖属性：路径/样式/绘制选项）。
- **SvgIconElement.cs**：SVG 元素模型。
- **SvgIconElementKind.cs**：SVG 元素类型枚举。
- **SvgIconModel.cs**：SVG 图标数据模型。
- **SvgIconStyle.cs**：SVG 图标样式（尺寸/拉伸等）。
- **SvgIconPaintOptions.cs**：绘制选项（颜色/不透明度）。
- **SvgIconLoader.cs**：SVG 图标加载器（缓存/读取）。
- **SvgIconParser.cs**：SVG 解析器（解析为元素树）。
- **SvgNumberParser.cs**：SVG 数值解析。
- **SvgPaintParser.cs**：SVG 绘制（paint）属性解析。
- **SvgPathGeometryParser.cs**：SVG Path 几何解析（转为 WPF Geometry）。

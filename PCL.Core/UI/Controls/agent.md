# UI/Controls 目录

自定义控件，位于命名空间 `PCL.Core.UI.Controls`。提供若干 WPF 自定义控件/装饰，补充启动器界面所需的视觉效果。

## 本目录模块

- **BlurBorder.cs** — 模糊边框控件 `BlurBorder`：带背景模糊的边框容器，重写 `ArrangeOverride` / `OnRender` 等布局与渲染方法，内部以 `GenerateGeometry` / `CalculateLayoutClip` 生成圆角几何与裁剪区域（`Radii` 描述圆角）。
- **Tooltip.cs** — 自定义工具提示控件 `Tooltip`：提供附加属性 `IsEnabled` / `FollowCursor` 的读写器（`SetIsEnabled`/`GetIsEnabled`、`SetFollowCursor`/`GetFollowCursor`）及 `Enable` / `Disable` / `Dismiss` 控制显示与跟随光标行为。
- **WaterfallPanel.cs** — 瀑布流排列面板 `WaterfallPanel`：自定义布局容器，重写 `MeasureOverride` / `ArrangeOverride`，按 `WaterfallLayoutMode` 决定分列与排列方式。
- **MotdRenderer.xaml.cs** — MOTD 渲染器 `MotdRenderer`：将服务器 MOTD 文本（含 `§` 颜色/格式代码）渲染为富文本；`TryGetColorFromCode` 解析颜色码，`RenderMotd` / `RenderCanvas` 执行渲染，`ClearCanvas` 清理。

## 直接代码要点

- **运行流程**：`BlurBorder` / `WaterfallPanel` 参与 WPF 布局与渲染管线（Measure→Arrange→Render）；`Tooltip` 作为附加行为挂到目标元素并在启用时跟随光标；`MotdRenderer` 先解析 MOTD 颜色码，再逐段绘制到画布。
- **输入输出规范**：布局控件输入为子元素集合与可用尺寸，输出为排列后的尺寸/位置；`MotdRenderer` 输入为 MOTD 字符串，输出为可视文本/画布内容；`Tooltip` 通过附加属性控制行为。
- **使用约束与注意事项**：全部依赖 WPF，必须在 UI/STA 线程使用；自定义 `MeasureOverride`/`ArrangeOverride` 需正确处理无限尺寸与空子元素；模糊/几何裁剪可能带来渲染开销，避免在高频刷新场景滥用。

## 子目录导航

- [SvgIcon](./SvgIcon/agent.md)：SVG 图标控件。

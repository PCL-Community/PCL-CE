# Controls

PCL2 的 **自定义 WPF 控件集合**，命名空间 `PCL`。提供启动器界面所需的各类基础控件、布局框架与辅助工具。本目录为控件的"实现层"，所有控件均为无业务逻辑的纯 UI 组件，被 `Pages` 与各页面广泛使用；消息框系列（`MyMsg`）与交互行为（`Behaviors`）各自独立成子目录。

## 本目录直接文件（代表性分类）

> 下列每个控件均有对应 `.xaml`（模板）+ `.xaml.cs`（逻辑），或纯 `.cs`（逻辑控件）；统一派生自 WPF 原生控件并重写视觉/交互。所有控件均含 `Uuid = Common.GetUuid()` 用于日志追踪。导航/单选项统一实现 `IMyRadio` 接口（暴露 `Check` / `Changed` 事件）。

- **按钮类**：
  - `MyButton`（`partial class MyButton`）：通用主按钮，支持 `Text`、`ColorState`（颜色主题）、`Padding`、`Click` 事件与内联文本 `Inlines`。
  - `MyTextButton`（`class MyTextButton : Label`）：轻量文本按钮，含 `Text` 与 `Click` 事件，左栏列表项常用。
  - `MyIconButton`（`partial class MyIconButton`）：图标按钮，支持 `Logo`/`SvgIcon`/`LogoScale`/`Theme`（`Themes.Color` 等）、`Click` 事件与 `RefreshAnim()` 涟漪动画；附带 `static partial class Animations.AniDispose`。
  - `MyIconTextButton`（`partial class MyIconTextButton`）：图标 + 文字组合按钮。
  - `MyExtraButton`（`partial class MyExtraButton`）：主窗口右下角"附加按钮"（更新重启/音乐/任务/日志/返回顶部等），关键特性是 `showCheck`（`ShowCheckDelegate` 返回 `bool`）控制按需显隐，`ShowRefresh()` 重新评估可见性；支持 `Progress`、`Logo`/`SvgIcon`、`Click`/`RightClick`（可右键）、`Ribble()` 涟漪。
  - `MyExtraTextButton`（`partial class MyExtraTextButton`）：带文字的附加按钮变体。
  - `MyRadioButton`（`partial class MyRadioButton`，实现 `IMyRadio`）：单选按钮，含 `Text`/`Logo`/`SvgIcon`/`ColorState`/`Checked`、`SetChecked(value, raiseByMouse, anime)`、`Check`/`PreviewClick` 事件；左栏导航项使用，`PanTitleSelect` 按下即触发 `PageChangeActual`。
  - `MyRadioBox`（`partial class MyRadioBox : IMyRadio`）：方形复选/单选框，含 `Checked`/`Text`、`Check`/`Changed` 事件与 `PreviewChange`/`PreviewCheck`。

- **输入/选择类**：
  - `MyTextBox`（`class MyTextBox : TextBox`）：输入框，内置校验框架 `ValidateRules`（`Collection<IValidator<string>>`）、`ValidateResult`/`IsValidated`/`ShowValidateResult`、`HintText` 占位提示、`CornerRadius`、`ValidatedTextChanged`/`ValidateChanged` 事件与 `Validate()` 方法。
  - `MyCheckBox`（`partial class MyCheckBox`）：复选框，支持三态 `IsThreeState`/`Checked`（`bool?`）、`Text`、调用 `SetChecked(value, user)`、`Change`/`PreviewChange` 事件。
  - `MyComboBox`（`class MyComboBox : ComboBox`）：下拉框，含 `HintText`、`Text`（重写）、`DropDownWidthSync`、`TextChanged` 事件与 `RefreshColor()`。
  - `MyComboBoxItem`（`class MyComboBoxItem : ComboBoxItem`）：下拉项，支持 `implicit operator string` 与 `ToString()`。
  - `MySearchBox`（`partial class MySearchBox : MyCard`）：搜索框（基于卡片），含 `HintText`/`Text`/`SearchButtonVisibility`、`Search`/`TextChanged` 事件。
  - `MySlider`（`partial class MySlider`）：滑块，含 `MaxValue`/`Value`/`ValueByKey`、`Change`/`PreviewChange` 事件，`DragDoing()`/`DragStop()`（配合主窗口 `DragTick` 全局拖动）、`RefreshPopup()`；`getHintText` 委托动态生成提示。
  - `MyScrollBar`（`class MyScrollBar : ScrollBar`）、`MyScrollViewer`（`class MyScrollViewer : ScrollViewer`）：自定义滚动条/滚动视图，后者含 `scrollBar`、`DeltaMult`（滚轮倍率）、`PerformVerticalOffsetDelta(delta)`（供主窗口"返回顶部"调用）。

- **展示/反馈类**：
  - `MyCard`（`class MyCard : AnimatedBackgroundGrid`）：通用卡片容器，是 `MySearchBox` 等的基类；含 `Title`/`BorderChild`/`MainChrome`（`MyDropShadow`）/`CornerRadius`/`HasMouseAnimation`/`UseAnimation`、`SwapControl`/`CanSwap`/`IsSwapped`（可翻转内容）、`StackInstall()`（向 `StackPanel` 批量装载子控件），以及 `static StackInstall(ref StackPanel, Action<StackPanel>)`。
  - `MyListItem`（`partial class MyListItem : IMyRadio`）：列表项，启动器中最核心的可点击条目；含 `tag`/`Tags`/`PlainTags`（数据绑定）、`Click`/`LogoClick`/`Check`/`Changed` 事件、`RefreshColor()`、`buttonStack`/`pathLogo`/`rectCheck`/`PanTags`/`LabInfo` 等模板部件；左栏导航与各类列表均使用它。
  - `MyHint`（`partial class MyHint`）：内联提示条，含 `Theme`（`Themes` 枚举）、`IsWarn`、`Text`/`Inlines`、`HasBorder`/`CanClose`/`RelativeSetup`（可跳转设置页）；附带 `static partial class Animations.AniDispose`。
  - `MyLoading`（`partial class MyLoading`）：加载占位/加载状态控件，含 `AutoRun`/`ShowProgress`/`Text`/`TextError`/`TextErrorInherit`、`MyLoadingState` 状态枚举、`State`（`ILoadingTrigger`）、`IsErrorChanged`/`StateChanged`/`Click` 事件与 `HasAnimation`；`MyLoadingStateSimulator` 用于模拟加载进度；`IMyRadio` 之外的 `ILoadingTrigger` 接口定义驱动源。
  - `MyToast`（`partial class MyToast`）：屏幕右下角轻量通知，含 `Context`/`Icon`/`ToastType`（`HintType`）/`DisplayDuration`/`IsDismissing`，提供 `Show()`/`Emphasize()`/`Dismiss()`。
  - `MyDropShadow`（`class MyDropShadow : Decorator`）：装饰层投影，含 `Color`/`ShadowRadius`/`CornerRadius`。
  - `MyImage`（`class MyImage : Image`）：图片控件，支持 `Source`/`ActualSource`/`FallbackSource`/`CornerRadius`、`EnableCache`（14 天缓存）、`fileCacheExpiredTime`，以及静态 `DownloadImageAsync(url)`（异步下载并缓存）、`GetTempPath(url)`。
  - `MyMenuItem`（`class MyMenuItem : MenuItem`）：菜单项，支持 `SvgIcon`。
  - `MyCollapseBar`（`class MyCollapseBar : StackPanel`）：可折叠分区栏，含 `Title`/`ContentPanel`/`IsCollapsed` 与 `Toggled` 事件。
  - `MyVirtualizingElement<T>` / `MyVirtualizingElement`（`class ... : FrameworkElement`）：延迟初始化包装器，`Init()` 惰性构造真实控件，`TryInit(element)` 静态辅助，`implicit operator T` 便于直接使用；用于 `MyPageLeft` 动画时按需初始化。

- **布局框架**：
  - `MyPageLeft`（`class MyPageLeft : Grid`）：所有左栏页面的基类（`PageDownloadLeft`/`PageSetupLeft`/`PageInstanceLeft` 等）。提供 `AnimatedControl`（逐个进入动画的容器）、`TriggerShowAnimation()`/`TriggerHideAnimation()`（页面切换动画，配合主窗口 `PageChangeAnim`），并在 `Loaded` 时按 `LocalizationService.UiRevision` 刷新本地化（`RefreshLocalization` 虚方法）。`IRefreshable` 接口（`Refresh()`）定义可刷新页面契约（主窗口 `F5` 调用）。
  - `MyPageRight`（`class MyPageRight : AdornerDecorator`）：所有右栏页面的基类。含 `PageStates` 枚举（如 `ContentStay`）、`PanScroll`（`MyScrollViewer`）、`PageState`、`PageLoaderInit(loaderUi, panLoader, ...)`（统一装载加载/错误 UI）、`PageOnEnter()`/`PageOnExit()`/`PageOnForceExit()`/`PageOnContentExit()`（生命周期钩子，由主窗口在切换时调用）、`PageEnter`/`PageExit` 事件、`TriggerEnterAnimation()`/`TriggerExitAnimation()`；`disabledPageAnimControls` 用于跳过动画。

- **视觉/媒体/本地化辅助**：
  - `AnimatedBackgroundGrid`（`class AnimatedBackgroundGrid : Grid`）：带背景画刷动画的网格，`BackgroundBrush` 依赖属性与 `uuid`；是 `MyCard` 的基类。
  - `ControlVisualHelpers`（`internal static class`）：控件视觉辅助方法。
  - `SvgIconControlHelper`（`internal static class`）：SVG 图标加载/渲染辅助（供各 `* SvgIcon` 属性使用）。
  - `TagPanelHelper`（`internal static class`）：标签面板辅助，`FillLocalized()`/`FillPlain()` 用于向 `MyListItem.PanTags` 填充本地化/纯文本标签。
  - `FontSelector`（`partial class FontSelector`）：字体选择器，`CustomFontCollection`（`CustomFontProperties`）、`SelectedFontTag`/`SelectedIndex`/`IsEnabled` 与 `SelectionChanged` 事件。
  - `LocalizationBindingExtensions`（`public static class`）：XAML 本地化绑定扩展，`SetLocalizedValue()`/`TrySetLocalizedValue()`/`CreateLocalizationBinding(key)`，把 `Lang.Text(key)` 结果绑定到依赖属性。
  - `LocalizedText`（`public sealed class LocalizedText : INotifyPropertyChanged`）：可绑定的本地化文本值，`Value` 惰性求值，`Bind()`/`TryBind()` 将控件属性与本地化键关联，语言切换时自动更新。

- **服务器展示**：
  - `MinecraftServer`（`partial class MinecraftServer : Grid`）：服务器卡片，含 `Address` 与 `UpdateServerInfoAsync(address)`（异步拉取服务器 MOTD/状态）。
  - `MinecraftServerQuery`（`partial class MinecraftServerQuery : Grid`）：服务器状态查询子控件（配合 `MinecraftServer`）。

## 本目录直接代码的运行流程

1. **控件实例化**：页面 XAML 引用这些控件 → WPF 加载模板（`*.xaml`）并构造逻辑类（`*.xaml.cs`）。
2. **本地化**：`LocalizedText`/`LocalizationBindingExtensions` 在绑定阶段将 `Lang.Text(key)` 结果注入；语言切换时由 `LocalizationService.UiRevision` 驱动 `MyPageLeft.RefreshLocalization` 与本地化属性更新。
3. **页面切换动画**：主窗口 `PageChangeAnim` 调用 `MyPageLeft.TriggerShowAnimation/HideAnimation` 与 `MyPageRight.PageOnEnter/Exit`，逐个控件播放进入/退出动画。
4. **交互**：按钮 `Click`、单选 `SetChecked`/`Check`、滑块 `DragDoing/DragStop`（经主窗口 `DragTick`）、下拉 `TextChanged` 等事件向上冒泡到页面逻辑。

## 本目录直接代码的输入输出规范

- **输入（XAML 属性）**：各控件通过依赖属性接收配置（`Text`、`Checked`、`Value`、`HintText`、`Tags`、`SvgIcon`、`ColorState` 等）；数据型控件（`MyListItem`）经 `tag`/`Tags`/`PlainTags` 承载业务数据。
- **输入（用户）**：鼠标/键盘/拖放事件，转化为 `Click`/`Check`/`Changed`/`Change`/`Search`/`ValidatedTextChanged` 等事件。
- **输出**：UI 渲染、动画（`Animations.Aa*`）、用户提示经 `HintService`/`MyToast` 由页面层调用；`MyImage` 输出缓存图片文件（`pathTemp` 下，14 天有效）。
- **契约**：`IMyRadio` 是导航/单选的统一契约；`IRefreshable` 是页面刷新契约；`ILoadingTrigger` 是加载状态驱动契约。

## 本目录直接代码的使用约束与注意事项

- 控件均为纯 UI，**不含业务逻辑**，业务判定应放在 `Pages`/`Modules`，控件只负责展示与事件回调。
- `MyExtraButton.showCheck` 返回 `false` 时按钮隐藏；新增主窗口附加按钮必须同时注册 `showCheck` 否则永远显示或永不显示。
- `MySlider` 的拖动依赖主窗口 `FormMain.DragTick`/`DragStop` 全局调度，控件自身不处理鼠标移出窗口的释放。
- 本地化文本务必走 `LocalizedText`/`SetLocalizedValue`，不要硬编码，否则语言切换不更新；`MyPageLeft` 的 `RefreshLocalization` 重写需注意只刷新本页文本。
- `MyImage.EnableCache` 默认开启 14 天缓存，下载失败回退到 `FallbackSource`；大量远程图标会占用临时目录空间。
- `MyPageLeft`/`MyPageRight` 是页面基类，子页面应继承并实现 `PageGet`/生命周期钩子，不应直接 new 后不挂入 `PanMainLeft`/`PanMainRight`。
- `MyVirtualizingElement` 的 `Init()` 为惰性，重复访问会重复初始化，需确保只 `Init` 一次或经 `TryInit`。

## 子目录导航

- [Behaviors](./Behaviors/agent.md)：WPF 交互行为（剪贴板拦截、控件懒加载）。
- [MyMsg](./MyMsg/agent.md)：自定义消息框系列（文本/输入/选择/Markdown/认证服务器）。

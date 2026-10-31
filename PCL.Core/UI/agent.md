# UI 目录

UI 框架层，位于命名空间 `PCL.Core.UI`。提供可被 WPF 绑定的核心 UI 模型（颜色、变换、图标、消息框、系统对话框、Toast、背景呈现）以及动画引擎、控件、转换器、模糊特效与主题。资源（图标/图片/着色器）作为资源目录纳入编译，不在此文档化。

## 本目录模块

- **NColor.cs** — 可动画颜色模型 `NColor`：在 RGB 与 LAB 色彩空间之间转换，支持 `FromHsl` 构造及值相等比较（`Equals`/`GetHashCode`），供动画系统对颜色做插值。
- **NRotateTransform.cs / NScaleTransform.cs** — 可动画的旋转/缩放变换模型 `NRotateTransform` / `NScaleTransform`：包装角度/缩放值并支持相等比较，作为 `*FromToAnimation` 的动画目标。
- **IconManager.cs / IconModel.cs** — 图标管理与图标模型：`IconManager` 提供 `SetSelectedIconByName`、`AddIconFromXaml`、`RemoveIconByName`、`TryLoadIconFromXaml`、`LoadIconFromXaml`，统一图标的加载、缓存与切换；`IconModel` 描述单个图标。
- **ImageLoaderHelper.cs** — 图片异步加载辅助：`SetServerLogoAsync`（服务器图标）、`SetImageFromBase64Async`、`SetImageFromFileAsync`、`SetFallbackImage`，负责把外部图片源安全地加载为 UI 图像并提供回退图。
- **MsgBoxWrapper.cs** — 消息框包装：`Show` 与 `ShowWithCustomButtons` 统一弹窗 API，配合 `MsgBoxButtonInfo`（按钮描述）与 `MsgBoxTheme`（主题）使用。
- **SystemDialogs.cs** — 系统对话框：`SelectFile` / `SelectFiles` / `SelectFolder` / `SelectSaveFile`，封装 Win32 文件选择与保存对话框。
- **ToastNotification.cs** — Toast 通知：`SendToast`、`SendToastFromTemplate`、`UninstallToasts`，基于 WinRT Toast 接口（见 `Utils/WinRT/Interface`）。
- **HintWrapper.cs** — 轻量提示气泡：`Show` 配合 `HintTheme` 在界面上展示短提示。
- **BackgroundPresenter.cs** — 背景呈现器：`DrawBackground` / `ForceRender` 及 `OnRender` 等重写，负责动态背景/壁纸的绘制与刷新。
- **GrayProfile.cs / GrayProfileConfig.cs** — 灰度（置灰）档案与配置：`GrayProfile` 描述置灰方案，`GrayProfileConfig.CurrentProfile` 提供当前生效配置（特殊主题/哀悼模式）。

## 直接代码要点

- **运行流程**：图标经 `IconManager` 加载/缓存后由 `IconModel` 暴露；图片经 `ImageLoaderHelper` 异步加载并设置回退；弹窗统一走 `MsgBoxWrapper` / `HintWrapper` / `SystemDialogs`；通知走 `ToastNotification`；背景由 `BackgroundPresenter` 在渲染管线中绘制。
- **输入输出规范**：图片加载以文件路径/Base64/URL 为输入，输出 WPF `ImageSource`；对话框返回用户选择的路径集合；Toast 接受文本/模板并触发系统通知。
- **使用约束与注意事项**：多数类型依赖 WPF 与 UI 线程（图像、弹窗、渲染须在 STA/UI 线程调用）；WinRT Toast 需要系统支持与相应互操作接口，失败时应降级；`ImageLoaderHelper` 的网络加载需处理超时与异常。

## 子目录导航

- [Animation](./Animation/agent.md)：动画引擎。
- **Assets**（资源目录，无源码：图标/图片/着色器资源文件，含 IconPacks、Shaders 等子目录，未单独文档化）。
- [Controls](./Controls/agent.md)：自定义控件。
- [Converters](./Converters/agent.md)：值转换器。
- [Effects](./Effects/agent.md)：模糊特效。
- [Media](./Media/agent.md)：媒体/图片转换。
- [Theme](./Theme/agent.md)：主题服务与颜色资源。

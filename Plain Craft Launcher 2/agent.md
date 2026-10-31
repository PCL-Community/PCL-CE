# Plain Craft Launcher 2

PCL2（Plain Craft Launcher 2）的 **WPF 图形界面客户端主程序**，Windows 平台 Minecraft 启动器。

- 技术栈：C# / .NET（`net10.0-windows`）、WPF（`UseWPF`）、`WinExe`，根命名空间 `PCL`。
- 依赖：`PCL.Core`（`..\PCL.Core\PCL.Core.csproj`）提供应用生命周期、IoC、日志、本地化、UI 主题等基础设施（本项目的 `Program`/`Application`/`FormMain` 均直接调用 `PCL.Core.App` 命名空间下的服务）。
- 资源：`.csproj` 中将 `Images\**` 与 `Resources\**` 作为嵌入资源（`Resource`）；`metadata.json` 作为内嵌资源。
- 配置：支持 `Debug / CI / Release / Beta` 多配置与 `AnyCPU / x64 / ARM64` 平台；启用单文件发布。

## 本目录直接文件

本目录为客户端外壳层（Shell），负责进程启动、应用生命周期接入、主窗口与全局页面导航，自身不含业务功能模块（业务分散在 `Controls`/`Modules`/`Pages` 子目录）。三个直接代码文件分工如下：

- `Program.cs`：程序入口 `Main()`。
  - 职责：进程启动的唯一起点。
  - 关键逻辑：
    - 解析命令行参数：含 `--console` 时调用 `KernelInterop.AllocateConsole()` 分配控制台；`DEBUG` 模式下含 `--debug` 时挂起等待调试器 attach。
    - 通过 `ApplicationService.Loading` 与 `MainWindowService.Loading` 两个委托，分别注册 `Application` 与 `FormMain` 的构造器（延迟实例化，由 `PCL.Core.App` 生命周期框架在合适时机调用）。
    - 处理 .NET Core 3.0+ 已知的平板设备（Tablet）初始化问题：提前访问 `Tablet.TabletDevices`（来自 dotnet/wpf #2393）。
    - 最后调用 `Lifecycle.OnInitialize()` 启动应用生命周期（后续由 `Application` 构造函数中注册的生命周期回调接管）。
  - 角色：纯引导层，不直接参与 UI，只把"如何创建 App / 主窗口"告知 `PCL.Core`，再启动生命周期。

- `Application.xaml.cs`：`Application` 分部类（WPF 应用对象），实现 `PCL.Core.App` 约定的生命周期回调。
  - 职责：承接 `Lifecycle.OnInitialize()` 之后的"应用级"初始化，并兜底全局异常。
  - 构造函数：通过 `Lifecycle.When(...)` 注册两个状态回调：
    - `LifecycleState.Loaded` → `_ApplicationStartup`（应用启动初始化）。
    - `LifecycleState.WindowCreated` → `_ShowEnvironmentWarning`（窗口创建后弹出环境告警）。
    - 同时订阅 `SessionEnding`，系统在关机/注销时优雅关闭程序（`EndProgram(false)`）。
  - 关键方法：
    - `_ApplicationStartup()`：编码提供器注册（`CodePagesEncodingProvider`）、Binding 失败监听（`BindingErrorTraceListener`）、线程优先级、WPF 字体校验（`StartupValidation.EnsureWpfFont`）；解析 `--gpu` 参数调整显卡偏好（`Launcher.SetGpuPreference`）后退出；创建 `PCL\Pictures`、`PCL\Musics`、临时目录 `Cache`/`Download`、`pathAppdata` 等文件结构；设置 `ToolTipService` 初始延迟并启用 `Tooltip`；按需显示启动 Logo（`SplashScreen`）；预热常用 `Config`（调试/网络/下载/偏好设置等），并在更新通道未配置时按版本名 `beta` 决定 `Beta`/`Release`；清理旧日志 `Log-CE{i}.log`；最后启动动画系统 `Animations.AniBeginControl()`。初始化异常时弹出错误并 `FormMain.EndProgramForce(...Exception)`。
    - `_ShowEnvironmentWarning()`：检测低版本系统（Build < 17763）、32 位系统、位于临时目录、位于微信/Tencent 文件目录等问题，命中时通过 `Launcher.MyMsgBox` 警告用户。
    - `Application_DispatcherUnhandledException`：全局未处理异常兜底，`e.Handled = true` 后做环境类错误的自动分析（如 `Dispatcher.Invoke`、`ITaskbarList.HrInit`、程序集加载失败），提示升级 .NET 运行时并上报反馈；最外层 `try/catch` 保证 handler 自身不崩溃。
    - `BindingErrorTraceListener`：自定义 `TraceListener`，把 WPF 绑定失败写入日志，便于定位 XAML 绑定问题。
  - 角色：客户端与 `PCL.Core` 生命周期/本地化/日志/UI 主题之间的胶水层。

- `FormMain.xaml(.cs)`：主窗口（`Window`），客户端 UI 的顶层容器与全局导航中枢。
  - 职责：窗口创建与初始化、DWM 自定义窗口边框/缩放、全局键盘与拖放、主/子页面切换（`PageChange`）、附加按钮（更新重启/音乐/任务管理/游戏日志/返回顶部/关闭 MC/愚人节）逻辑、窗口隐藏与置顶、程序正常/强制退出。
  - 关键成员：
    - 构造 `FormMain()`：记录启动计时，订阅主题变更（`ThemeService`）、设置 `Launcher.frmMain` 与 `Navigation`，预先构造启动左右栏（`PageLaunchLeft`/`PageLaunchRight`）；根据 `States.System.LastVersion` 与 `Common.VersionCode` 触发 `UpgradeSub`（升级，含遥测/主页预设重置、更新日志、社区版公告）或 `DowngradeSub`（降级）；登记拖放事件、MsgBox/Hint 事件、语言变更；`InitializeComponent()` 后恢复窗口尺寸（`States.UI`），管理员权限下用 `DragHelper` 处理文件拖放；装载左右栏到 `PanMainLeft`/`PanMainRight`；尽早启动 `GameFolders.McFolderListLoader`（供下载已存在检测与启动按钮就绪）；注册 `Lifecycle.When(WindowCreated, FormMain_Loaded)`。
    - `FormMain_Loaded()`：装载完成后读取设置（`PageSetupUI.BackgroundRefresh`）、音乐、初始化附加按钮的 `showCheck` 可见性判定、窗口缩放器、应用主题（`ThemeManager.ThemeRefresh`/`Setup.ApplyAll`）、模糊背景资源、入场动画、启动主 Timer（`Launcher.TimerMainStart`），并在后台线程依次处理特殊版本提示、EULA 同意、遥测同意、启动各加载池（`GameDownload.dlClientListMojangLoader`、`UpdateManager.serverLoader`、清理临时任务）、采集硬件信息（`HardwareInfo.GetHardwareInfo`）。
    - 自定义窗口（`#region 自定义窗口`）：重写 `WM_NCHITTEST`（`_SizeWndProc`）实现 DWM 边框命中区，使缩放手感正常；`OnSourceInitialized` 设置软件渲染降级、透明背景、`ExtendFrameIntoClientArea`；`EndProgram(sendWarning, isUpdating)` / `EndProgramForce(...)` 实现正常与强制退出（含动画、保存日志、更新重启、退出码）。
    - 窗体事件（`#region 窗体事件`）：键盘事件 `FormMain_KeyDown`（回车/Esc 操作弹窗、Esc 返回、`F11` 切换隐藏实例、`F12` 切换隐藏功能、`F5` 刷新页面、启动页回车启动游戏、屏蔽 Alt 系统菜单）；鼠标侧键返回；`TriggerPageBack`；`FormMain_Activated`（切回窗口时按当前页刷新 Mod/资源包/光影/原理图/实例/数据包）；文件拖放 `HandleDrag`/`FrmMain_Drop`/`FileDrag`（支持主页 XAML、Mod/整合包/存档/资源包/光影/原理图/错误报告等多种拖入行为，最终调用 `Modpack.ModpackInstall`、`PageInstanceCompResource.InstallMods`、或 `CrashAnalyzer` 自助分析）；`WndProc`（系统时间变更、置顶请求、`WM_SETTINGCHANGE` 系统主题切换）。
    - 页面切换（`#region 切换页面`）：核心枚举 `PageType`（Launch/Download/Tools/Setup/InstanceSelect/TaskManager/InstanceSetup/CompDetail/HelpDetail/GameLog/VersionSaves 等）与 `PageSubType`（下载/设置/工具/实例各子页的编号，**数值须与左栏 `StackPanel` 下标一致**）；页面状态保存在 `pageCurrent`/`pageLast`/`PageCurrentSub`/`pageStack` 与 `PageStackData`（含 `additional` 透传资源项目/版本/加载器/存档路径等）；`PageChange`、`PageChangeActual`、`PageChangeAnim`、`PageChangeExit` 等实现主/子页面切换动画与左右栏装载，并经由 `NavigationService.RaiseNavigated` 向 `PCL.Core` 导航系统广播（`RouteKey(page, sub)`）。
    - 控件拖动（`#region 控件拖动`）：`DragTick`/`DragDoing`/`DragStop` 维护全局 `Launcher.dragControl` 的滑块拖动生命周期。
    - 附加按钮（`#region 附加按钮`）：更新重启、音乐暂停/切歌、任务管理入口、愚人节投降、关闭运行中的 Minecraft（`GameLaunch`/`GameWatcher`）、游戏日志入口、返回顶部（`BackToTop`）等，均通过 `showCheck` 实现按需显示。
  - 角色：客户端"外壳"，统一承载所有 `Pages` 与 `Controls`，并通过 `PCL.Core` 的 `NavigationService` 与生命周期对外暴露导航。
  - 数据/调用关系：大量引用 `Launcher`（全局静态入口，含 `frmMain`/`frmLaunchLeft`/`frmDownloadLeft` 等页面实例缓存）、`Common`、`Config`、`States`、`Lang`、`Animations`、`ThemeService`/`ThemeManager`、`Lifecycle`、`UpdateManager`、`GameLaunch`/`GameWatcher`、`Modpack`、`CrashAnalyzer`、`NetFacade`；页面具体逻辑分散于 `Pages` 子目录，本文件只负责装载与切换。

## 本目录直接代码的运行流程

1. **进程启动**：`Program.Main()` → 解析命令行 → 注册 `ApplicationService.Loading`/`MainWindowService.Loading` → `Lifecycle.OnInitialize()`。
2. **应用初始化**：`PCL.Core` 调用 `Application` 构造 → 注册 `Loaded`/`WindowCreated` 回调 → 触发 `_ApplicationStartup`（编码/字体/目录/配置预热/Logo）→ 创建 `FormMain` 并 `InitializeComponent()`。
3. **窗口装载**：`FormMain_Loaded` 读取设置/音乐/主题、启动 Timer、后台弹出 EULA/遥测/加载池/硬件信息，最后播放入场动画并 `ShowWindowToTop()`。
4. **日常运行**：用户通过导航栏/返回/键盘/拖放触发 `PageChange`/`FileDrag` 等，主窗口装载对应 `Pages` 子页面；`WndProc`/`SessionEnding`/`DispatcherUnhandledException` 处理系统级事件与崩溃兜底。
5. **退出**：`EndProgram` → 动画收尾 → `EndProgramForce` → `Lifecycle.Shutdown(returnCode, force)`。

## 本目录直接代码的输入输出规范

- **输入（命令行）**：`--console`（分配控制台）、`--debug`（DEBUG 下等待调试器）、`--gpu <path>`（调整显卡偏好后退出）。其他参数由 `PCL.Core` 与子模块解析。
- **输入（系统/用户）**：`WM_*` 窗口消息、`SessionEnding`、键盘/鼠标/拖放事件、来自 `Pages` 的导航请求。
- **输出**：日志（`Common.Log`/`LogWrapper`）、用户提示（`HintService`/`Launcher.MyMsgBox`）、窗口 UI、`Lifecycle.Shutdown` 退出码（`Common.ProcessReturnValues`）。
- **对外契约**：通过 `PCL.Core.App` 的 `ApplicationService`/`MainWindowService`/`Lifecycle`/`NavigationService` 暴露创建与导航接口；`PageType`/`PageSubType`/`PageStackData` 视为公开导航 API 的一部分。

## 本目录直接代码的使用约束与注意事项

- `PageSubType` 的数值**必须与左栏 `StackPanel` 中列表项的下标保持一致**，改动布局或枚举需同步。
- 主窗口初始化时 `isSizeSaveable` 为 `false`，避免初始化阶段把临时尺寸写入 `States.UI`；恢复窗口尺寸失败会回退到 `Min + 100`。
- 窗口边缘命中区（`_SizeWndProc`）与 DWM 透明边框依赖 `OnSourceInitialized` 中的 `ExtendFrameIntoClientArea`，禁用硬件加速时改为软件渲染。
- `EndProgram` 有约 0.3s 的退场动画，强制退出（`EndProgramForce(force:true)`）由异常兜底或升级重启调用，二者不应被随意混用。
- 全局异常 handler 内再次 `try/catch`，确保其自身不会二次崩溃；环境类错误会引导用户升级 .NET 运行时（https://get.dot.net/10）。
- 管理员权限模式下文件拖放走 `DragHelper` 的 `WndProc` Hook 路径，与普通模式不同。
- 资源目录 `Images`/`Resources` 仅作嵌入资源，无源码，导航中列为资源目录而非可点击 agent 链接。

## 子目录导航

- [Controls](./Controls/agent.md)：自定义 WPF 控件与消息框。
- [Modules](./Modules/agent.md)：业务功能模块（基础、事件、Minecraft、网络、UI、更新）。
- [Pages](./Pages/agent.md)：界面页面（下载、主页、实例、启动、设置、工具）。
- `Images`（资源目录，无源码：背景图、方块、头像、图标、皮肤等图片资源）。
- `Resources`（资源目录，无源码：提示文本 `hints` 等静态资源）。

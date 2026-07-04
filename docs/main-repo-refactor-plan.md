# PCL N 主仓库重构计划

本文档用于规划 PCL N 主仓库在插件系统闭源拆分前需要完成的架构重构。

核心目标：**主仓库只提供开放扩展面，不主动依赖闭源 `PCL.Plugin`。**

---

## 1. 核心原则

### 1.1 Desktop 不依赖 Plugin

禁止依赖方向：

```text
PCL.Desktop -> PCL.Plugin
```

推荐依赖方向：

```text
PCL.Desktop
  -> PCL.Application
  -> PCL.Platform
  -> PCL.UI.Abstractions

PCL.Application
  -> PCL.Core.Portable
  -> PCL.Domain
  -> PCL.Platform.Abstractions
```

闭源插件仓库后续依赖主仓库抽象：

```text
PCL.Plugin.Hosting
  -> PCL.Application
  -> PCL.UI.Abstractions
  -> PCL.Platform.Abstractions
```

主仓库负责“可扩展”，闭源 Plugin 仓库负责“如何加载和管理扩展”。

---

## 2. 主仓库职责边界

### 2.1 主仓库负责

```text
核心业务能力
平台抽象能力
UI 抽象能力
Host Module 接入口
可扩展注册表
默认内置实现
内置页面和内置业务模块
```

### 2.2 主仓库不负责

```text
第三方插件目录扫描
插件 Manifest 解析
插件签名校验
插件沙箱
插件权限 UI
插件市场
插件更新
第三方 DLL 依赖解析
```

---

## 3. Extension 与 Plugin 的概念拆分

主仓库只定义 Extension 能力，闭源仓库实现 Plugin 系统。

```text
Extension = 主仓库开放的扩展能力
Plugin    = Extension + 加载器 + Manifest + 权限 + 隔离 + 签名 + 市场 + 更新
```

主仓库只需要理解：

```text
Host Module
Navigation Registry
Command Registry
Launch Pipeline
Account Provider Registry
Download Source Registry
Settings Registry
UI Abstractions
```

---

## 4. 阶段 M1：移除 Desktop 对 Plugin 的编译期依赖

### 4.1 修改 `PCL.Desktop/PCL.Desktop.csproj`

删除：

```xml
<ProjectReference Include="../PCL.Plugin/PCL.Plugin.csproj" />
```

保留：

```xml
<ProjectReference Include="../PCL.Application/PCL.Application.csproj" />
<ProjectReference Include="../PCL.Platform/PCL.Platform.csproj" />
<ProjectReference Include="../PCL.UI.Abstractions/PCL.UI.Abstractions.csproj" />
```

### 4.2 验收标准

```text
PCL.Desktop 可在不引用 PCL.Plugin 的情况下编译
PCL.Desktop 中不存在 using PCL.Plugin
Desktop 不参与插件加载
Desktop 只消费 Application / UI 抽象
```

---

## 5. 阶段 M2：建立 Host Module 机制

建议放置位置：

```text
PCL.Application/Hosting/
```

### 5.1 `IPclHostModule`

```csharp
public interface IPclHostModule
{
    string Id { get; }

    void Configure(IPclHostBuilder builder);
}
```

### 5.2 `IPclHostBuilder`

```csharp
public interface IPclHostBuilder
{
    IServiceRegistry Services { get; }
    INavigationRegistry Navigation { get; }
    ICommandRegistry Commands { get; }
    ISettingsRegistry Settings { get; }
    ILaunchPipelineBuilder Launching { get; }
}
```

### 5.3 `IPclHost`

```csharp
public interface IPclHost
{
    IServiceProvider Services { get; }
    INavigationRegistry Navigation { get; }
    ICommandRegistry Commands { get; }
}
```

### 5.4 设计说明

`IPclHostModule` 是主仓库公开的最小模块入口。闭源 `PCL.Plugin.Hosting` 后续只是一个外部 `IPclHostModule` 实现。

### 5.5 验收标准

```text
内置功能可以通过 IPclHostModule.Configure() 注册
Desktop 启动时从 IPclHost 获取导航、命令和服务
主仓库内没有插件加载器语义
```

---

## 6. 阶段 M3：建立 UI Abstractions 注册表

建议目录：

```text
PCL.UI.Abstractions/
  Navigation/
    INavigationRegistry.cs
    INavigationService.cs
    NavigationPageDescriptor.cs

  Commands/
    ICommandRegistry.cs
    ICommandContext.cs
    CommandDescriptor.cs

  Pages/
    IPageProvider.cs
    PageCreateContext.cs
    PageRegion.cs

  Dialogs/
    IDialogService.cs
    DialogRequest.cs

  Notifications/
    INotificationService.cs

  Themes/
    IThemeRegistry.cs
    ThemeDescriptor.cs
```

### 6.1 `INavigationRegistry`

```csharp
public interface INavigationRegistry
{
    IReadOnlyList<NavigationPageDescriptor> Pages { get; }

    void AddPage(NavigationPageDescriptor descriptor);
    bool RemovePage(string route);
    bool ReplacePage(string route, NavigationPageDescriptor descriptor);
}
```

### 6.2 `NavigationPageDescriptor`

```csharp
public sealed record NavigationPageDescriptor
{
    public required string Route { get; init; }
    public required string Title { get; init; }
    public string? Icon { get; init; }
    public int Order { get; init; }
    public required IPageProvider Provider { get; init; }
}
```

### 6.3 `IPageProvider`

```csharp
public interface IPageProvider
{
    ValueTask<object> CreatePageAsync(
        PageCreateContext context,
        CancellationToken cancellationToken);
}
```

### 6.4 注意事项

```text
PCL.UI.Abstractions 不应该引用 Avalonia
IPageProvider 返回 object 是为了保持 UI 框架无关
PCL.Desktop 负责把 object 适配为 Avalonia Control
```

### 6.5 验收标准

```text
导航页来自 INavigationRegistry，不再由 MainWindow 写死
命令来自 ICommandRegistry，不再散落在控件事件里
UI 抽象层不依赖 Avalonia
```

---

## 7. 阶段 M4：建立 Application 业务扩展面

建议目录：

```text
PCL.Application/
  Extensions/
    IServiceRegistry.cs
    IExtensionRegistry.cs

  Launching/
    ILaunchPipeline.cs
    ILaunchPipelineBuilder.cs
    ILaunchMiddleware.cs
    LaunchRequest.cs
    LaunchContext.cs

  Accounts/
    IAccountProvider.cs
    IAccountProviderRegistry.cs

  Downloads/
    IDownloadSource.cs
    IDownloadSourceRegistry.cs

  Settings/
    ISettingsRegistry.cs
    SettingDescriptor.cs
```

### 7.1 启动流程 Pipeline

```csharp
public delegate ValueTask LaunchPipelineNext(
    LaunchContext context,
    CancellationToken cancellationToken);

public interface ILaunchMiddleware
{
    ValueTask InvokeAsync(
        LaunchContext context,
        LaunchPipelineNext next,
        CancellationToken cancellationToken);
}
```

### 7.2 注册方式

```csharp
builder.Launching.Use<DefaultLaunchMiddleware>();
builder.Launching.Use<JvmArgumentPatchMiddleware>();
builder.Launching.Use<GameProcessObserverMiddleware>();
```

### 7.3 验收标准

```text
启动参数构建、账号选择、进程启动、日志监听可以由中间件扩展
第三方后续可以通过闭源 Plugin Host 注册 Launch Middleware
Application 仍然不依赖 Plugin
```

---

## 8. 阶段 M5：MainWindow 去业务化

`MainWindow` 最终只负责：

```text
渲染导航
渲染页面
执行命令
播放动画
承载对话框
窗口生命周期
```

不再负责：

```text
启动业务
账号业务
下载源业务
插件业务
页面工厂
插件页面适配
```

### 8.1 目标结构

```csharp
public sealed partial class MainWindow : Window
{
    private readonly INavigationRegistry _navigation;
    private readonly ICommandRegistry _commands;
    private readonly IDesktopPageAdapter _pageAdapter;

    public MainWindow(
        INavigationRegistry navigation,
        ICommandRegistry commands,
        IDesktopPageAdapter pageAdapter)
    {
        InitializeComponent();

        _navigation = navigation;
        _commands = commands;
        _pageAdapter = pageAdapter;

        BuildNavigation();
        NavigateToDefaultPage();
    }
}
```

### 8.2 Desktop 页面适配接口

```csharp
public interface IDesktopPageAdapter
{
    ValueTask<Control> CreateControlAsync(
        IPageProvider provider,
        PageCreateContext context,
        CancellationToken cancellationToken);
}
```

### 8.3 验收标准

```text
MainWindow 中不再写死导航页字典
内置页面与外部页面走同一套 NavigationPageDescriptor
MainWindow 不知道页面来自内置模块、闭源 Plugin Host 还是其他 Host Module
```

---

## 9. 阶段 M6：内置功能模块化

内置功能也应走 Host Module，而不是特殊硬编码。

### 9.1 示例

```csharp
public sealed class BuiltInLaunchModule : IPclHostModule
{
    public string Id => "pcl.builtin.launch";

    public void Configure(IPclHostBuilder builder)
    {
        builder.Navigation.AddPage(new NavigationPageDescriptor
        {
            Route = "pcl.launch",
            Title = "启动",
            Icon = "lucide/play",
            Order = 0,
            Provider = new LaunchPageProvider()
        });

        builder.Launching.Use<DefaultLaunchMiddleware>();
    }
}
```

### 9.2 建议内置模块

```text
pcl.builtin.launch
pcl.builtin.download
pcl.builtin.settings
pcl.builtin.accounts
pcl.builtin.online
pcl.builtin.community
```

### 9.3 验收标准

```text
删除硬编码页面注册
内置页面和第三方页面统一排序、统一导航、统一生命周期
可以在测试中只启用部分内置模块
```

---

## 10. 阶段 M7：提供极薄外部 Host Module 加载器

建议放置位置：

```text
PCL.Application.Hosting/HostModuleLoader.cs
```

### 10.1 只负责

```text
读取配置里的 Host Module 程序集路径
加载程序集
查找 IPclHostModule
调用 Configure
```

### 10.2 不负责

```text
插件 Manifest
插件签名
插件权限
插件依赖解析
插件市场
插件更新
插件沙箱
```

### 10.3 配置示例

```json
{
  "hostModules": [
    "PCL.Plugin.Hosting/PCL.Plugin.Hosting.dll"
  ]
}
```

### 10.4 验收标准

```text
主仓库可以加载任意 IPclHostModule
主仓库不理解 pcl-plugin.json
闭源 Plugin Host 可以作为普通 Host Module 接入
```

---

## 11. 主仓库里程碑

| 阶段 | 目标 | 验收标准 |
|---|---|---|
| M1 | 移除 Desktop -> Plugin 引用 | `PCL.Desktop` 不再引用 `PCL.Plugin` |
| M2 | 建立 HostBuilder / HostModule | 内置功能可通过 Host Module 注册 |
| M3 | 建立 UI 注册表 | 导航页来自 `INavigationRegistry` |
| M4 | 建立业务扩展面 | 启动、账号、下载源可注册扩展 |
| M5 | MainWindow 去业务化 | MainWindow 只负责渲染和窗口行为 |
| M6 | 内置页面模块化 | 内置页面和外部页面走同一机制 |
| M7 | 外部 Host Module 加载 | 可加载闭源 `PCL.Plugin.Hosting.dll` |
| M8 | 测试与兼容 | Application/UI Abstractions 有单元测试 |

---

## 12. 最终判断

主仓库的最终目标不是实现完整插件系统，而是提供稳定、清晰、低耦合的扩展面。

只要保持以下三条约束，后续闭源 Plugin 仓库就可以独立演进：

```text
Desktop 不引用 Plugin
Application/UI Abstractions 提供扩展点但不提供插件加载器
Plugin 仓库依赖主仓库抽象并在运行时注册能力
```

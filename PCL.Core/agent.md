# PCL.Core 模块

PCL.Core 是 PCL2（Plain Craft Launcher 2，Windows 平台的 Minecraft 启动器）的共享核心库，基于 C#/.NET 10 + WPF 构建。它封装了启动器运行所需的全部基础设施与业务逻辑，供 PCL 主程序（如 PCL.Launcher）通过依赖注入与生命周期系统（`App.IoC`）消费。

本模块按职责划分为以下子系统，互不耦合、通过事件总线与配置服务协作：

- **App**：应用基础设施（CLI、配置与存储、数据库、事件总线、依赖注入/生命周期、本地化、区域、任务、工具）。
- **IO**：输入输出层（文件/目录工具、下载抽象、网络与 DNS/HTTP/缓存/端口转发、基于哈希与 SQLite 的内容缓存存储）。
- **Link**：联机/P2P 相关（局域网广播、EasyTier 组网、大厅、MC 服务器 ping、脚手架客户端/服务端握手与帧封装）。
- **Logging**：统一的日志服务与 Logger 体系。
- **Minecraft**：Minecraft 领域逻辑（身份认证模型、Java 解析与扫描、启动、游戏档案与认证、资源项目、存档编辑/解析）。
- **Model**：跨层复用的数据模型（目前为首页相关）。
- **UI**：UI 框架（动画引擎、资源/图标、控件、转换器、模糊特效、媒体、主题）。
- **Utils**：通用工具（编解码、诊断、差异、加密、扩展、哈希、OS 互操作、密钥、线程、校验、WinRT 互操作）。
- **ViewModel**：MVVM 视图模型。

## 本目录直接代码

本目录为共享核心库的项目根/程序集层级，直接包含的 `.cs` 文件极少，主要承载程序集级别的声明：

- **AssemblyInfo.cs**：程序集特性声明文件。通过一系列 `[assembly: XmlnsDefinition(...)]` 与 `[assembly: XmlnsPrefix(...)]` 把多个命名空间（`PCL.Core.UI.Animation`、`PCL.Core.UI.Animation.Core`、`PCL.Core.UI.Animation.Easings`、`PCL.Core.Utils.Validate`、`PCL.Core.App.Localization`）统一映射到 XAML 命名空间前缀（`ani`、`val`、`loc`），使 WPF XAML 可直接引用这些类型；同时声明 `[assembly: DisableRuntimeMarshalling]`（关闭运行时封送以兼容 WinRT 互操作）与 `[assembly: InternalsVisibleTo("PCL.Core.Test")]`（让测试项目可访问内部成员）。该文件不含业务类型，仅做程序集元数据配置。

## 子目录导航

- [App](./App/agent.md)：应用基础设施（CLI、配置/存储、数据库、事件总线、IoC/生命周期、本地化、区域、任务、工具）。
- [IO](./IO/agent.md)：IO 层（文件/目录工具、下载、网络、存储与缓存）。
- [Link](./Link/agent.md)：联机/P2P 层（广播、EasyTier、大厅、McPing、脚手架）。
- [Logging](./Logging/agent.md)：日志服务与 Logger 体系。
- [Minecraft](./Minecraft/agent.md)：Minecraft 领域逻辑（认证、Java、启动、档案、资源、存档）。
- [Model](./Model/agent.md)：跨层数据模型。
- [UI](./UI/agent.md)：UI 框架（动画、图标、控件、转换器、特效、主题）。
- [Utils](./Utils/agent.md)：通用工具库。
- [ViewModel](./ViewModel/agent.md)：MVVM 视图模型。

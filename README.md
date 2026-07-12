**简体中文** | [English](README-EN.md) | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="PCL.Desktop/Assets/icon.ico" alt="Logo" width="80" height="80">

# PCL N Edition

[![Stars](https://img.shields.io/github/stars/MuXue1230-owo/PCL-N?style=flat&logo=data:image/svg%2bxml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZlcnNpb249IjEiIHdpZHRoPSIxNiIgaGVpZ2h0PSIxNiI+PHBhdGggZD0iTTggLjI1YS43NS43NSAwIDAgMSAuNjczLjQxOGwxLjg4MiAzLjgxNSA0LjIxLjYxMmEuNzUuNzUgMCAwIDEgLjQxNiAxLjI3OWwtMy4wNDYgMi45Ny43MTkgNC4xOTJhLjc1MS43NTEgMCAwIDEtMS4wODguNzkxTDggMTIuMzQ3bC0zLjc2NiAxLjk4YS43NS43NSAwIDAgMS0xLjA4OC0uNzlsLjcyLTQuMTk0TC44MTggNi4zNzRhLjc1Ljc1IDAgMCAxIC40MTYtMS4yOGw0LjIxLS42MTFMNy4zMjcuNjY4QS43NS43NSAwIDAgMSA4IC4yNVoiIGZpbGw9IiNlYWM1NGYiLz48L3N2Zz4=&logoSize=auto&label=stars&labelColor=444444&color=eac54f)](https://github.com/MuXue1230-owo/PCL-N/)
![GitHub Release](https://img.shields.io/github/v/release/MuXue1230-owo/PCL-N?label=release&logo=github)
[![Issues](https://img.shields.io/github/issues/MuXue1230-owo/PCL-N?style=flat&label=issues&labelColor=444444&color=1F883D&logo=github)](https://github.com/MuXue1230-owo/PCL-N/issues)
[![Pull requests](https://img.shields.io/github/issues-pr/MuXue1230-owo/PCL-N?style=flat&label=pull%20requests&labelColor=444444&color=1F883D&logo=github)](https://github.com/MuXue1230-owo/PCL-N/pulls)
![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/MuXue1230-owo/PCL-N/build-test.yml)
![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/MuXue1230-owo/PCL-N/total)

[下载最新版本](https://github.com/MuXue1230-owo/PCL-N/releases/latest) |
[提交问题](https://github.com/MuXue1230-owo/PCL-N/issues/new/choose) |
[赞助](https://ifdian.net/a/pclne)

</div>

**PCL N Edition**（Plain Craft Launcher N Edition）是由 [MUXUE1230](https://github.com/MuXue1230-owo) 独立开发和维护的 Minecraft 启动器。

当前主线基于 **.NET 10 + Avalonia 12** 重写，面向 Windows / Linux / macOS，提供单文件发布与模块化架构。版本号与 PCL / PCL-CE 主线**并非严格对应**，请不要向其他仓库反馈 PCL N 的问题。

欢迎试用与反馈！

## ✨ 主要特性

- **跨平台桌面壳**：`PCL.Desktop` 基于 Avalonia，支持 win / linux / osx 的 x64 与 arm64
- **模块化核心**：可移植核心、领域模型、应用服务、平台抽象与 UI 抽象分层
- **启动与实例管理**：版本安装、Java 选择、启动参数规划、实例元数据与导出
- **账号体系**：微软正版、离线、第三方 / Authlib-Injector 等登录流程
- **下载与资源**：Minecraft 客户端 / 资源 / 库文件下载规划与任务管理
- **插件宿主**：内置 HostModule 桥接与第三方 `.pnp` 插件运行时（签名校验、隔离加载）
- **发布形态**：自包含（SelfContained）与依赖运行时（NoRuntime）两种产物，发布包附带 GPG 签名

## 🏗 仓库结构

主解决方案：[`PCL-N.slnx`](PCL-N.slnx)

| 项目 | 说明 |
|---|---|
| `PCL.Core.Portable` | 可移植核心原语（IO、工具、Minecraft 协议相关等），Native AOT 友好 |
| `PCL.Domain` | 领域模型 |
| `PCL.Platform.Abstractions` / `PCL.Platform` | 平台能力抽象与默认实现（路径、进程、系统信息、Java 定位等） |
| `PCL.Application` | 应用服务层（账号、下载、实例、启动、设置等） |
| `PCL.UI.Abstractions` | UI 无关的命令、导航、主题、通知等抽象 |
| `PCL.Desktop` | Avalonia 桌面壳与功能页面 |
| `PCL.Desktop.SourceGenerators` | 桌面导航 / 设置 / 下载 / 实例页面注册源生成 |
| `*.Test` / `*.AotSmoke` | 单元测试与 AOT 冒烟测试 |

相关仓库与目录：

- 第三方插件公开契约：[PCL-N-Plugin-SDK](https://github.com/MuXue1230-owo/PCL-N-Plugin-SDK)
- 私有插件运行时与内置 HostModule：`PCL.Plugin/`（见其 README）
- 在线服务端：`PCL.Server/`（独立部署，见其 README）

## 💻 支持平台

| 平台 | 架构 | 支持情况 |
|---|---|---|
| Windows 10 / 11 | x64、ARM64 | ✅ 完整支持 |
| Linux | x64、ARM64 | ✅ 支持（发行版差异请以最新版本实测为准） |
| macOS | x64、ARM64 | ✅ 支持 |
| 更旧的 Windows / 其他系统 | — | ❌ 不保证可用 |

**发布产物命名约定**（Release）：

- `PCL_N_Release_<rid>_SelfContained`：自包含，无需预装 .NET
- `PCL_N_Release_<rid>_NoRuntime`：体积更小，需要本机安装 [.NET 10 运行时](https://dotnet.microsoft.com/download/dotnet/10.0)

其中 `<rid>` 为 `win-x64`、`win-arm64`、`linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64`。

**说明**：

- 我们仅对**最新版本**提供支持
- 建议使用较新的操作系统与显卡驱动以获得最佳体验
- 在不受支持的环境上仍可尝试运行，但可能遇到额外问题

## 🛠 从源码构建

**环境要求**：[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（仓库 `global.json` 固定 SDK 策略）

```powershell
# 还原与构建主解决方案
dotnet restore .\PCL-N.slnx
dotnet build .\PCL-N.slnx -c Release

# 运行桌面启动器（开发）
dotnet run --project .\PCL.Desktop\PCL.Desktop.csproj

# 运行测试
dotnet test .\PCL-N.slnx -c Release

# 发布单文件（示例：Windows x64 自包含）
dotnet publish .\PCL.Desktop\PCL.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\artifacts\win-x64
```

完整多平台发布由 GitHub Actions 的 `release-stable_publish.yml` / `release-beta_publish.yml` 完成。

## 🔒 许可证

本仓库代码默认使用 [Apache License 2.0](LICENSE)。

第三方依赖的许可证信息见 `PCL.Desktop/metadata.json` 与各项目 NOTICE（如有）。

## 🌟 统计数据

![Alt](https://repobeats.axiom.co/api/embed/803751b94f0b1e8682bf9b5aba0f0dd9f2d156fd.svg "Repobeats analytics image")

[![Star History Chart](https://api.star-history.com/svg?repos=MuXue1230-owo/PCL-N&type=Date)](https://www.star-history.com/#MuXue1230-owo/PCL-N&Date)

**此页浏览量**（总计 / 今日）：[![Hits](https://hits.zkitefly.eu.org/?tag=https://github.com/MuXue1230-owo/PCL-N)](https://hits.zkitefly.eu.org/?tag=https://github.com/MuXue1230-owo/PCL-N&web=true)

# PCL Mac版（实验性原生移植）

这是独立的 SwiftUI macOS 启动器原型，参考 PCL 的界面、资源和行为重新实现。原作者为**龙腾猫跃**，可通过[原作者赞助页面](https://meloong.com/afd/a/LTCat)支持 PCL。本目录不是原作者的官方 macOS 发行版，也尚未与 PCL-CE 的 C# 核心集成。

本次将已有的原生端整理为独立目录，供维护者评估是否接纳。Windows 工程不依赖本目录。来源提交、原版资源及许可范围见 [NOTICE](NOTICE) 和 [LICENCE](LICENCE)；部分第三方字体、图片及标志的具体分发权限仍需维护者核对。

## 构建

需要 macOS 14 或更新版本，以及带有 Swift 5.9 或更新工具链的完整 Xcode。游戏运行还需要合适的 Java；启动器目前不会自动安装 Java。

在仓库根目录运行：

```sh
cd macOS
zsh scripts/build-app.sh --universal
open ".build/dist.noindex/PCL Mac版.app"
```

通用应用包含 arm64 和 x86_64。只构建当前机器架构时，运行 `zsh scripts/build-app.sh`。脚本进行本地临时签名，不配置 Developer ID 签名或 Apple 公证，产物保存在已忽略的 `.build/` 中。

也可用 Xcode 打开 `PCLMac.xcodeproj`，选择 `PCLMac` scheme。`project.yml` 是工程配置源；安装 XcodeGen 后在本目录运行 `xcodegen generate` 可重新生成工程。核心库与测试另提供 Swift Package。

常规构建使用已提交的图标，无需旧 Windows 源码目录。原始图标输入和来源固定在 [Support/IconSources](Support/IconSources/README.md)，可离线验证：

```sh
python3 scripts/make-source-icons.py --check
python3 scripts/make-top-navigation-icons.py --check
python3 scripts/make-instance-icons.py --check
python3 scripts/make-version-action-icons.py --check
```

## 使用与范围

1. 在“设置”中检查 Java 与内存。自动检测系统及用户 JDK、Homebrew OpenJDK 和 `JAVA_HOME`，也可手动选择 Java 可执行文件。
2. 在“下载”选择 Minecraft 与组件版本，或导入本地 `.zip` / `.mrpack` 整合包。安装和校验完成后，版本才进入版本列表。
3. 在“版本选择”选择实例，在“版本设置”配置隔离目录、Java、内存、窗口与 JVM 参数，或管理 Mod、修改组件与导出整合包。
4. 在启动页填写离线用户名后启动。支持正版游戏，并仅在有权使用游戏的情况下游玩。

当前包含：

- 原版安装；Forge、NeoForge、Fabric、Quilt、OptiFine、OptiFabric、LiteLoader 与 Fabric API 的下载和修改，组合受游戏与组件版本约束。
- Modrinth、CurseForge、MultiMC / Prism、MCBBS 及完整游戏 ZIP 导入；解压、文件解析与游戏安装并行，按大小及完整内容哈希复用文件，失败或取消时等待分支结束再清理。
- 导出格式与加载器分别识别；CurseForge 缺失文件按原项目的 MCIM 镜像逻辑解析，无需用户填写 API Key。镜像是外部服务，可用性不受本工程控制。
- 社区 Mod、整合包、数据包、资源包与光影搜索、下载及必需前置解析；版本收藏、删除、重命名、独立设置、Mod 管理、完整 ZIP / Modrinth 导出、启动日志与停止游戏。
- 原版矢量与图片资源、自绘下拉框及任务进度。窗口使用左侧原生 macOS 控制按钮，保留 Dock。

默认游戏目录为 `~/Library/Application Support/PCL-SwiftUI/minecraft`，可在设置中更换。实例配置保存为 `versions/<版本>/PCL/macos-settings.json`；隔离实例使用该版本目录，公共资源与依赖库仍共享。新建或导入不会覆盖已有完整实例；删除版本使用系统废纸篓。

## 测试

在本目录运行：

```sh
swift test
```

普通测试使用本地夹具，覆盖离线身份、启动参数、Java 选择、安装校验、继承配置、组件修改与回滚、整合包识别、并行导入、缓存复用、ZIP 路径与校验、社区依赖和导出。真实网络下载、完整游戏安装与图形启动测试默认跳过。

真实集成测试必须显式启用，并指定新的独立测试目录。例如，原版 1.20.4 安装与启动需要本机架构的 Java 17：

```sh
PCL_REAL_SMOKE_GAME_DIR="$PWD/.build/smoke-game" swift test --filter LauncherSmokeTests
```

添加 `PCL_SMOKE_PREPARE_ONLY=1` 可只验证下载和启动计划。其他启用变量见 `Tests/PCLCoreTests/*SmokeTests.swift`；不要指向正在使用的实例。常规 CI 不启用这些测试，也不附带游戏、Mod JAR 或本机验证日志。

旧 LWJGL2 游戏在 macOS 27 上可能在窗口事件后丢失 OpenGL 上下文。本目录附带仅用于相应 Java 8 启动的兼容 agent，源码在 `Support/LegacyOpenGL/`。常规构建使用嵌入数据，不需要 Java 编译器；使用 JDK 8 验证可重现生成：

```sh
python3 scripts/make-legacy-opengl-agent.py --java-home /path/to/jdk8/Contents/Home --check
```

## 已知限制

尚未实现 Microsoft / Xbox 正版登录、PCL 联机、Java 自动安装或 Mod 联网自动更新。离线身份不能连接要求正版验证的服务器。旧版本可能需要 Intel Java 与 Rosetta；并非所有游戏版本、加载器或整合包都已在 macOS 上验证。

暂不支持 ZIP64、加密或分卷 ZIP、MultiMC 自定义补丁与外部启动命令。完整 ZIP 仅包含所选实例当前 macOS 运行所需文件，不含 Windows 启动器。Windows 专属选项及尚未移植的功能保持禁用。

本目录作为独立原型提交；接纳范围、第三方资源权限、后续拆分及与 CE 核心的集成方式均待维护者评估。

## AI 披露

代码、测试、界面移植、构建整理及本文档实质性使用了 Codex 生成和修改。提交者已授权整理和提交；尚未确认由提交者逐文件完成人工审查。文档和 PR 说明由 Codex 按提交者要求代拟，不作“由贡献者独立撰写”或“全部代码已人工审查”的声明。相关状态应在正式请求合并前由贡献者与维护者确认。

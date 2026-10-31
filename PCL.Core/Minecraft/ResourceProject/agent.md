# Minecraft/ResourceProject 目录

资源项目子系统，位于命名空间 `PCL.Core.Minecraft.ResourceProject`。处理 Mod/资源包等外部项目的依赖解析与平台数据模型（CurseForge、Modrinth）。

## 本目录模块

- **`ModDependencyResolver.cs`** — Mod 依赖解析器 `ModDependencyResolver`：`Resolve` 解析依赖图并产出 `ModDependencyResolutionResult`；配套模型 `ModDependencyReference`/`ModDependencyRequest`/`ModDependencyProject`/`ModDependencyFile`/`InstalledModIdentity`/`ResolvedDependencyInstall`/`UnresolvedDependency`/`IgnoredDependency`。`ResolutionContext` 提供 `GetVisitedKey`（去重）、`IsInstalledCompatible`（已装兼容判定）、`AddInstall`/`AddUnresolved`/`AddSatisfied`（结果归集）。

## 直接代码要点

- **运行流程**：`Resolve` 接收依赖请求 → 以 `ResolutionContext` 记录已访问节点避免环/重复 → 对每个依赖判定已安装兼容或加入待安装 → 递归展开子依赖 → 归集为「可安装 / 未解析 / 已满足」三类结果。
- **输入输出规范**：输入为 `ModDependencyRequest`（项目/版本/加载器约束）；输出 `ModDependencyResolutionResult`（`ResolvedDependencyInstall` 列表、`UnresolvedDependency`、`IgnoredDependency`）。
- **使用约束与注意事项**：依赖图可能含环，必须用 visited 集合去重；平台（CurseForge/Modrinth）字段差异由各自模型适配；解析结果不保证网络可用，下载由上层负责；已安装判定需匹配加载器与游戏版本。

## 子目录导航

- [Curseforge](./Curseforge/agent.md)：CurseForge 项目数据模型。
- [Modrinth](./Modrinth/agent.md)：Modrinth 项目数据模型。

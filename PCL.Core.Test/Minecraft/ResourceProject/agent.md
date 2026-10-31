# Minecraft/ResourceProject 测试

本目录测试 PCL.Core 中 Minecraft Mod 资源依赖解析逻辑。

## 本目录测试模块

- `ModDependencyResolverTest.cs` — `ModDependencyResolver.Resolve` 对 `ModDependencyRequest` 的解析行为：缺失必需依赖、递归依赖、共享依赖去重、循环依赖终止、未解析依赖标记、已安装兼容/不兼容依赖处理、可选依赖忽略、按发布类型选取最新兼容文件、跨源（Modrinth/CurseForge）不混配等（`PCL.Core.Minecraft.ResourceProject`）。

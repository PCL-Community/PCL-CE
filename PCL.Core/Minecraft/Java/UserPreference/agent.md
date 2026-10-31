# Minecraft/Java/UserPreference 目录

Java 用户偏好，位于命名空间 `PCL.Core.Minecraft.Java.UserPreference`。描述启动某游戏/版本时如何选择 Java。

## 本目录模块

- **JavaPreference.cs**：Java 偏好基类/抽象（统一的选择策略接口）。
- **AutoSelect.cs**：自动选择策略（按版本自动挑选合适 Java）。
- **ExistingJava.cs**：使用指定已有 Java。
- **UseGlobalPreference.cs**：使用全局 Java 偏好。
- **UseRelativePath.cs**：使用相对路径 Java（便携版场景）。

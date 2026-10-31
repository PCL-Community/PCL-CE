# Minecraft/Java/Scanner 目录

Java 安装扫描，位于命名空间 `PCL.Core.Minecraft.Java.Scanner`。从不同来源发现本机 Java 安装。

## 本目录模块

- **IJavaScanner.cs**：Java 扫描器接口。
- **DefaultPathsScanner.cs**：默认路径扫描器（常见安装目录）。
- **RegistryJavaScanner.cs**：注册表扫描器（Windows 注册表中的 Java 项）。
- **PathEnvironmentScanner.cs**：环境变量 PATH 扫描器。
- **WhereCommandScanner.cs**：`where` 命令扫描器（查找 java 可执行文件）。
- **MicrosoftStoreJavaScanner.cs**：Microsoft Store Java 扫描器（商店版 Java 的特殊位置）。

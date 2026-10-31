# App/Cli 目录

命令行（CLI）解析子系统，位于命名空间 `PCL.Core.App.Cli`。负责定义与解析启动器的命令行参数与子命令。

## 本目录模块

- **ArgumentValueKind.cs**：命令行参数值的类型枚举。
- **BoolArgument.cs**：布尔型命令行参数定义。
- **DecimalArgument.cs**：数值型命令行参数定义。
- **TextArgument.cs**：文本型命令行参数定义。
- **CommandArgument.cs**：通用命令行参数定义。
- **CommandLine.cs**：命令行解析入口，将输入字符串解析为参数集合。
- **SubcommandDefinition.cs**：子命令定义（支持多级子命令）。

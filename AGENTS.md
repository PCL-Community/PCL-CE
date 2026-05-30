# PCL-CE 项目 AI 开发规范

面向 AI 智能体的项目规范，所有 AI 生成的代码和提交必须遵守。

> 详细文档请通过 GitHub MCP 或 WebFetch 工具获取：<https://github.com/PCL-Community/PCL-CE/wiki>

## 1. 项目概述

- **项目名称**：PCL-CE（Minecraft 启动器社区版，基于 .NET 8）
- **核心技术栈**：.NET 8 / C# 14 / MSTest
- **项目组成**：
  - `PCL.Core` — 核心库
  - `PCL.Core.SourceGenerators` — 源生成器
  - `PCL.Core.Test` — 测试项目 (MSTest)
  - `Plain Craft Launcher 2` — 启动器本体
- **引用方向**：本体 → 核心库（禁止反向引用）

## 2. 环境搭建与开发命令

```bash
dotnet build    # 构建项目
dotnet test     # 运行测试
dotnet clean    # 清理构建
```

- **运行时**：.NET Desktop Runtime 8.0

## 3. 编码规范

### 命名规范

| 类别 | 规范 | 示例 |
|------|------|------|
| 局部变量/参数 | camelCase | `name`, `resourceMap` |
| 私有属性/只读全局 | _PascalCase | `_Items`, `_PageMap` |
| 私有全局/静态 | _camelCase | `_parameters`, `_hasDisposed` |
| 主构造器属性/其它 | PascalCase | `Name`, `CurrentThread` |
| 非私有方法 | PascalCase | `Start()`, `ComposeMessage()` |
| 私有方法 | _PascalCase | `_StartInternal()` |
| 接口 | IPascalCase | `ICollection`, `ILifecycleService` |

**特殊**：事件不以 `On` 开头；同步原语以 `Event` 结尾。

### 代码风格
- LF 换行、UTF-8 无 BOM、4 空格缩进、Allman 花括号
- 禁止 `goto`、不必要的 `this`、类型不明确的 `var`

### 代码质量
- 空值检查：`??` / `?.`
- 异常：只捕获特定异常，不捕获 `Exception`
- 异步：`async`/`await`，禁止 `.Result` / `.Wait()`
- 避免：魔法数字、重复代码、单方法 >50 行、嵌套 >3 层
- 公共 API 加 XML 文档注释

> 生命周期服务、IoC 依赖注入、配置项、编译时优化(GeneratedRegex/LibraryImport)等写法参考 wiki

## 4. 操作边界

### ✅ 必须做
- 新增正则用 `GeneratedRegex`，P/Invoke 用 `LibraryImport`
- 核心逻辑写单元测试（MSTest），方法命名 `Should_` 开头，每个方法只测一个功能点
- 使用 `StringBuilder` 拼接字符串、`ConcurrentDictionary` 做线程安全集合
- 在 `Config.cs` 集中声明配置项（缓存不需要实时更新的值）

### ⚠️ 先询问
- 创建新的一级目录或修改核心工具类
- 引入新 NuGet 依赖
- 修改 CI/CD 配置、依赖版本

### 🚫 禁止
- `goto` 语句
- 硬编码密钥/密码
- 捕获通用 `Exception`
- 提交 `.vscode/`、未完成代码
- 在异步代码中使用 `.Result` / `.Wait()`

## 5. 提交规范

```
type(scope): digest
```

- **type**：`feat` / `fix` / `imp` / `ref` / `docs` / `style` / `perf` / `test` / `chore` / `bump` / `ci`
- `type` 英文小写，`scope` 英文小写短横线连接，`digest` 中文动词开头且无句号
- AI 生成代码在末尾添加模型标识，如 `[DeepSeek-V3.2]`

## 6. AI 生成代码规则

1. 禁止直接提交 >200 行或修改 >5 个 `.cs`/`.xaml` 文件的 PR
2. 必须人工审查：代码风格、逻辑缺陷（TODO/虚假API/重复造轮子）、冗余设计
3. 提交前运行 `dotnet build` 和 `dotnet test` 确认无报错

## 7. 常见问题

| 问题 | 排查方法 |
|------|----------|
| 编译失败 | 检查 SDK 版本（需 10.0+），`dotnet clean` 后重试 |
| 测试失败 | 确认 NuGet 包已 restore |
| 运行时异常 | 检查生命周期服务调用顺序、配置项是否正确注册 |

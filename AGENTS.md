# PCL2-CE 项目代理开发规范

本指南旨在为 AI 代理（agents）提供在 PCL2-CE 项目中开发时应遵循的规范和最佳实践。所有代理生成的代码和提交必须严格遵守以下规则。

## 项目概述

PCL2-CE 是一个基于 .NET 8 的 Minecraft 启动器社区版项目，包含以下核心组件：
- **Plain Craft Launcher 2**: 启动器本体（Visual Basic）
- **PCL.Core**: 社区版核心库（C#，语言版本 12.0）
- **PCL.Core.SourceGenerators**: 核心库源生成器
- **PCL.Core.Test**: 核心库测试项目

**重要**：项目引用是单向的，只能在本体项目中引用核心库，无法反向引用。

## C# 命名规范

### 变量命名
| 类别 | 命名规范 | 示例 |
|------|----------|------|
| 局部变量、方法参数、构造器参数 | camelCase | `name`, `runningThread`, `resourceMap` |
| 私有属性、私有只读全局变量 | _PascalCase | `_Items`, `_PageMap` |
| 私有全局变量、静态私有全局变量 | _camelCase | `_parameters`, `_runningThreads`, `_hasDisposed` |
| 主构造器属性 (record)、其它 | PascalCase | `Name`, `CurrentThread`, `ActivePageCollection` |

### 方法命名
| 类别 | 命名规范 | 示例 |
|------|----------|------|
| 非私有方法、静态非私有方法、局部方法 | PascalCase | `Start()`, `ComposeMessage()`, `WriteLogItem()` |
| 私有方法 | _PascalCase | `_StartInternal()`, `_RaiseChanged()` |

### 类型命名
| 类别 | 命名规范 | 示例 |
|------|----------|------|
| 接口 | IPascalCase | `ICollection`, `ILifecycleService`, `IConfig` |
| 其它 | PascalCase | 类、枚举、记录、委托等 |

### 特殊命名规则
- **事件**：不应以 `On` 开头
- **同步原语**：`AutoResetEvent`、`ManualResetEvent`、`ManualResetEventSlim` 等应以 `Event` 结尾

## 代码风格规范

### 基本要求
1. **换行符**：使用 LF 换行符（不要使用 CRLF）
2. **编码**：使用标准 UTF-8 编码（不附带 BOM）
3. **缩进**：使用 4 个空格进行缩进（不要使用 Tab）
4. **花括号**：使用 Allman 风格（左花括号另起一行）

### 禁止事项
- **禁止使用 `goto` 语句和代码标签**：这是严格禁止的，PR 中包含 `goto` 语句将被直接拒绝
- **避免不必要的 `this` 引用**：除非必要，不要使用 `this.` 前缀
- **避免使用 `var` 当类型不明确时**：当类型从右侧明显时可以使用 `var`，否则应显式声明类型

## 编译时优化规范

### 正则表达式
- **必须使用 `GeneratedRegex`**：所有新增的正则匹配必须使用 .NET 7+ 的 `GeneratedRegexAttribute`
- **推荐位置**：在 `Utils/RegexPatterns.cs` 文件中添加新的正则表达式声明
- **方法参数**：新增的传入正则参数的方法应直接接受正则表达式实例，而不是字符串实例

### 外部库调用 (P/Invoke)
- **必须使用 `LibraryImport`**：使用 `LibraryImport` 取代 `DllImport`
- **可见性**：推荐使用 `private` 修饰符（虽然官方允许 `internal`）
- **使用 `partial` 关键字**：`LibraryImport` 要求使用 `partial` 替代 `extern`
- **利用 Span<T>**：对于指针参数，优先使用 `Span<T>` 提升性能和安全性

## 生命周期服务规范

### 服务声明
```csharp
[LifecycleService(LifecycleState.Loaded, Priority = 114)]
public sealed class MyService : ILifecycleService
{
    public string Identifier => "my-service";
    public string Name => "我的服务";
    public bool SupportAsyncStart => true;
    
    private static LifecycleContext? _context;
    private static LifecycleContext Context => _context!;
    
    private MyService() { _context = Lifecycle.GetContext(this); }
    
    public Task StartAsync() { /* ... */ }
    public Task StopAsync() { /* ... */ }
}
```

### 调用顺序要求
- 同步服务：确保在更晚的生命周期状态中调用，或在同状态的异步服务初始化中调用
- 异步服务：只能在更晚的生命周期状态中调用
- 违反调用顺序可能导致 `NullReferenceException`、`InvalidOperationException` 等异常

## 配置系统规范

### 配置项声明
```csharp
[ConfigItem<int>("SomeKey", 114514, ConfigSource.Shared)]
public static partial int SomeConfig { get; set; }
```

### 最佳实践
1. **集中管理**：尽可能在核心库的 `Config.cs` 文件中添加配置项
2. **添加注释**：确保每个配置项都有清晰的注释
3. **性能考虑**：配置系统的所有事件均同步执行，注册事件前确保代码具有足够性能
4. **缓存配置值**：不需要实时更新的值应缓存，避免频繁获取

## 提交信息规范

### 简单格式（单行）
```
type(scope?): digest
```

### 多行格式
```
type(scope?): subject

body

footer
```

### 类型 (type)
- `feat`/`feature`：引入新特性
- `fix`：修复 bug
- `imp`/`improve`：优化现有功能
- `ref`/`refactor`：重构代码
- `docs`：仅修改文档
- `style`：代码格式调整
- `perf`/`performance`：性能优化
- `test`：添加或修改测试
- `chore`：构建流程维护
- `bump`：版本相关更改
- `ci`：修改 CI/CD 配置

### 规则
- `type` 使用英文小写
- `scope` 使用英文，小写，单词间用短横线连接
- `digest` 应尽量使用中文，以动词原型开头
- `digest` 末尾不应有句号
- `scope` 后的冒号与 `digest` 之间应有一个空格

## AI 生成代码规范

### 必须遵守的规则
1. **注明模型版本**：在提交消息或 PR 标题末尾添加模型标识，如 `[DeepSeek-V3.2]`
2. **禁止直接提交大型 PR**：一次性新增/修改超过 200 行或修改超过 5 个 `.cs`/`.vb`/`.xaml` 文件的 PR 不允许直接提交 AI 生成代码
3. **人工审查**：必须对 AI 生成的代码进行人工审查，修正代码缺陷

### 审查要点
- 检查代码风格是否符合技术规范
- 检查逻辑缺陷：TODO 注释、虚假 API 参数、重复造轮子、性能问题
- 检查冗余度：设计是否过度，是否引入不必要的依赖

## 文件组织规范

### 项目结构
- **核心库目录**：按用途分出不同的目录和命名空间
- **新内容添加**：尽可能不要创建新的一级目录，减少二级目录使用
- **命名空间**：与目录结构保持一致

### 文件命名
- **类文件**：与类名一致（如 `MessageService.cs`）
- **接口文件**：以 `I` 开头（如 `ILifecycleService.cs`）
- **测试文件**：以 `Tests` 结尾（如 `MyServiceTests.cs`）

## 代码质量要求

### 必须做到
1. **空值检查**：使用 null 合并运算符 `??` 和条件访问运算符 `?.`
2. **资源管理**：实现 `IDisposable` 接口的类必须正确释放资源
3. **异常处理**：不要捕获通用异常（`Exception`），只捕获特定异常
4. **异步编程**：使用 `async`/`await`，避免 `Result` 和 `Wait()`

### 避免事项
1. **魔法数字**：使用常量或枚举代替
2. **重复代码**：提取公共方法或使用继承/组合
3. **过长方法**：单个方法不应超过 50 行
4. **过深嵌套**：避免超过 3 层的嵌套

## 测试规范

### 单元测试
- 使用 xUnit 测试框架
- 测试方法应以 `Should_` 开头
- 测试类应与被测试类同名，加上 `Tests` 后缀
- 每个测试方法只测试一个功能点

### 测试覆盖
- 核心业务逻辑必须有单元测试
- 工具类方法必须有单元测试
- 复杂的算法必须有单元测试

## 性能要求

### 内存管理
- 避免不必要的内存分配
- 使用 `StringBuilder` 进行字符串拼接
- 重用对象，避免频繁创建和销毁

### 并发处理
- 使用 `CancellationToken` 支持取消操作
- 避免死锁：不要在异步代码中使用 `.Result` 或 `.Wait()`
- 使用 `ConcurrentDictionary` 等线程安全集合

## 安全规范

### 数据安全
- 不要在代码中硬编码敏感信息（密码、API 密钥等）
- 使用安全的随机数生成器（`RandomNumberGenerator`）
- 对用户输入进行验证和清理

### 代码安全
- 避免 SQL 注入：使用参数化查询
- 避免 XSS 攻击：对输出进行编码
- 避免路径遍历：验证文件路径

## 文档要求

### 代码注释
- 公共 API 必须有 XML 文档注释
- 复杂算法必须有行内注释
- 临时代码必须标注 `// TODO:` 或 `// HACK:`

### 方法文档
```csharp
/// <summary>
/// 处理用户登录请求
/// </summary>
/// <param name="username">用户名</param>
/// <param name="password">密码</param>
/// <returns>登录结果，包含用户信息和令牌</returns>
/// <exception cref="ArgumentException">当用户名或密码为空时抛出</exception>
public async Task<LoginResult> LoginAsync(string username, string password)
{
    // 方法实现
}
```

## 工具和环境

### 开发工具
- **推荐 IDE**：JetBrains Rider 2025.3.2 或更高版本
- **备选 IDE**：Visual Studio 2026 或更高版本
- **SDK**：.NET SDK 10.0 或更高版本
- **Runtime**：.NET Desktop Runtime 8.0

### 构建命令
```bash
# 构建项目
dotnet build

# 运行测试
dotnet test

# 清理构建
dotnet clean
```

## 版本控制规范

### 分支管理
- 功能开发在特性分支进行
- 不要直接在 `dev` 分支提交代码
- 使用有意义的分支名称（如 `feature/dark-mode`、`fix/login-issue`）

### 提交规范
- 每个提交应该是一个逻辑完整的单元
- 提交信息应该清晰描述更改内容
- 避免提交无关的更改

## 常见问题处理

### 编译错误
1. **检查 SDK 版本**：确保使用正确的 .NET SDK 版本
2. **清理解决方案**：执行 `dotnet clean` 后重新构建
3. **检查依赖**：确保所有 NuGet 包已正确安装

### 运行时错误
1. **检查生命周期**：确保服务调用顺序正确
2. **检查配置**：确保配置项已正确声明和初始化
3. **检查资源**：确保资源已正确释放

## 代码审查清单

### 提交前检查
- [ ] 代码符合命名规范
- [ ] 代码符合代码风格
- [ ] 没有使用 `goto` 语句
- [ ] 使用了 `GeneratedRegex` 而不是动态正则
- [ ] 使用了 `LibraryImport` 而不是 `DllImport`
- [ ] 添加了必要的测试
- [ ] 更新了相关文档
- [ ] 提交信息符合规范

### PR 审查要点
- [ ] 代码质量符合项目标准
- [ ] 没有引入新的安全漏洞
- [ ] 性能影响可接受
- [ ] 文档已更新
- [ ] 测试覆盖率足够

## 联系方式

如有疑问，请通过以下方式联系：
- GitHub Issues：在项目仓库中提交 Issue
- 开发者 QQ 群：加入社区开发者群组讨论

## 更新记录

- 2026-05-30：初始版本，基于项目 wiki 整理
# PCL.Core.Test 测试项目

## 项目定位

针对共享核心库 **PCL.Core** 的单元测试项目（PCL2 / Plain Craft Launcher 2 的 Windows 启动器核心）。
- 测试框架：**MSTest**（MSTest.TestFramework / MSTest.TestAdapter 4.3.0 + Microsoft.NET.Test.Sdk 18.7.0）。
- 目标框架：`net10.0-windows`，启用 WPF；依赖项含 fNbt、SQLitePCLRaw、System.Text.Json。
- 项目引用 `..\PCL.Core\PCL.Core.csproj`，命名空间对应 PCL.Core 各模块（如 `PCL.Core.Test.Minecraft` 对应 `PCL.Core.Minecraft`）。
- 覆盖范畴：通用工具（编码/JSON/Diff/进制/字节/版本号/日志/模糊效果）、App 层（本地化/区域策略/首页资讯视图/房间码）、Minecraft（Java 探测/存档管理/导入/McPing/档案）、网络（DoH/HTTP 服务）、校验器、加密密钥与缓存存储等。
- 注意：部分用例需联网或访问运行时资源（如 Toast、Blur、Java 扫描、Modrinth/Cloudflare 查询），并非完全离线可重复，运行前需审视。

## 本目录直接测试模块

本目录（不含子目录）直接承载以下测试类，按被测 PCL.Core 模块分组说明：

### 通用工具（`PCL.Core.Utils` 及子命名空间）
- `BaseXTest.cs` — 进制转换工具（`PCL.Core.Utils.Exts`）。
  - 覆盖：`FromB10ToB36` / `FromB36ToB10` / `FromB10ToB32` / `FromB32ToB10`。
  - 关键用例：`TestBase36`、`TestBase32` 对字符串 `"999999999999999"` 做十进制↔B36/B32 往返转换，断言 `FromB36ToB10(FromB10ToB36(x)) == x` 等往返一致，验证无精度/字符丢失。
- `ByteHelperTest.cs` — 字节/容量可读化格式化（`PCL.Core.IO` 命名空间下的 `ByteStream.GetReadableLength`）。
  - 关键用例：`TestTransform` 用 `Random` 生成 1000 个 1KB~10MB 随机长度调用 `GetReadableLength`，仅用 `Stopwatch` 测量耗时并打印，断言为零（纯性能基准，无功能性断言）。
- `DiffTest.cs` — 二进制差量补丁应用（`PCL.Core.Utils.Diff.BsDiff`）。
  - 关键用例：`TestBsDiff` 内置一段源字节数组与 BSDIFF 补丁字节，调用 `ApplyAsync` 后逐字节断言等于预期 `"I am very sure how bsdiff work"`。`TestBsDiff2` 为可选的文件级用例，若 `from`/`diffFile`/`outFile` 任一为空则直接返回（默认不执行）。
- `DynamicJsonTest.cs` — 动态 JSON（`ExpandoObject`）序列化/反序列化往返（`PCL.Core.Utils` + `JsonCompat.SerializerOptions`）。
  - 关键用例：`TestDynamicJson` 构造含嵌套与自引用的匿名对象，经 `JsonSerializer` 序列化后再反序列化为 `ExpandoObject`，仅打印结果（无断言，验证不抛异常）。
- `EncodingDetectorTest.cs` — 文本编码自动探测（`PCL.Core.Utils.Codecs.EncodingDetector`）。
  - 关键用例：`TestEncoding` 验证 UTF-8（含中文）、GB2312 字节能被正确识别；无法识别的非法字节序列（如 `[0xfe,0x5f,0xa1]`）回退到 `Encoding.Default`。GB18030 用例被注释保留。
- `JsonCompatDateTimeTest.cs` — 日期时间解析（`PCL.Core.Utils.JsonCompat.TryParseDateTime`）。
  - 关键用例：`ParsesStandardIso`（标准带时区）、`ParsesNegativeZeroOffset`（`-00:00` 等价 `+00:00`/UTC）、`ParsesEndOfDayHour24AsNextDayMidnight`（ISO `24:00:00` 归一为次日零点）、`RejectsInvalidTimeMasqueradingAsEndOfDay`（`24:30:00` 必须失败而非被归一）、`RejectsGarbage`（非日期/空串/`null` 均返回 `false`）。
- `SemVerTest.cs` — 语义化版本号（`PCL.Core.Utils.SemVer`）。
  - 关键用例：`TestParse` 解析 `2.3.4`、`2.3.4-beta.1`、`2.3.4-beta.1+11451aq`、`456.759.159-alpha.2`、`v2.14.5-beta.1.2147483647`，分别断言 Major/Minor/Patch/Prerelease/BuildMetadata 字段，并验证版本比较（`t5 > t4`）。
- `MavenTest.cs` — Maven 坐标解析（`PCL.Core.Minecraft.MavenArtifact`）。
  - 关键用例：`ParsePathAndUri` 覆盖 `group:artifact:version`、`…:jar:version`、`…:snapshot:version`、`…:jar:snapshot:version` 四种坐标写法，调用 `Resolve` 生成 URI/本地路径（仅打印）。`ParseInvalidId` 以非法坐标（段数过少/过多）构造，预期抛出 `FormatException`。

### App 层（`PCL.Core.App` 及子命名空间）
- `LocalizationTest.cs` — 本地化资源字典（`PCL.Core.App.Localization`）。
  - 覆盖：`LocalizationService`、`LocalizationFontProfile`/`LocalizationFontService`。
  - 关键用例：以 `zh-CN` 为基线，校验各语言文件（XAML，实时从仓库 `PCL.Core/App/Localization/Languages/*.xaml` 读取）键集合完整、无缺失键；各语言无重复键；键名遵循点分隔命名（非空、含 `.`、无空格）；`CultureName` 合法且对应资源文件存在；`ResolveProfileFromCultureName` 将 zh-CN/zh-Hans→简体、zh-TW/zh-HK/zh-Hant-HK→繁体、ja-JP→日文、ko-KR→韩文、en-*→英文、其余→Other；别名（如 `zh-HK`）不被当作受支持语言。
  - 注意：依赖仓库路径定位（`GetRepositoryRoot` 向上查找含 `PCL.Core` 的目录），需在仓库内运行。
- `RegionPolicyTest.cs` — 区域策略分类（`PCL.Core.App.Region.RegionPolicy` / `RegionSignals`）。
  - 关键用例：`ChinaGeoRestrictsRegardlessOfLanguage`（geo=`CN` 直接归 Mainland）、`ChinaTimeZoneAndExplicitChinaLocaleRestrictEvenWithOtherGeo`、`OtherChineseLocalesDoNotRestrict`（HK/MO/TW/SG 归 Other 且 `IsRestricted=false`）、`MissingSignalsRestrictAsUnknown`（`signals-unavailable`）、`AvailableButInsufficientSignalsUseInsufficientReason`、设备区域 `deviceRegion=45`（Windows GeoID 中国）→Mainland（`device-region-cn`），`156`（马拉维 GeoID）≠CN、`NonChinaDeviceRegionDoesNotOverrideOtherSignals`、`OneWeakLocaleSignalWithoutGeoOrTimeZoneIsUnknown`、`SecondaryPreferredLanguageDoesNotCountAsTheSystemDisplayLanguage` 等，全面覆盖地理/语言/时区/设备区域各信号组合。
- `HomepageNewsViewModelTest.cs` — 首页新闻链接安全校验（`PCL.Core.ViewModel.Homepage.NewsViewModel.IsSafeNewsLink`）。
  - 关键用例：`IsSafeNewsLink_AllowsExpectedWebHosts` 数据驱动放行 `minecraft.net`、`minecraft-services.net`、`learn.microsoft.com` 等白名单 HTTPS/HTTP 域名；`IsSafeNewsLink_BlocksShellAndUnexpectedTargets` 拦截 `null`/空串/`calc.exe`/本地文件路径/`file://`/`minecraft://`/UNC 路径/伪造子域 `minecraft.net.evil.example` 等，断言均返回 `false`。
- `LobbyCodeGenerateTest.cs` — 联机房间的房间码生成与解析（`PCL.Core.Link.Scaffolding.LobbyCodeGenerator`）。
  - 关键用例：`GenerateTest` 调用 `Generate()`（无断言，仅生成）；`ParseTest` 生成 `FullCode` 后 `TryParse` 应成功（`Assert.IsTrue(success)`）。

### Minecraft（`PCL.Core.Minecraft` 及子命名空间）
- `JavaTest.cs` — 本地 Java 运行时探测（`PCL.Core.Minecraft` + `PCL.Core.Minecraft.Java.Parser`/`Scanner`）。
  - 关键用例：`TestJavaSearch` 组合 `PeHeaderParser` 与 5 个扫描器（注册表/默认路径/环境变量/微软商店/where 命令）构造 `JavaManager`，`ScanJavaAsync` 后 `GetSortedJavaList` 断言：每项 `Version.Major>0`、JavaFolder 非空；二次扫描结果数量一致（稳定可重复）；`SelectSuitableJavaAsync(1.8, 30)` 能选出合适版本；按 `JavaExePath` 分组无重复。
  - 注意：依赖本机已安装 Java 与注册表/环境变量，结果随运行环境变化，非确定性；且会访问运行时资源，建议在有 Java 的环境中运行。

### 日志（`PCL.Core.Logging`）
- `LoggerTest.cs` — 日志写入与并发（`PCL.Core.Logging.Logger` / `LoggerConfiguration` / `LoggerAdapter`）。
  - 关键用例：`TestSimpleWrite` 写 10 条后校验文件行数与 `[HH:MM:SS.mmm] [INFO] [分类] 消息` 格式正则；`TestHeavyWrite` 25 线程 × 25565 条高并发写入，断言 `written + dropped == threadCount*perThread`（验证背压/丢弃计数）；`TestMinLogLevelFiltersFile`（`MinLogLevel=Error` 时 Debug/Info 不落盘）；`TestDisposeIsIdempotent`（多次 `DisposeAsync` 不抛）；`TestAdapterScopeIsolation`（`LoggerAdapter` 的 `BeginScope` 嵌套作用域日志标记 `[A => B]` 不向父作用域泄漏）；`TestAdapterExceptionOnce`（异常文本仅渲染一次）。
  - 夹具：每个用例用 `_CreateTempFolder` 在临时目录建独立日志目录，写后 `_ReadAll` 汇总文件内容断言。

### UI（`PCL.Core.UI`）
- `ToastTest.cs` — 系统 Toast 通知（`PCL.Core.UI.ToastNotification.SendToast`）。
  - 关键用例：`TestToast` 直接调用 `SendToast`（无断言）。代码注释明确说明：依赖运行时加载的 `Basics.cs` Metadata，单元测试项目无法访问，**运行会炸**，勿在常规测试运行中启用。
- `BlurPerformanceTest.cs` — 模糊效果性能基准（`PCL.Core.UI.Effects` 的 `BlurEffect` 与原生/优化 `OptimizedBlurEffect`）。
  - 关键用例：`BlurTest.TestBlur` 调用 `BlurPerformanceTest.RunComprehensiveTest()`，在 1920×1080 测试图上对比原生 `BlurEffect` 与不同采样率（1.0/0.7/0.5/0.3/0.1）优化版耗时，并含 `QuickPerformanceComparison`、`MemoryUsageTest` 等静态基准方法（均为纯性能打印，无功能性断言）。依赖 WPF 渲染（`RenderTargetBitmap`/`DrawingVisual`），需在支持 WPF 的运行时执行。

## 运行流程（测试编排）

- 所有测试类标注 `[TestClass]`、方法标注 `[TestMethod]`（或 `[DataTestMethod]` 数据驱动），由 MSTest 发现与执行。
- 无统一 `TestInitialize`/`TestCleanup`：仅 `JavaTest`、`LoggerTest` 用 `TestContext` 取消令牌；`LoggerTest`/`BlurPerformanceTest` 各自维护临时目录或 WPF 渲染上下文。
- 数据驱动用例（`HomepageNewsViewModelTest`、`RegionPolicyTest`）通过 `[DataRow]` 批量展开同形断言；`SaveManagerTest` 等位于子目录的测试用 `#region` 组织 Scan/Load/Editors 用例。
- 纯基准类（`ByteHelperTest`、`BlurPerformanceTest`、`DynamicJsonTest`）偏重 `Console.WriteLine` 输出耗时/结果，断言极少，用于人工查看而非 CI 强校验。

## 输入输出规范（被测 API 与夹具）

- 被测 API 入口：多为 `PCL.Core.*` 静态方法或构造后调用（`FromB10ToB36`、`BsDiff.ApplyAsync`、`EncodingDetector.DetectEncoding`、`JsonCompat.TryParseDateTime`、`SemVer.Parse`、`MavenArtifact`/`LobbyCodeGenerator`、`NewsViewModel.IsSafeNewsLink`、`RegionPolicy.Evaluate`、`JavaManager.ScanJavaAsync`、`Logger`/`LoggerAdapter`、`ToastNotification.SendToast`、`BlurEffect`/`OptimizedBlurEffect`）。
- 夹具：临时目录统一基于 `Path.GetTempPath()/PCLTest/.../{Guid}`（见 `LoggerTest._CreateTempFolder`、`SaveImportHelperTest`）；NBT 数据通过 `fNbt.NbtFile` 以 GZip 写入 `level.dat`（见 `SaveManagerTest.WriteLevelDat`）；本地化测试直接读取仓库内 `*.xaml` 资源。
- 断言风格：功能类用 `Assert.AreEqual`/`IsTrue`/`IsFalse`/`ThrowsExactly`/`StringAssert.Matches|Contains`；性能类仅打印；数据驱动用 `[DataRow]` 参数化。

## 使用约束与注意事项

- 需联网/外部服务：`JavaTest`（扫描本机 Java）、`McPing`（见子目录，ping `mc.hypixel.net` 等真实服务器）、子目录中的网络/Modrinth 查询——结果非确定性，CI 中建议隔离或忽略。
- 需运行时/桌面环境：`ToastTest`（依赖 `Basics.cs` Metadata，运行会抛）、`BlurPerformanceTest`（依赖 WPF `RenderTargetBitmap` 渲染）。
- 依赖仓库布局：`LocalizationTest` 需从程序基目录向上定位含 `PCL.Core` 的仓库根，才能读取语言 XAML；在源码树外运行会 `Assert.Fail("无法定位仓库根目录")`。
- 纯基准无强断言：`ByteHelperTest`、`BlurPerformanceTest`、`DynamicJsonTest`、`LobbyCodeGenerateTest.GenerateTest` 主要用于观察，不应作为通过/失败的唯一依据。
- 部分用例为可选/占位：`DiffTest.TestBsDiff2`、`LobbyCodeGenerateTest.GenerateTest` 路径为空时直接返回，默认不产生断言。

## 子目录导航

- [App](./App/agent.md) — App 层测试：命令行解析、配置存储、事件总线。
- [Encryption](./Encryption/agent.md) — 加密相关测试（AES-GCM、ChaCha20、ChaCha20-Poly1305）。
- [Hash](./Hash/agent.md) — 哈希与哈希缓存测试（MurmurHash2、HashCache）。
- [IO](./IO/agent.md) — IO 存储与缓存系统测试容器。
- [Minecraft](./Minecraft/agent.md) — Minecraft 相关测试：Java 探测、存档、McPing、档案、资源依赖。
- [Network](./Network/agent.md) — 网络测试：DoH 查询、HTTP 服务、DoH 连接。
- [Project](./Project/agent.md) — 项目平台（Modrinth）相关测试。
- [Properties](./Properties/agent.md) — 程序集元数据（非测试）。
- [Utils](./Utils/agent.md) — 工具类测试容器（含密钥/加密辅助）。
- [Validate](./Validate/agent.md) — 输入校验器测试集合。

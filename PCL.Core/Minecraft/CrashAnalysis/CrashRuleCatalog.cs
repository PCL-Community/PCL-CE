using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     <p>内置崩溃规则目录。</p>
///     <p>
///         本文件集中声明规则，但规则执行顺序由 <see cref="CrashRuleEngine" /> 根据优先级控制。
///         新增规则时优先使用 <see cref="Rules.Text" /> 或 <see cref="Rules.Regex" />，并为稳定的 rule id 添加测试。
///         这里可以包含日志匹配字符串，因为它们属于规则数据；但不能包含用户可见的完整分析文案。
///     </p>
/// </summary>
internal static class CrashRuleCatalog
{
    /// <summary>
    ///     <p>创建全部内置规则。</p>
    ///     <p>
    ///         返回顺序不直接等同于执行顺序，真正执行时会由 <see cref="CrashRuleEngine" /> 按优先级分组。
    ///         这里的分组主要服务于维护：让 Java、内存、显卡、加载器、Mod 和世界内容规则各自集中。
    ///     </p>
    /// </summary>
    public static IReadOnlyList<ICrashRule> Create()
    {
        return
        [
            new NoAnalyzableLogRule(),
            .._CreateJavaRules(),
            .._CreateMemoryRules(),
            .._CreateGraphicsRules(),
            .._CreateModFileRules(),
            .._CreateModLoaderRules(),
            .._CreateModCrashRules(),
            .._CreateOptiFineRules(),
            .._CreateWorldRules(),
            new StackTraceRule(new CrashStackAnalyzer()),
            new VeryShortOutputRule()
        ];
    }

    #region Rules Creator

    /// <summary>
    ///     Java 运行时相关规则：JDK/JRE 错用、OpenJ9、Java 版本过高或过低等。
    /// </summary>
    private static IReadOnlyList<ICrashRule> _CreateJavaRules()
    {
        return
        [
            Rules.Text("java.version_too_high.define_class",
                CrashReasonCode.JavaVersionTooHigh,
                CrashRulePriority.High,
                CrashLogSection.CrashReport,
                ["Unable to make protected final java.lang.Class java.lang.ClassLoader.defineClass"]),
            Rules.Text("java.using_jdk.class_cast_java_base",
                CrashReasonCode.UsingJdk,
                CrashRulePriority.High,
                CrashLogSection.Game,
                [
                    "java.lang.ClassCastException: java.base/jdk",
                    "java.lang.ClassCastException: class jdk."
                ]),
            Rules.Text("java.openj9.unsupported",
                CrashReasonCode.UsingOpenJ9,
                CrashRulePriority.High,
                CrashLogSection.Game,
                [
                    "Open J9 is not supported",
                    "OpenJ9 is incompatible",
                    ".J9VMInternals."
                ]),
            Rules.Text("java.version_too_high.module_export",
                CrashReasonCode.JavaVersionTooHigh,
                CrashRulePriority.High,
                CrashLogSection.Game,
                [
                    "java.lang.NoSuchFieldException: ucp",
                    "because module java.base does not export",
                    "java.lang.ClassNotFoundException: jdk.nashorn.api.scripting.NashornScriptEngineFactory",
                    "java.lang.ClassNotFoundException: java.lang.invoke.LambdaMetafactory"
                ]),
            Rules.Text("java.version_incompatible.class_file_major",
                CrashReasonCode.JavaVersionIncompatible,
                CrashRulePriority.High,
                CrashLogSection.Game,
                [
                    "Unsupported class file major version",
                    "Unsupported major.minor version"
                ]),
            Rules.Text("java.mod_requires_java11.class_file_55",
                CrashReasonCode.ModRequiresJava11,
                CrashRulePriority.High,
                CrashLogSection.Game,
                [
                    "has been compiled by a more recent version of the Java Runtime (class file version 55.0)",
                    "sun.misc.Unsafe.defineAnonymousClass",
                    "The requested compatibility level JAVA_11 could not be set"
                ]),
            Rules.Text("java.old_forge_high_java.manifest_verifier",
                CrashReasonCode.OldForgeHighJavaIncompatible,
                CrashRulePriority.High,
                CrashLogSection.Game,
                ["java.lang.NoSuchMethodError: sun.security.util.ManifestEntryVerifier"])
        ];
    }

    /// <summary>
    ///     内存相关规则：堆内存不足、物理内存不足和 32 位 Java 堆大小限制。
    /// </summary>
    private static IReadOnlyList<ICrashRule> _CreateMemoryRules()
    {
        return
        [
            Rules.Text("memory.out_of_memory.game",
                CrashReasonCode.OutOfMemory,
                CrashRulePriority.High,
                CrashLogSection.Game,
                [
                    "java.lang.OutOfMemoryError",
                    "an out of memory error",
                    "Could not reserve enough space"
                ]),
            Rules.Text("memory.out_of_memory.crash_report",
                CrashReasonCode.OutOfMemory,
                CrashRulePriority.High,
                CrashLogSection.CrashReport,
                ["java.lang.OutOfMemoryError"]),
            Rules.Text("memory.out_of_memory.hs_err",
                CrashReasonCode.OutOfMemory,
                CrashRulePriority.High,
                CrashLogSection.JavaError,
                [
                    "The system is out of physical RAM or swap space",
                    "Out of Memory Error"
                ]),
            Rules.Text(
                "memory.32bit_java.heap",
                CrashReasonCode.ThirtyTwoBitJavaMemoryLimit,
                CrashRulePriority.High,
                CrashLogSection.Game,
                [
                    "Invalid maximum heap size",
                    "for 1048576KB object heap"
                ])
        ];
    }

    /// <summary>
    ///     显卡和 OpenGL 相关规则：驱动不支持、像素格式异常、厂商驱动访问冲突等。
    /// </summary>
    private static IReadOnlyList<ICrashRule> _CreateGraphicsRules()
    {
        return
        [
            Rules.Text("graphics.driver.opengl_unsupported",
                CrashReasonCode.UnsupportedOpenGl,
                CrashRulePriority.High,
                CrashLogSection.Game,
                ["The driver does not appear to support OpenGL"]),
            Rules.Text("graphics.driver.pixel_format",
                CrashReasonCode.PixelFormatUnsupported,
                CrashRulePriority.High,
                CrashLogSection.Combined,
                [
                    "Couldn't set pixel format",
                    "Pixel format not accelerated"
                ]),
            Rules.Text("graphics.driver.intel_access_violation",
                CrashReasonCode.IntelDriverAccessViolation,
                CrashRulePriority.High,
                CrashLogSection.JavaError,
                all:
                [
                    "EXCEPTION_ACCESS_VIOLATION",
                    "# C  [ig"
                ]),
            Rules.Text("graphics.driver.amd_access_violation",
                CrashReasonCode.AmdDriverAccessViolation,
                CrashRulePriority.High,
                CrashLogSection.JavaError,
                all:
                [
                    "EXCEPTION_ACCESS_VIOLATION",
                    "# C  [atio"
                ]),
            Rules.Text("graphics.driver.nvidia_access_violation",
                CrashReasonCode.NvidiaDriverAccessViolation,
                CrashRulePriority.High,
                CrashLogSection.JavaError,
                all:
                [
                    "EXCEPTION_ACCESS_VIOLATION",
                    "# C  [nvoglv"
                ]),
            Rules.Text("graphics.resource_pack.too_large",
                CrashReasonCode.ResourcePackTooLargeOrGpuInsufficient,
                CrashRulePriority.High,
                CrashLogSection.Game,
                ["Maybe try a lower resolution resourcepack?"]),
            Rules.Text("graphics.opengl.invalid_operation_1282",
                CrashReasonCode.ShaderOrResourcePackOpenGl1282,
                CrashRulePriority.High,
                CrashLogSection.Game,
                ["1282: Invalid operation"])
        ];
    }

    /// <summary>
    ///     Mod 文件层面的规则：jar 被解压、文件名非法、重复安装、文件损坏等。
    /// </summary>
    private static IReadOnlyList<ICrashRule> _CreateModFileRules()
    {
        return
        [
            Rules.Text("mod.file.extracted_jar",
                CrashReasonCode.ExtractedModJar,
                CrashRulePriority.High,
                CrashLogSection.Game,
                [
                    "The directories below appear to be extracted jar files. Fix this before you continue.",
                    "Extracted mod jars found, loading will NOT continue"
                ]),
            Rules.Text("mod.file.invalid_module_name",
                CrashReasonCode.InvalidModFileName,
                CrashRulePriority.High,
                CrashLogSection.Game,
                ["Invalid module name: '' is not a Java identifier"]),
            Rules.Regex("mod.file.integrity.signer",
                CrashReasonCode.FileIntegrityFailed,
                CrashRulePriority.High,
                CrashLogSection.Game,
                Rules.Pattern("class \\\"(?<FileName>[^\\\"]+)\\\"'s signer information", RegexOptions.IgnoreCase),
                [new CrashParameterMapping(CrashFindingParameterNames.FileName, "FileName")]),
            new DuplicateModRule()
        ];
    }

    /// <summary>
    ///     加载器层面的规则：Fabric/Forge/Quilt 错误、缺失依赖、版本不匹配和安装不完整。
    /// </summary>
    private static IReadOnlyList<ICrashRule> _CreateModLoaderRules()
    {
        return
        [
            Rules.Text("modloader.mixin_bootstrap.missing",
                CrashReasonCode.MissingMixinBootstrap,
                CrashRulePriority.High,
                CrashLogSection.Game,
                ["java.lang.ClassNotFoundException: org.spongepowered.asm.launch.MixinTweaker"]),
            Rules.Text("modloader.forge.incomplete.launch_target",
                CrashReasonCode.IncompleteForgeInstallation,
                CrashRulePriority.High,
                CrashLogSection.Game,
                ["Cannot find launch target fmlclient, unable to launch"],
                []),
            Rules.Text("modloader.forge.incomplete.fmlcore",
                CrashReasonCode.IncompleteForgeInstallation,
                CrashRulePriority.High,
                CrashLogSection.Game,
                all:
                [
                    "Invalid paths argument, contained no existing paths",
                    @"libraries\net\minecraftforge\fmlcore"
                ]),
            Rules.Text("modloader.forge.multiple_arguments",
                CrashReasonCode.MultipleForgeArguments,
                CrashRulePriority.High,
                CrashLogSection.Game,
                ["Found multiple arguments for option fml.forgeVersion, but you asked for only one"]),
            Rules.Text("modloader.too_many_ids",
                CrashReasonCode.TooManyModsIdLimit,
                CrashRulePriority.High,
                CrashLogSection.CrashReport,
                ["maximum id range exceeded"]),
            Rules.Text("modloader.night_config.not_enough_data",
                CrashReasonCode.NightConfigBug,
                CrashRulePriority.High,
                CrashLogSection.Game,
                ["com.electronwill.nightconfig.core.io.ParsingException: Not enough data available"]),
            new FabricSolutionRule(),
            new FabricOrModLoaderErrorRule(),
            new ForgeErrorRule(),
            new ModDependencyRule()
        ];
    }

    /// <summary>
    ///     Mod 初始化、Mixin 和配置相关规则。
    /// </summary>
    private static IReadOnlyList<ICrashRule> _CreateModCrashRules()
    {
        return
        [
            new MixinFailureRule(),
            new ConfirmedModCrashRule(),
            new SuspectedModCrashRule(),
            new ModConfigCrashRule(),
            new ModInitializationFailureRule()
        ];
    }

    /// <summary>
    ///     OptiFine 相关规则，尤其是与 Forge、光影或世界加载的兼容问题。
    /// </summary>
    private static IReadOnlyList<ICrashRule> _CreateOptiFineRules()
    {
        return
        [
            Rules.Text("optifine.forge.incompatible.methods",
                CrashReasonCode.OptiFineForgeIncompatible,
                CrashRulePriority.High,
                CrashLogSection.Combined,
                [
                    "TRANSFORMER/net.optifine/net.optifine.reflect.Reflector.<clinit>(Reflector.java",
                    "java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.texture.SpriteContents.<init>",
                    "java.lang.NoSuchMethodError: 'java.lang.String com.mojang.blaze3d.systems.RenderSystem.getBackendDescription",
                    "java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.block.model.BakedQuad.<init>",
                    "java.lang.NoSuchMethodError: 'void net.minecraftforge.client.gui.overlay.ForgeGui.renderSelectedItemName",
                    "java.lang.NoSuchMethodError: 'void net.minecraft.server.level.DistanceManager",
                    "java.lang.NoSuchMethodError: 'net.minecraft.network.chat.FormattedText net.minecraft.client.gui.Font.ellipsize"
                ]),
            Rules.Text("optifine.forge.missing_mods",
                CrashReasonCode.OptiFineForgeIncompatible,
                CrashRulePriority.High,
                CrashLogSection.CrashReport,
                all:
                [
                    "has mods that were not found",
                    "optifine\\OptiFine"
                ]),
            Rules.Text("optifine.world_load.chunk_manager",
                CrashReasonCode.OptiFineWorldLoadCrash,
                CrashRulePriority.High,
                CrashLogSection.Game,
                all:
                [
                    "net.minecraft.world.server.ChunkManager$ProxyTicketManager.shouldForceTicks",
                    "OptiFine"
                ]),
            Rules.Text("optifine.shader_mod.duplicate",
                CrashReasonCode.ShadersModWithOptiFine,
                CrashRulePriority.High,
                CrashLogSection.Game,
                ["Shaders Mod detected. Please remove it, OptiFine has built-in support for shaders."])
        ];
    }

    /// <summary>
    ///     世界内容相关规则，用于提取导致崩溃的方块或实体。
    /// </summary>
    private static IReadOnlyList<ICrashRule> _CreateWorldRules()
    {
        return
        [
            Rules.Regex("world.block.specific",
                CrashReasonCode.SpecificBlockCrash,
                CrashRulePriority.Low,
                CrashLogSection.CrashReport,
                Rules.Pattern(
                    @"\tBlock: Block\{(?<BlockName>[^}]+)}[\s\S]+?\tBlock location: World: (?<Location>\([^\)]+\))",
                    RegexOptions.IgnoreCase),
                [new CrashParameterMapping(CrashFindingParameterNames.BlockName, "BlockName")],
                CrashRuleBehavior.Continue),
            Rules.Regex("world.entity.specific",
                CrashReasonCode.SpecificEntityCrash,
                CrashRulePriority.Low,
                CrashLogSection.CrashReport,
                Rules.Pattern(
                    @"\tEntity Type: (?<EntityName>[^\n]+?)(?= \()[\s\S]+?\tEntity's Exact location: (?<Location>[^\n]+)",
                    RegexOptions.IgnoreCase),
                [new CrashParameterMapping(CrashFindingParameterNames.EntityName, "EntityName")],
                CrashRuleBehavior.Continue),
            Rules.Text("debug.manual_crash",
                CrashReasonCode.ManualDebugCrash,
                CrashRulePriority.High,
                CrashLogSection.CrashReport,
                ["Manually triggered debug crash"])
        ];
    }

    private static string? _FindMixinJsonName(string text)
    {
        var match = Rules.Pattern(@"^[^\t]+[ \[{(]{1}(?<name>[^ \[{(]+\.[^ ]+)(?=\.json)",
            RegexOptions.Multiline | RegexOptions.IgnoreCase).Match(text);
        return match.Success ? match.Groups["name"].Value : null;
    }

    #endregion

    #region CrashRuleBase Implementations

    private sealed class NoAnalyzableLogRule : CrashRuleBase
    {
        public override string Id => "log.none";
        public override CrashRulePriority Priority => CrashRulePriority.Critical;
        public override CrashRuleBehavior Behavior => CrashRuleBehavior.StopAll;

        public override bool IsMatch(CrashRuleContext context)
        {
            return !context.Logs.HasAnalyzableContent;
        }

        public override CrashFinding CreateFinding(CrashRuleContext context)
        {
            return Finding(Id, CrashReasonCode.NoAnalyzableLog);
        }
    }

    private sealed class DuplicateModRule : CrashRuleBase
    {
        private IReadOnlyList<string> _matches = [];
        public override string Id => "mod.duplicate.detected";
        public override CrashRulePriority Priority => CrashRulePriority.High;

        public override bool IsMatch(CrashRuleContext context)
        {
            var text = context.Game.Text;
            if (!context.Game.Contains("DuplicateModsFoundException") &&
                !context.Game.Contains("Found duplicate mod") &&
                !context.Game.Contains("ModResolutionException: Duplicate")) return false;

            var matches = new List<string>();
            matches.AddRange(CrashTextUtils.MatchAll(text, @"(?<=Mod ID: ')\w+?(?=' from mod files:)",
                RegexOptions.IgnoreCase));
            matches.AddRange(CrashTextUtils.MatchAll(text, @"[^\\/\s]+\.jar", RegexOptions.IgnoreCase));
            _matches = matches.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
            return true;
        }

        public override CrashFinding CreateFinding(CrashRuleContext context)
        {
            return Finding(Id, CrashReasonCode.DuplicateModInstalled,
                [new CrashFindingParameter(CrashFindingParameterNames.ModNames, string.Join("\n - ", _matches))]);
        }
    }

    private sealed class FabricSolutionRule : CrashRuleBase
    {
        private string _detail = "";
        public override string Id => "fabric.solution.provided";
        public override CrashRulePriority Priority => CrashRulePriority.Medium;

        public override bool IsMatch(CrashRuleContext context)
        {
            var text = context.Game.Text;
            if (!context.Game.Contains("A potential solution has been determined") &&
                !context.Game.Contains("确定了一种可能的解决方法")) return false;

            _detail = _ExtractBulletBlock(text,
                "A potential solution has been determined:",
                "A potential solution has been determined, this may resolve your problem:",
                "确定了一种可能的解决方法，这样做可能会解决你的问题：");
            return true;
        }

        private static string _ExtractBulletBlock(string text, params string[] markers)
        {
            foreach (var marker in markers)
            {
                var start = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (start < 0) continue;

                var tail = text[(start + marker.Length)..];
                var lines = CrashTextUtils
                    .ReadLinesNormalized(tail)
                    .Select(static line => line.Trim())
                    .TakeWhile(static line => line.StartsWith('-') || line.StartsWith('•') || line.Length == 0)
                    .Where(static line => line.Length > 0)
                    .Select(static line => line.TrimStart('-', '•', ' '));

                return string.Join("\n", lines);
            }

            return string.Empty;
        }

        public override CrashFinding CreateFinding(CrashRuleContext context)
        {
            return Finding(Id, CrashReasonCode.FabricProvidedSolution,
                [new CrashFindingParameter(CrashFindingParameterNames.Detail, _detail)]);
        }
    }

    private sealed class FabricOrModLoaderErrorRule : CrashRuleBase
    {
        public override string Id => "modloader.error.resolution_failed";
        public override CrashRulePriority Priority => CrashRulePriority.Low;
        public override CrashRuleBehavior Behavior => CrashRuleBehavior.Continue;

        public override bool IsMatch(CrashRuleContext context)
        {
            return context.Game.Contains("Mod resolution failed");
        }

        public override CrashFinding CreateFinding(CrashRuleContext context)
        {
            return Finding(Id, CrashReasonCode.ModLoaderError, confidence: CrashFindingConfidence.Medium);
        }
    }

    private sealed class ForgeErrorRule : CrashRuleBase
    {
        private string _detail = "";
        public override string Id => "forge.error.screen";
        public override CrashRulePriority Priority => CrashRulePriority.Medium;

        public override bool IsMatch(CrashRuleContext context)
        {
            if (!context.Game.Contains("An exception was thrown, the game will display an error screen and halt."))
                return false;
            var match = Rules
                .Pattern(
                    @"the game will display an error screen and halt.[\n\r]+[^\n]+?Exception: (?<detail>[\s\S]+?)(?=\n\tat)",
                    RegexOptions.IgnoreCase).Match(context.Game.Text);
            _detail = match.Success ? match.Groups["detail"].Value.Trim() : "";
            return true;
        }

        public override CrashFinding CreateFinding(CrashRuleContext context)
        {
            return Finding(Id, CrashReasonCode.ForgeError,
                [new CrashFindingParameter(CrashFindingParameterNames.Detail, _detail)]);
        }
    }

    private sealed class ModDependencyRule : CrashRuleBase
    {
        private string _detail = "";
        private CrashReasonCode _reason = CrashReasonCode.MissingDependencyOrWrongMinecraftVersion;
        public override string Id => "mod.dependency.missing_or_wrong_mc";
        public override CrashRulePriority Priority => CrashRulePriority.High;

        public override bool IsMatch(CrashRuleContext context)
        {
            if (context.Game.Contains("Missing or unsupported mandatory dependencies:"))
            {
                _reason = CrashReasonCode.MissingDependencyOrWrongMinecraftVersion;
                _detail = string.Join("\n",
                    CrashTextUtils.MatchAll(context.Game.Text,
                            @"(?<=Missing or unsupported mandatory dependencies:)([\n\r]+\t(.*))+",
                            RegexOptions.IgnoreCase)
                        .Select(static value => value.Trim('\r', '\n', '\t', ' ')));
                return true;
            }

            if (!context.Game.Contains("Incompatible mods found!")) return false;
            _reason = CrashReasonCode.IncompatibleMods;
            var match = Rules
                .Pattern(@"Incompatible mods found![\s\S]+: (?<detail>[\s\S]+?)(?=\tat )", RegexOptions.IgnoreCase)
                .Match(context.Game.Text);
            _detail = match.Success ? match.Groups["detail"].Value.Trim() : "";
            return true;
        }

        public override CrashFinding CreateFinding(CrashRuleContext context)
        {
            var requiresLoaderChange = _detail.Contains("mod loader", StringComparison.OrdinalIgnoreCase) ||
                                       _detail.Contains("loader", StringComparison.OrdinalIgnoreCase);
            var parameters = new List<CrashFindingParameter>
            {
                new(CrashFindingParameterNames.Detail, _detail)
            };
            if (requiresLoaderChange)
                parameters.Add(new CrashFindingParameter(CrashFindingParameterNames.RequiresModLoaderChange,
                    bool.TrueString));
            return Finding(Id, _reason, parameters);
        }
    }

    private sealed class MixinFailureRule : CrashRuleBase
    {
        private string _modName = "";
        public override string Id => "mod.mixin.failure";
        public override CrashRulePriority Priority => CrashRulePriority.Medium;

        public override bool IsMatch(CrashRuleContext context)
        {
            var text = context.Combined.Text;
            if (!context.Combined.Contains("Mixin prepare failed ") &&
                !context.Combined.Contains("Mixin apply failed ") &&
                !context.Combined.Contains("MixinApplyError") && !context.Combined.Contains("MixinTransformerError") &&
                !context.Combined.Contains("mixin.injection.throwables.") &&
                !context.Combined.Contains(".json] FAILED during )")) return false;

            _modName = CrashTextUtils.MatchFirst(text, @"(?<=from mod )[^.\/ ]+(?=\] from)", RegexOptions.IgnoreCase)
                       ?? CrashTextUtils.MatchFirst(text, @"(?<=for mod )[^.\/ ]+(?= failed)", RegexOptions.IgnoreCase)
                       ?? _FindMixinJsonName(text)
                       ?? "";
            _modName = _modName
                .Replace("mixins", "mixin", StringComparison.OrdinalIgnoreCase)
                .Replace(".mixin", "", StringComparison.OrdinalIgnoreCase)
                .Replace("mixin.", "", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        public override CrashFinding CreateFinding(CrashRuleContext context)
        {
            var names = CrashStackAnalyzer.ResolveModNames([_modName], context);
            var selectedNames = names.Count > 0
                ? names
                : new List<string> { _modName };
            var value = string.Join("\n - ", selectedNames);

            return Finding(Id,
                CrashReasonCode.ModMixinFailed,
                [new CrashFindingParameter(CrashFindingParameterNames.ModNames, value)]);
        }
    }

    private sealed class ConfirmedModCrashRule : CrashRuleBase
    {
        private IReadOnlyList<string> _names = [];
        public override string Id => "mod.confirmed.crash";
        public override CrashRulePriority Priority => CrashRulePriority.High;

        public override bool IsMatch(CrashRuleContext context)
        {
            var candidates = new List<string>();
            if (context.Game.Contains("Caught exception from "))
                candidates.AddRange(CrashTextUtils.MatchAll(context.Game.Text,
                    @"(?<=Caught exception from )[^\n]+",
                    RegexOptions.IgnoreCase));

            if (context.Game.Contains("due to errors, provided by "))
                candidates.AddRange(CrashTextUtils.MatchAll(context.Game.Text,
                    "(?<=due to errors, provided by ')[^']+",
                    RegexOptions.IgnoreCase));

            if (context.CrashReport.Contains("-- MOD "))
                candidates.AddRange(CrashTextUtils.MatchAll(context.CrashReport.Text,
                    "(?<=Mod File: ).+",
                    RegexOptions.IgnoreCase));

            candidates.AddRange(CrashTextUtils.MatchAll(context.CrashReport.Text,
                @"(?<=LoaderExceptionModCrash: Caught exception from )[^\n]+",
                RegexOptions.IgnoreCase));
            candidates.AddRange(CrashTextUtils.MatchAll(context.CrashReport.Text,
                "(?<=Multiple entries with same key: )[^=]+",
                RegexOptions.IgnoreCase));
            _names = CrashStackAnalyzer.ResolveModNames(
                candidates
                    .Select(static value => value.Trim())
                    .Where(static value => value.Length > 0)
                    .Take(10)
                    .ToList(), context);

            return _names.Count > 0 || candidates.Count > 0;
        }

        public override CrashFinding CreateFinding(CrashRuleContext context)
        {
            return Finding(Id,
                CrashReasonCode.ConfirmedModCrash,
                [new CrashFindingParameter(CrashFindingParameterNames.ModNames, string.Join("\n - ", _names))]);
        }
    }

    private sealed class SuspectedModCrashRule : CrashRuleBase
    {
        private IReadOnlyList<string> _names = [];
        public override string Id => "mod.suspected.crash_report";
        public override CrashRulePriority Priority => CrashRulePriority.Medium;

        public override bool IsMatch(CrashRuleContext context)
        {
            if (!context.CrashReport.Contains("Suspected Mod")) return false;

            var suspectsRaw = CrashTextUtils.Between(context.CrashReport.Text,
                "Suspected Mod",
                "Stacktrace");
            if (suspectsRaw.StartsWith("s: None", StringComparison.OrdinalIgnoreCase)) return false;

            var matches = Rules
                .Pattern(@"\n\t[^\(\t]+\((?<name>[^)\n]+)", RegexOptions.IgnoreCase)
                .Matches(suspectsRaw)
                .Select(static match => match.Groups["name"].Value)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            _names = CrashStackAnalyzer.ResolveModNames(matches, context);

            return _names.Count > 0;
        }

        public override CrashFinding CreateFinding(CrashRuleContext context)
        {
            return Finding(Id,
                CrashReasonCode.SuspectedModCrash,
                [new CrashFindingParameter(CrashFindingParameterNames.ModNames, string.Join("\n - ", _names))],
                CrashFindingConfidence.Medium);
        }
    }

    private sealed class ModConfigCrashRule : CrashRuleBase
    {
        private string _detail = "";
        public override string Id => "mod.config.failed_loading";
        public override CrashRulePriority Priority => CrashRulePriority.High;

        public override bool IsMatch(CrashRuleContext context)
        {
            if (!context.CrashReport.Contains("Failed loading config file ")) return false;

            var modMatch = Rules
                .Pattern(@"Failed loading config file .+ for modid (?<mod>[^\n]+)", RegexOptions.IgnoreCase)
                .Match(context.CrashReport.Text);
            var configMatch = Rules
                .Pattern("Failed loading config file (?<config>.+?)(?= of type)", RegexOptions.IgnoreCase)
                .Match(context.CrashReport.Text);
            var modId = modMatch.Success
                ? modMatch.Groups["mod"].Value
                : "";
            var config = configMatch.Success
                ? configMatch.Groups["config"].Value
                : "";
            _detail = string.Join("\n", new[] { modId, config }
                .Where(static value => !string.IsNullOrWhiteSpace(value)));

            return true;
        }

        public override CrashFinding CreateFinding(CrashRuleContext context)
        {
            return Finding(Id,
                CrashReasonCode.ModConfigCrash,
                [new CrashFindingParameter(CrashFindingParameterNames.Detail, _detail)]);
        }
    }

    private sealed class ModInitializationFailureRule : CrashRuleBase
    {
        private string _modName = "";
        public override string Id => "mod.initialization.failed";
        public override CrashRulePriority Priority => CrashRulePriority.Low;
        public override CrashRuleBehavior Behavior => CrashRuleBehavior.Continue;

        public override bool IsMatch(CrashRuleContext context)
        {
            if (!context.Game.Contains("Failed to create mod instance.")) return false;
            _modName = CrashTextUtils.MatchFirst(context.Game.Text,
                           "(?<=Failed to create mod instance. ModID: )[^,]+",
                           RegexOptions.IgnoreCase) ??
                       CrashTextUtils.MatchFirst(context.Game.Text,
                           @"(?<=Failed to create mod instance. ModId )[^\n]+(?= for )",
                           RegexOptions.IgnoreCase) ??
                       "";
            return true;
        }

        public override CrashFinding CreateFinding(CrashRuleContext context)
        {
            return Finding(Id,
                CrashReasonCode.ModInitializationFailed,
                [new CrashFindingParameter(CrashFindingParameterNames.ModName, _modName)],
                CrashFindingConfidence.Medium);
        }
    }

    private sealed class StackTraceRule(CrashStackAnalyzer analyzer) : CrashRuleBase
    {
        private CrashFinding? _finding;

        public override string Id => "stacktrace.mod_keyword";
        public override CrashRulePriority Priority => CrashRulePriority.StackTrace;

        public override bool IsMatch(CrashRuleContext context)
        {
            _finding = CrashStackAnalyzer.Analyze(context);
            return _finding is not null;
        }

        public override CrashFinding CreateFinding(CrashRuleContext context)
        {
            return _finding!;
        }
    }

    private sealed class VeryShortOutputRule : CrashRuleBase
    {
        public override string Id => "log.very_short_output";
        public override CrashRulePriority Priority => CrashRulePriority.Low;
        public override CrashRuleBehavior Behavior => CrashRuleBehavior.Continue;

        public override bool IsMatch(CrashRuleContext context)
        {
            return !context.Game.Contains("at net.") && !context.Game.Contains("INFO]") &&
                   context.JavaError.IsEmpty && context.CrashReport.IsEmpty &&
                   context.Game.Text.Length is > 0 and < 100;
        }

        public override CrashFinding CreateFinding(CrashRuleContext context)
        {
            return Finding(Id,
                CrashReasonCode.VeryShortProgramOutput,
                [new CrashFindingParameter(CrashFindingParameterNames.Detail, context.Game.Text)],
                CrashFindingConfidence.Low);
        }
    }

    #endregion
}
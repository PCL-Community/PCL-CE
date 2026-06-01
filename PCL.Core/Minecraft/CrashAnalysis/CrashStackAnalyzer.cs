using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     <p>基于异常堆栈推断可能相关的 Mod 或代码包名。</p>
///     <p>
///         堆栈分析只作为中低置信度补充：它在明确规则没有命中时尝试从堆栈帧中抽取关键词，
///         过滤 Java/Minecraft/加载器自身包名，再结合 Mod 列表反查可读 Mod 名称。
///         结果仍然是结构化参数，不在这里拼接用户文案。
///     </p>
/// </summary>
public sealed class CrashStackAnalyzer
{
    private const int MaxKeywordCount = 20;

    private static readonly string[] _IgnoredStackPrefixes =
    [
        "java", "sun", "javax", "jdk", "oolloo", "org.lwjgl", "com.sun", "net.minecraftforge",
        "paulscode.sound", "com.mojang", "net.minecraft", "cpw.mods", "com.google", "org.apache",
        "org.spongepowered", "net.fabricmc", "com.mumfrey", "org.quiltmc", "com.electronwill.nightconfig",
        "it.unimi.dsi", "MojangTricksIntelDriversForPerformance_javaw"
    ];

    private static readonly HashSet<string> _IgnoredWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "com", "org", "net", "asm", "fml", "mod", "jar", "sun", "lib", "map", "gui", "dev", "nio",
        "api", "dsi", "top", "mcp", "core", "init", "mods", "main", "file", "game", "load", "read",
        "done", "util", "tile", "item", "base", "oshi", "impl", "data", "pool", "task", "forge",
        "setup", "block", "model", "mixin", "event", "unimi", "netty", "world", "lwjgl", "gitlab",
        "common", "server", "config", "mixins", "compat", "loader", "launch", "entity", "assist",
        "client", "plugin", "modapi", "mojang", "shader", "events", "github", "recipe", "render",
        "packet", "preinit", "preload", "machine", "reflect", "channel", "general", "handler", "content",
        "systems", "modules", "service", "fastutil", "optifine", "internal", "platform", "override",
        "fabricmc", "neoforge", "injection", "listeners", "scheduler", "minecraft", "universal", "multipart",
        "neoforged", "microsoft", "transformer", "transformers", "minecraftforge", "blockentity", "spongepowered",
        "electronwill"
    };

    private static readonly Regex _StackPackageRegex = new(
        @"\n[^{]*(?<stack>[a-zA-Z_]\w+\.[a-zA-Z_][\w\.]+)(?=\.[\w\.$]+\.)",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(500));

    private static readonly Regex _MixinStackRegex = new(
        @"at [^(]+?\.\w+\$\w+\$(?<stack>[\w\$]+?)(?=\$\w+\()",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(500));

    public static CrashFinding? Analyze(CrashRuleContext context)
    {
        if (!context.IsModdedGame) return null;

        var stackBlocks = _ExtractStackBlocks(context);
        var keywords = _ExtractKeywords(stackBlocks);
        if (keywords.Count is 0 or > MaxKeywordCount) return null;

        var modNames = ResolveModNames(keywords, context);
        if (modNames.Count > 0)
            return new CrashFinding
            {
                RuleId = "stacktrace.mod_name",
                Reason = CrashReasonCode.StackTraceModName,
                Confidence = CrashFindingConfidence.Medium,
                Parameters =
                    [new CrashFindingParameter(CrashFindingParameterNames.ModNames, string.Join("\n - ", modNames))]
            };

        return new CrashFinding
        {
            RuleId = "stacktrace.keyword",
            Reason = CrashReasonCode.StackTraceKeyword,
            Confidence = CrashFindingConfidence.Low,
            Parameters = [new CrashFindingParameter(CrashFindingParameterNames.Keywords, string.Join(", ", keywords))]
        };
    }

    public static IReadOnlyList<string> ResolveModNames(IReadOnlyList<string> keywords, CrashRuleContext context)
    {
        if (keywords.Count == 0) return [];

        var normalizedKeywords = keywords
            .SelectMany(static keyword => keyword.Split('('))
            .Select(static keyword => keyword.Trim(' ', ')', '\r', '\n', '\t'))
            .Where(static keyword => keyword.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var names = new List<string>();
        names.AddRange(_ResolveFromCrashReport(normalizedKeywords, context.CrashReport.Text));
        names.AddRange(_ResolveFromDebugLog(normalizedKeywords, context.Debug.Text));

        if (names.Count == 0)
            names.AddRange(normalizedKeywords
                .Where(static keyword => !string.IsNullOrWhiteSpace(keyword)));

        return names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static IReadOnlyList<string> _ResolveFromCrashReport(IReadOnlyList<string> keywords, string crashReport)
    {
        if (string.IsNullOrWhiteSpace(crashReport) || !crashReport.Contains("A detailed walkthrough of the error",
                StringComparison.OrdinalIgnoreCase)) return [];

        var details = crashReport
            .Replace("A detailed walkthrough of the error", "¨", StringComparison.OrdinalIgnoreCase);
        var isFabricDetail = details
            .Contains("Fabric Mods", StringComparison.OrdinalIgnoreCase);

        if (isFabricDetail)
            details = details.Replace("Fabric Mods", "¨", StringComparison.OrdinalIgnoreCase);
        if (details.Contains("quilt-loader", StringComparison.OrdinalIgnoreCase))
            details = details.Replace("Mod Table Version", "¨", StringComparison.OrdinalIgnoreCase);

        details = CrashTextUtils.AfterLast(details, "¨");

        var modLines = CrashTextUtils.ReadLinesNormalized(details)
            .Where(line =>
                (line.Contains(".jar", StringComparison.OrdinalIgnoreCase) &&
                 line.Length - line.Replace(".jar", "", StringComparison.OrdinalIgnoreCase).Length == 4) ||
                (isFabricDetail && line.StartsWith("\t\t", StringComparison.Ordinal) && !Regex.IsMatch(line,
                    @"\t\tfabric[\w-]*: Fabric", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500))))
            .ToList();

        return _ExtractNamesFromLines(keywords, modLines, isFabricDetail);
    }

    private static IReadOnlyList<string> _ResolveFromDebugLog(IReadOnlyList<string> keywords, string debugLog)
    {
        var modLines = CrashTextUtils.MatchAll(debugLog,
            "(?<=valid mod file ).*",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        return (
            from keyword in keywords
            from modLine in modLines
            where modLine.Contains($"{{{keyword}}}", StringComparison.OrdinalIgnoreCase)
            select CrashTextUtils.MatchFirst(modLine, ".*(?= with)", RegexOptions.IgnoreCase)
            into name
            where !string.IsNullOrWhiteSpace(name)
            select name.Trim()
        ).ToList();
    }

    private static IReadOnlyList<string> _ExtractNamesFromLines(
        IReadOnlyList<string> keywords,
        IReadOnlyList<string> modLines,
        bool isFabricDetail)
    {
        var result = new List<string>();
        foreach (var keyword in keywords)
        {
            var keywordNormalized = keyword
                .Replace("_", "", StringComparison.Ordinal)
                .ToLowerInvariant();

            foreach (var line in modLines)
            {
                var normalizedLine = line
                    .Replace("_", "", StringComparison.Ordinal)
                    .ToLowerInvariant();

                if (!normalizedLine.Contains(keywordNormalized, StringComparison.Ordinal)) continue;
                if (normalizedLine.Contains("minecraft.jar", StringComparison.Ordinal) ||
                    normalizedLine.Contains(" forge-", StringComparison.Ordinal) ||
                    normalizedLine.Contains(" mixin-", StringComparison.Ordinal)) continue;

                var name = isFabricDetail
                    ? CrashTextUtils.MatchFirst(line,
                        @"(?<=: )[^\n]+(?= [^\n]+)",
                        RegexOptions.IgnoreCase)
                    : CrashTextUtils.MatchFirst(line,
                        @"(?<=\()[^\t]+\.jar(?=\))|(?<=(\t\t)|(\| ))[^\t\|]+\.jar",
                        RegexOptions.IgnoreCase);
                if (!string.IsNullOrWhiteSpace(name)) result.Add(name.Trim());

                break;
            }
        }

        return result;
    }

    private static IReadOnlyList<string> _ExtractStackBlocks(CrashRuleContext context)
    {
        var blocks = new List<string>();
        if (!context.CrashReport.IsEmpty)
            blocks.Add(CrashTextUtils.BeforeFirst(context.CrashReport.Text, "System Details"));
        if (!context.Game.IsEmpty)
        {
            blocks.AddRange(CrashTextUtils.MatchAll(
                context.Game.Text,
                @"/FATAL] .+?(?=[\n]+\[)",
                RegexOptions.Singleline));

            if (context.Game.Contains("Unreported exception thrown!"))
                blocks.Add(CrashTextUtils.Between(context.Game.Text,
                    "Unreported exception thrown!",
                    "at oolloo.jlw.Wrapper"));
        }

        if (!context.JavaError.IsEmpty)
            blocks.Add(CrashTextUtils.Between(context.JavaError.Text, "T H R E A D", "Registers:"));
        return blocks
            .Where(static block => !string.IsNullOrWhiteSpace(block))
            .ToList();
    }

    private static IReadOnlyList<string> _ExtractKeywords(IReadOnlyList<string> stackBlocks)
    {
        var stacks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var block in stackBlocks)
        {
            var wrapped = "\n" + block + "\n";

            foreach (Match match in _StackPackageRegex.Matches(wrapped))
                stacks.Add(match.Groups["stack"].Value.Trim());
            foreach (Match match in _MixinStackRegex.Matches(wrapped))
                stacks.Add(match.Groups["stack"].Value.Replace('$', '.').Trim());
        }

        var possibleStacks = stacks
            .Where(static stack =>
                !_IgnoredStackPrefixes.Any(prefix =>
                    stack.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var split in possibleStacks
                     .Select(stack => stack.Split('.', StringSplitOptions.RemoveEmptyEntries)))
            for (var i = 0; i <= Math.Min(3, split.Length - 1); i++)
            {
                var word = split[i].Trim();
                if (word.Length <= 2 || word.StartsWith("func_", StringComparison.OrdinalIgnoreCase)) continue;
                if (_IgnoredWords.Contains(word)) continue;
                words.Add(word);
            }

        return words.Take(MaxKeywordCount + 1).ToList();
    }
}
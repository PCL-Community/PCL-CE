using System.Text.RegularExpressions;
using PCL.Core.Logging;

namespace PCL;

internal sealed partial class CrashStackAnalyzer
{
    private static readonly HashSet<string> IgnoredPrefixes = new(StringComparer.Ordinal)
    {
        "java", "sun", "javax", "jdk", "com.sun",
        "com.mojang", "net.minecraft", "MojangTricksIntelDriversForPerformance_javaw",
        "net.minecraftforge", "cpw.mods", "net.fabricmc", "org.quiltmc", "org.spongepowered", "com.mumfrey",
        "org.lwjgl", "paulscode.sound", "com.google", "org.apache", "com.electronwill.nightconfig", "it.unimi.dsi",
        "oolloo"
    };

    private static readonly HashSet<string> IgnoredWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "com", "org", "net", "top", "dev", "gitlab", "github",
        "sun", "lib", "nio", "api", "asm", "reflect", "internal", "microsoft",
        "mcp", "fml", "forge", "fabricmc", "neoforge", "neoforged", "minecraftforge", "minecraft", "mojang",
        "mod", "mods", "modapi", "jar", "loader", "launch", "preinit", "preload", "init", "setup", "plugin",
        "mixin", "mixins", "injection", "transformer", "transformers", "spongepowered",
        "game", "world", "server", "client", "common", "entity", "block", "item", "tile", "blockentity",
        "gui", "model", "render", "shader", "optifine",
        "config", "data", "file", "read", "recipe", "content", "general",
        "event", "events", "handler", "listeners", "assist", "override",
        "netty", "packet", "channel",
        "task", "pool", "scheduler", "systems", "system", "modules", "service", "platform",
        "core", "main", "base", "util", "impl", "done", "map", "load", "machine",
        "dsi", "unimi", "fastutil", "lwjgl", "oshi", "electronwill",
        "compat", "universal", "multipart"
    };

    public CrashFinding? Analyze(
        CrashLogSet logs,
        CrashModIndex modIndex)
    {
        if (!_ShouldAnalyzeStack(logs.All))
        {
            LogWrapper.Info("Crash", "可能并未安装 Mod，不进行堆栈分析");
            return null;
        }

        var stackText = _CollectStackText(logs);
        var packages = _ExtractPackages(stackText).ToList();
        var keywords = _ExtractKeywords(packages).ToList();

        if (keywords.Count is 0 or > 10)
        {
            if (keywords.Count > 10)
                LogWrapper.Info("Crash", "关键词过多，考虑匹配出错，不纳入考虑");

            return null;
        }

        var mods = modIndex.ResolveMany(keywords).ToList();

        if (mods.Count != 0)
            return new CrashFinding(
                CrashCause.StackModNameFound,
                CrashConfidence.Medium,
                mods.Select(mod => new CrashEvidence
                {
                    Source = CrashLogKind.All,
                    Value = mod.DisplayNameOrFileName,
                    DisplayKind = "mod"
                }))
            {
                ShouldStop = true
            };

        return new CrashFinding(
            CrashCause.StackKeywordFound,
            CrashConfidence.Low,
            keywords.Select(keyword => new CrashEvidence
            {
                Source = CrashLogKind.All,
                Value = keyword,
                DisplayKind = "keyword"
            }))
        {
            ShouldStop = true
        };
    }

    private static bool _ShouldAnalyzeStack(string allLogs)
    {
        return allLogs.Contains("forge", StringComparison.OrdinalIgnoreCase) ||
               allLogs.Contains("fabric", StringComparison.OrdinalIgnoreCase) ||
               allLogs.Contains("quilt", StringComparison.OrdinalIgnoreCase) ||
               allLogs.Contains("liteloader", StringComparison.OrdinalIgnoreCase);
    }

    private static string _CollectStackText(CrashLogSet logs)
    {
        var result = new List<string>();

        if (logs.CrashReport?.Text is not null)
        {
            LogWrapper.Info("Crash", "开始进行崩溃日志堆栈分析");
            result.Add(CrashText.BeforeFirst(logs.CrashReport.Text, "System Details"));
        }

        if (logs.Game?.Text is not null)
        {
            var fatals = CrashRegex.All(logs.Game.Text, @"/FATAL] .+?(?=[\n]+\[)");

            if (logs.Game.Text.Contains("Unreported exception thrown!", StringComparison.Ordinal))
                fatals.Add(
                    CrashText.Between(logs.Game.Text,
                        "Unreported exception thrown!",
                        "at oolloo.jlw.Wrapper"));

            LogWrapper.Info("Crash", "开始进行 Minecraft 日志堆栈分析，发现 " + fatals.Count + " 个报错项");
            result.AddRange(fatals);
        }

        if (logs.HsErr?.Text is not null)
        {
            LogWrapper.Info("Crash", "开始进行虚拟机堆栈分析");
            result.Add(CrashText.Between(logs.HsErr.Text, "T H R E A D", "Registers:"));
        }

        return "\n" + string.Join("\n", result) + "\n";
    }

    private static IEnumerable<string> _ExtractPackages(string stackText)
    {
        var packages = new List<string>();

        packages.AddRange(
            PackageRegex()
                .Matches(stackText)
                .Select(match => match.Value));

        packages.AddRange(
            MixinStackRegex()
                .Matches(stackText)
                .Select(match => match.Value.Replace("$", ".")));

        var possibleStacks = packages
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .Where(item => !IgnoredPrefixes.Any(prefix => item.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        LogWrapper.Info("Crash", "找到 " + possibleStacks.Count + " 条可能的堆栈信息");

        foreach (var stack in possibleStacks)
            LogWrapper.Info("Crash", " - " + stack);

        return possibleStacks;
    }

    private static IEnumerable<string> _ExtractKeywords(IEnumerable<string> packages)
    {
        var words = new List<string>();

        foreach (var package in packages)
        {
            var split = package.Split('.');

            for (var i = 0; i <= Math.Min(3, split.Length - 1); i++)
            {
                var word = split[i].Trim();

                if (word.Length <= 2 ||
                    word.StartsWith("func_", StringComparison.Ordinal) ||
                    IgnoredWords.Contains(word))
                    continue;

                words.Add(word);
            }
        }

        var result = words
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        LogWrapper.Info("Crash", "从堆栈信息中找到 " + result.Count + " 个可能的 Mod ID 关键词");

        if (result.Count != 0)
            LogWrapper.Info("Crash", " - " + string.Join(", ", result));

        return result;
    }

    [GeneratedRegex(
        @"(?<=\n[^{]+)[a-zA-Z_]+\w+\.[a-zA-Z_]+[\w\.]+(?=\.[\w\.$]+\.)",
        RegexOptions.Compiled)]
    private static partial Regex PackageRegex();

    [GeneratedRegex(
        @"(?<=at [^(]+?\.\w+\$\w+\$)[\w\$]+?(?=\$\w+\()",
        RegexOptions.Compiled)]
    private static partial Regex MixinStackRegex();
}
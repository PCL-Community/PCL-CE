using System.Text.RegularExpressions;

namespace PCL;

internal sealed class CrashEvidenceExtractor
{
    public IEnumerable<CrashFinding> Extract(
        DetectionPhase phase,
        CrashLogSet logs,
        CrashModIndex modIndex)
    {
        return phase switch
        {
            DetectionPhase.Fatal => _ExtractFatal(logs, modIndex),
            DetectionPhase.Primary => _ExtractPrimary(logs, modIndex),
            DetectionPhase.Secondary => _ExtractSecondary(logs, modIndex),
            _ => []
        };
    }

    private static IEnumerable<CrashFinding> _ExtractFatal(
        CrashLogSet logs,
        CrashModIndex modIndex)
    {
        foreach (var finding in _ExtractOutOfMemory(logs))
            yield return finding;

        var memoryReservation = _ExtractJvmMemoryReservation(logs);
        if (memoryReservation is not null)
            yield return memoryReservation;

        foreach (var finding in _ExtractAccessViolation(logs))
            yield return finding;

        var signer = _ExtractSignerValidationFailure(logs);
        if (signer is not null)
            yield return signer;

        var optifineMissingMods = _ExtractOptiFineMissingMods(logs);
        if (optifineMissingMods is not null)
            yield return optifineMissingMods;

        foreach (var finding in _ExtractConfirmedModCrash(logs, modIndex))
            yield return finding;

        foreach (var finding in _ExtractDuplicateMods(logs))
            yield return finding;

        foreach (var finding in _ExtractMissingDependency(logs))
            yield return finding;
    }

    private static IEnumerable<CrashFinding> _ExtractPrimary(
        CrashLogSet logs,
        CrashModIndex modIndex)
    {
        foreach (var finding in _ExtractMixinFailure(logs, modIndex))
            yield return finding;

        foreach (var finding in _ExtractForgeError(logs))
            yield return finding;

        foreach (var finding in _ExtractFabricSolution(logs))
            yield return finding;

        foreach (var finding in _ExtractFabricProvidedModCrash(logs, modIndex))
            yield return finding;

        foreach (var finding in _ExtractSuspectedMods(logs, modIndex))
            yield return finding;
    }

    private static IEnumerable<CrashFinding> _ExtractSecondary(
        CrashLogSet logs,
        CrashModIndex modIndex)
    {
        var shortOutput = _ExtractShortOutput(logs);
        if (shortOutput is not null)
            yield return shortOutput;

        var modLoader = _ExtractModLoaderError(logs);
        if (modLoader is not null)
            yield return modLoader;

        var modInitialization = _ExtractModInitializationFailure(logs, modIndex);
        if (modInitialization is not null)
            yield return modInitialization;

        foreach (var finding in _ExtractSpecificBlockAndEntity(logs))
            yield return finding;
    }

    private static IEnumerable<CrashFinding> _ExtractOutOfMemory(CrashLogSet logs)
    {
        var evidence = _FindPatterns(
                logs,
                [
                    (CrashLogKind.Game, "java.lang.OutOfMemoryError"),
                    (CrashLogKind.Game, "an out of memory error"),
                    (CrashLogKind.HsErr, "The system is out of physical RAM or swap space"),
                    (CrashLogKind.HsErr, "Out of Memory Error"),
                    (CrashLogKind.CrashReport, "java.lang.OutOfMemoryError")
                ])
            .ToList();

        if (evidence.Count == 0)
            yield break;

        yield return new CrashFinding(
            CrashCause.OutOfMemory,
            CrashConfidence.High,
            evidence)
        {
            ShouldStop = true
        };
    }

    private static CrashFinding? _ExtractJvmMemoryReservation(CrashLogSet logs)
    {
        var gameLog = logs.Game?.Text;

        if (string.IsNullOrEmpty(gameLog) ||
            !gameLog.Contains("Could not reserve enough space", StringComparison.Ordinal))
            return null;

        var cause = gameLog.Contains("for 1048576KB object heap", StringComparison.Ordinal)
            ? CrashCause.X86JavaMemoryLimit
            : CrashCause.OutOfMemory;

        return new CrashFinding(
            cause,
            CrashConfidence.High,
            [
                _Evidence(
                    "Could not reserve enough space",
                    CrashLogKind.Game,
                    "jvm-message")
            ])
        {
            ShouldStop = true
        };
    }

    private static IEnumerable<CrashFinding> _ExtractAccessViolation(CrashLogSet logs)
    {
        var hsLog = logs.HsErr?.Text;

        if (string.IsNullOrEmpty(hsLog) ||
            !hsLog.Contains("EXCEPTION_ACCESS_VIOLATION", StringComparison.Ordinal))
            yield break;

        if (hsLog.Contains("# C  [ig", StringComparison.Ordinal))
            yield return _DriverFinding(CrashCause.IntelDriverAccessViolation, "# C  [ig");

        if (hsLog.Contains("# C  [atio", StringComparison.Ordinal))
            yield return _DriverFinding(CrashCause.AmdDriverAccessViolation, "# C  [atio");

        if (hsLog.Contains("# C  [nvoglv", StringComparison.Ordinal))
            yield return _DriverFinding(CrashCause.NvidiaDriverAccessViolation, "# C  [nvoglv");
    }

    private static CrashFinding _DriverFinding(
        CrashCause cause,
        string pattern)
    {
        return new CrashFinding(
            cause,
            CrashConfidence.High,
            [_Evidence(pattern, CrashLogKind.HsErr, "driver-signature")])
        {
            ShouldStop = true
        };
    }

    private static CrashFinding? _ExtractSignerValidationFailure(CrashLogSet logs)
    {
        var gameLog = logs.Game?.Text;

        if (string.IsNullOrEmpty(gameLog) ||
            !gameLog.Contains(
                "signer information does not match signer information of other classes in the same package",
                StringComparison.Ordinal))
            return null;

        var detail = (CrashRegex.First(gameLog, "(?<=class \")[^']+(?=\"'s signer information)") ?? "")
            .TrimEnd('\r', '\n');

        return new CrashFinding(
            CrashCause.FileOrContentValidationFailed,
            CrashConfidence.High,
            [_Evidence(detail, CrashLogKind.Game, "class")])
        {
            ShouldStop = true
        };
    }

    private static CrashFinding? _ExtractOptiFineMissingMods(CrashLogSet logs)
    {
        var crash = logs.CrashReport?.Text;

        if (string.IsNullOrEmpty(crash) ||
            !crash.Contains("has mods that were not found", StringComparison.Ordinal) ||
            !CrashRegex.IsMatch(crash, @"The Mod File [^\n]+optifine\\OptiFine[^\n]+ has mods that were not found"))
            return null;

        return new CrashFinding(
            CrashCause.OptiFineForgeIncompatible,
            CrashConfidence.High,
            [_Evidence("OptiFine", CrashLogKind.CrashReport, "mod")])
        {
            ShouldStop = true
        };
    }

    private static IEnumerable<CrashFinding> _ExtractConfirmedModCrash(
        CrashLogSet logs,
        CrashModIndex modIndex)
    {
        var gameLog = logs.Game?.Text;

        if (!string.IsNullOrEmpty(gameLog) &&
            gameLog.Contains("Caught exception from ", StringComparison.Ordinal))
        {
            var hint = CrashRegex.First(gameLog, @"(?<=Caught exception from )[^\n]+?")
                ?.TrimEnd('\r', '\n', ' ');

            yield return _ModFinding(
                CrashCause.ConfirmedModCrash,
                modIndex.ResolveToDisplayNames([hint ?? ""]),
                CrashLogKind.Game,
                true);
        }

        var crash = logs.CrashReport?.Text;
        if (string.IsNullOrEmpty(crash))
            yield break;

        if (crash.Contains("-- MOD ", StringComparison.Ordinal))
        {
            var modSection = CrashText.Between(crash, "-- MOD ", "Failure message:");

            if (modSection.Contains(".jar", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = (CrashRegex.First(modSection, "(?<=Mod File: ).+") ?? "")
                    .TrimEnd('\r', '\n', ' ');

                yield return _ModFinding(
                    CrashCause.ConfirmedModCrash,
                    [fileName],
                    CrashLogKind.CrashReport,
                    true);
            }
            else
            {
                var message = (CrashRegex.First(crash, @"(?<=Failure message: )[\w\W]+?(?=\tMod)") ?? "")
                    .Replace("\t", " ")
                    .TrimEnd('\r', '\n', ' ');

                yield return _ModFinding(
                    CrashCause.ModLoaderError,
                    [message],
                    CrashLogKind.CrashReport,
                    true,
                    "loader-message");
            }
        }

        if (crash.Contains("Multiple entries with same key: ", StringComparison.Ordinal))
        {
            var hint = (CrashRegex.First(crash, "(?<=Multiple entries with same key: )[^=]+") ?? "")
                .TrimEnd('\r', '\n', ' ');

            yield return _ModFinding(
                CrashCause.ConfirmedModCrash,
                modIndex.ResolveToDisplayNames([hint]),
                CrashLogKind.CrashReport,
                true);
        }

        if (crash.Contains("LoaderExceptionModCrash: Caught exception from ", StringComparison.Ordinal))
        {
            var hint = (CrashRegex.First(crash, @"(?<=LoaderExceptionModCrash: Caught exception from )[^\n]+") ?? "")
                .TrimEnd('\r', '\n', ' ');

            yield return _ModFinding(
                CrashCause.ConfirmedModCrash,
                modIndex.ResolveToDisplayNames([hint]),
                CrashLogKind.CrashReport,
                true);
        }

        if (crash.Contains("Failed loading config file ", StringComparison.Ordinal))
        {
            var mod = (CrashRegex.First(crash, @"(?<=Failed loading config file .+ for modid )[^\n]+") ?? "")
                .TrimEnd('\r', '\n');

            var config = (CrashRegex.First(crash, "(?<=Failed loading config file ).+(?= of type)") ?? "")
                .TrimEnd('\r', '\n');

            var resolved = modIndex.ResolveToDisplayNames([mod]);

            var evidence = new List<CrashEvidence>();
            evidence.AddRange(resolved.Select(item => _Evidence(item, CrashLogKind.CrashReport, "mod")));

            if (!string.IsNullOrWhiteSpace(config))
                evidence.Add(_Evidence(config, CrashLogKind.CrashReport, "config"));

            yield return new CrashFinding(
                CrashCause.ModConfigCrash,
                CrashConfidence.High,
                evidence)
            {
                ShouldStop = true
            };
        }
    }

    private static IEnumerable<CrashFinding> _ExtractDuplicateMods(CrashLogSet logs)
    {
        var gameLog = logs.Game?.Text;

        if (string.IsNullOrEmpty(gameLog))
            yield break;

        if (gameLog.Contains("DuplicateModsFoundException", StringComparison.Ordinal))
            yield return _ModFinding(
                CrashCause.DuplicateMods,
                CrashRegex.All(
                    gameLog,
                    @"(?<=\n\t[\w]+ : [A-Z]:[^\n]+(/|\\))[^/\\\n]+?.jar",
                    RegexOptions.IgnoreCase),
                CrashLogKind.Game,
                true);

        if (gameLog.Contains("Found a duplicate mod", StringComparison.Ordinal))
            yield return _ModFinding(
                CrashCause.DuplicateMods,
                CrashRegex.All(
                    CrashRegex.First(gameLog, @"Found a duplicate mod[^\n]+") ?? "",
                    @"[^\\/]+.jar",
                    RegexOptions.IgnoreCase),
                CrashLogKind.Game,
                true);

        if (gameLog.Contains("Found duplicate mods", StringComparison.Ordinal))
            yield return _ModFinding(
                CrashCause.DuplicateMods,
                CrashRegex.All(gameLog, @"(?<=Mod ID: ')\w+?(?=' from mod files:)")
                    .Distinct()
                    .ToList(),
                CrashLogKind.Game,
                true);

        if (gameLog.Contains("ModResolutionException: Duplicate", StringComparison.Ordinal))
            yield return _ModFinding(
                CrashCause.DuplicateMods,
                CrashRegex.All(
                    CrashRegex.First(gameLog, @"ModResolutionException: Duplicate[^\n]+") ?? "",
                    @"[^\\/]+.jar",
                    RegexOptions.IgnoreCase),
                CrashLogKind.Game,
                true);
    }

    private static IEnumerable<CrashFinding> _ExtractMissingDependency(CrashLogSet logs)
    {
        var gameLog = logs.Game?.Text;

        if (string.IsNullOrEmpty(gameLog))
            yield break;

        if (gameLog.Contains("Incompatible mods found!", StringComparison.Ordinal))
            yield return _ModFinding(
                CrashCause.IncompatibleMods,
                [CrashRegex.First(gameLog, @"(?<=Incompatible mods found![\s\S]+: )[\s\S]+?(?=\tat )") ?? ""],
                CrashLogKind.Game,
                true,
                "loader-message");

        if (gameLog.Contains("Missing or unsupported mandatory dependencies:", StringComparison.Ordinal))
        {
            var details = CrashRegex.All(
                    gameLog,
                    @"(?<=Missing or unsupported mandatory dependencies:)([\n\r]+\t(.*))+",
                    RegexOptions.IgnoreCase)
                .Select(item => item.Trim('\r', '\n', '\t', ' '))
                .Distinct()
                .ToList();

            yield return _ModFinding(
                CrashCause.MissingDependencyOrWrongMcVersion,
                details,
                CrashLogKind.Game,
                true,
                "loader-message");
        }
    }

    private static IEnumerable<CrashFinding> _ExtractMixinFailure(
        CrashLogSet logs,
        CrashModIndex modIndex)
    {
        return from source in new[] { CrashLogKind.Game, CrashLogKind.CrashReport }
            let text = logs.GetText(source)
            where !string.IsNullOrEmpty(text) && _LooksLikeMixinFailure(text)
            let modHints = _ExtractMixinModHints(text)
                .Distinct(StringComparer.Ordinal)
                .ToList()
            let resolved = modIndex.ResolveToDisplayNames(modHints)
            let hasResolved = modHints.Count > 0 && !modHints.SequenceEqual(resolved)
            let evidence = (resolved.Count != 0 ? resolved : modHints)
                .Select(item => _Evidence(item, source, hasResolved ? "mod" : "mod-hint"))
            select new CrashFinding(
                CrashCause.ModMixinFailed,
                hasResolved ? CrashConfidence.High : CrashConfidence.Medium,
                evidence)
            {
                ShouldStop = true
            };
    }

    private static bool _LooksLikeMixinFailure(string text)
    {
        return text.Contains("Mixin prepare failed ", StringComparison.Ordinal) ||
               text.Contains("Mixin apply failed ", StringComparison.Ordinal) ||
               text.Contains("MixinApplyError", StringComparison.Ordinal) ||
               text.Contains("MixinTransformerError", StringComparison.Ordinal) ||
               text.Contains("mixin.injection.throwables.", StringComparison.Ordinal) ||
               text.Contains(".json] FAILED during )", StringComparison.Ordinal);
    }

    private static IEnumerable<string> _ExtractMixinModHints(string text)
    {
        var patterns = new[]
        {
            new Regex(
                @"(?<=from mod )[^.\/ ]+(?=\] from)",
                RegexOptions.Compiled),

            new Regex(
                @"(?<=for mod )[^.\/ ]+(?= failed)",
                RegexOptions.Compiled),

            new Regex(
                @"(?<=^[^\t]+[ \[{(]{1})[^ \[{(]+\.[^ ]+(?=\.json)",
                RegexOptions.Compiled | RegexOptions.Multiline)
        };

        foreach (var pattern in patterns)
        foreach (Match match in pattern.Matches(text))
        {
            var value = match.Value
                .Replace("mixins", "mixin")
                .Replace(".mixin", "")
                .Replace("mixin.", "")
                .Trim();

            if (!string.IsNullOrEmpty(value))
                yield return value;
        }
    }

    private static IEnumerable<CrashFinding> _ExtractForgeError(CrashLogSet logs)
    {
        var gameLog = logs.Game?.Text;

        if (string.IsNullOrEmpty(gameLog) ||
            !gameLog.Contains(
                "An exception was thrown, the game will display an error screen and halt.",
                StringComparison.Ordinal))
            yield break;

        var message = CrashRegex.First(
                gameLog,
                @"(?<=the game will display an error screen and halt.[\n\r]+[^\n]+?Exception: )[\s\S]+?(?=\n\tat)")
            ?.Trim('\r', '\n') ?? "";

        yield return _ModFinding(
            CrashCause.ForgeError,
            [message],
            CrashLogKind.Game,
            true,
            "loader-message");
    }

    private static IEnumerable<CrashFinding> _ExtractFabricSolution(CrashLogSet logs)
    {
        var gameLog = logs.Game?.Text;

        if (string.IsNullOrEmpty(gameLog))
            yield break;

        var solution = "";

        if (gameLog.Contains("A potential solution has been determined:", StringComparison.Ordinal))
            solution = string.Join(
                "\n",
                CrashRegex.All(
                    CrashRegex.First(gameLog, @"(?<=A potential solution has been determined:\n)(\s+ - [^\n]+\n)+") ??
                    "",
                    @"(?<=\s+)[^\n]+"));
        else if (gameLog.Contains(
                     "A potential solution has been determined, this may resolve your problem:",
                     StringComparison.Ordinal))
            solution = string.Join(
                "\n",
                CrashRegex.All(
                    CrashRegex.First(
                        gameLog,
                        @"(?<=A potential solution has been determined, this may resolve your problem:\n)(\s+ - [^\n]+\n)+") ??
                    "",
                    @"(?<=\s+)[^\n]+"));
        else if (gameLog.Contains("确定了一种可能的解决方法，这样做可能会解决你的问题：", StringComparison.Ordinal))
            solution = string.Join(
                "\n",
                CrashRegex.All(
                    CrashRegex.First(gameLog, @"(?<=确定了一种可能的解决方法，这样做可能会解决你的问题：\n)(\s+ - [^\n]+\n)+") ?? "",
                    @"(?<=\s+)[^\n]+"));

        if (!string.IsNullOrWhiteSpace(solution))
            yield return _ModFinding(
                CrashCause.FabricSolutionProvided,
                [solution],
                CrashLogKind.Game,
                true,
                "loader-message");
    }

    private static IEnumerable<CrashFinding> _ExtractFabricProvidedModCrash(
        CrashLogSet logs,
        CrashModIndex modIndex)
    {
        var gameLog = logs.Game?.Text;

        if (string.IsNullOrEmpty(gameLog) ||
            !gameLog.Contains("due to errors, provided by ", StringComparison.Ordinal) ||
            _LooksLikeMixinFailure(gameLog))
            yield break;

        var hint = (CrashRegex.First(gameLog, "(?<=due to errors, provided by ')[^']+") ?? "")
            .TrimEnd('\r', '\n', ' ');

        yield return _ModFinding(
            CrashCause.ConfirmedModCrash,
            modIndex.ResolveToDisplayNames([hint]),
            CrashLogKind.Game,
            true);
    }

    private static IEnumerable<CrashFinding> _ExtractSuspectedMods(
        CrashLogSet logs,
        CrashModIndex modIndex)
    {
        var crash = logs.CrashReport?.Text;

        if (string.IsNullOrEmpty(crash) ||
            !crash.Contains("Suspected Mod", StringComparison.Ordinal))
            yield break;

        var suspectsRaw = CrashText.Between(crash, "Suspected Mod", "Stacktrace");

        if (suspectsRaw.StartsWith("s: None", StringComparison.Ordinal))
            yield break;

        var suspects = CrashRegex.All(suspectsRaw, @"(?<=\n\t[^(\t]+\()[^)\n]+");

        if (suspects.Count != 0)
            yield return _ModFinding(
                CrashCause.SuspectedModCrash,
                modIndex.ResolveToDisplayNames(suspects),
                CrashLogKind.CrashReport,
                true);
    }

    private static CrashFinding? _ExtractShortOutput(CrashLogSet logs)
    {
        var gameLog = logs.Game?.Text;

        if (string.IsNullOrEmpty(gameLog) ||
            logs.HsErr is not null ||
            logs.CrashReport is not null ||
            gameLog.Contains("at net.", StringComparison.Ordinal) ||
            gameLog.Contains("INFO]", StringComparison.Ordinal) ||
            gameLog.Length >= 100)
            return null;

        return _ModFinding(
            CrashCause.VeryShortOutput,
            [gameLog],
            CrashLogKind.Game,
            false,
            "raw-output");
    }

    private static CrashFinding? _ExtractModLoaderError(CrashLogSet logs)
    {
        var gameLog = logs.Game?.Text;

        if (string.IsNullOrEmpty(gameLog) ||
            !gameLog.Contains("Mod resolution failed", StringComparison.Ordinal))
            return null;

        return new CrashFinding(
            CrashCause.ModLoaderError,
            CrashConfidence.High);
    }

    private static CrashFinding? _ExtractModInitializationFailure(
        CrashLogSet logs,
        CrashModIndex modIndex)
    {
        var gameLog = logs.Game?.Text;

        if (string.IsNullOrEmpty(gameLog) ||
            !gameLog.Contains("Failed to create mod instance.", StringComparison.Ordinal))
            return null;

        var hint = (CrashRegex.First(gameLog, "(?<=Failed to create mod instance. ModID: )[^,]+") ??
                    CrashRegex.First(gameLog, @"(?<=Failed to create mod instance. ModId )[^\n]+(?= for )") ?? "")
            .TrimEnd('\r', '\n');

        return _ModFinding(
            CrashCause.ModInitializationFailed,
            modIndex.ResolveToDisplayNames([hint]),
            CrashLogKind.Game,
            false);
    }

    private static IEnumerable<CrashFinding> _ExtractSpecificBlockAndEntity(CrashLogSet logs)
    {
        var crash = logs.CrashReport?.Text;

        if (string.IsNullOrEmpty(crash))
            yield break;

        if (crash.Contains("\tBlock location: World: ", StringComparison.Ordinal))
        {
            var value =
                (CrashRegex.First(crash, @"(?<=\tBlock: Block\{)[^\}]+") ?? "") +
                " " +
                (CrashRegex.First(crash, @"(?<=\tBlock location: World: )\([^\)]+\)") ?? "");

            yield return _ModFinding(
                CrashCause.SpecificBlockCrash,
                [value],
                CrashLogKind.CrashReport,
                false,
                "block");
        }

        if (crash.Contains("\tEntity's Exact location: ", StringComparison.Ordinal))
        {
            var value =
                (CrashRegex.First(crash, @"(?<=\tEntity Type: )[^\n]+(?= \()") ?? "") +
                " (" +
                (CrashRegex.First(crash, @"(?<=\tEntity's Exact location: )[^\n]+") ?? "")
                .TrimEnd('\r', '\n') +
                ")";

            yield return _ModFinding(
                CrashCause.SpecificEntityCrash,
                [value],
                CrashLogKind.CrashReport,
                false,
                "entity");
        }
    }

    private static IEnumerable<CrashEvidence> _FindPatterns(
        CrashLogSet logs,
        IEnumerable<(CrashLogKind Source, string Pattern)> patterns)
    {
        foreach (var (source, pattern) in patterns)
        {
            var text = logs.GetText(source);

            if (text?.Contains(pattern, StringComparison.Ordinal) == true)
                yield return _Evidence(pattern, source, "pattern", pattern);
        }
    }

    private static CrashFinding _ModFinding(
        CrashCause cause,
        IEnumerable<string?> values,
        CrashLogKind source,
        bool shouldStop,
        string displayKind = "mod")
    {
        return new CrashFinding(
            cause,
            CrashConfidence.High,
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => _Evidence(value!, source, displayKind)))
        {
            ShouldStop = shouldStop
        };
    }

    private static CrashEvidence _Evidence(
        string value,
        CrashLogKind source,
        string displayKind,
        string? pattern = null)
    {
        return new CrashEvidence
        {
            Value = value,
            Source = source,
            Pattern = pattern,
            DisplayKind = displayKind
        };
    }
}
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;

namespace PCL;

internal sealed class CrashResultFormatter
{
    public CrashDialogContent Format(
        CrashAnalysisContext context,
        bool isHandAnalyze)
    {
        var result = context.Result ?? new CrashAnalysisResult();

        if (!result.Any)
            return _CreateUnknownContent(isHandAnalyze);

        var specs = result.Findings
            .Select(finding => _CreateMessageSpec(finding, context.LogAll))
            .ToList();

        var text = _JoinParagraphs(specs.Select(_Render));
        if (!isHandAnalyze)
            text = _AppendFollowUps(text, specs);

        return new CrashDialogContent(
            _NormalizeLineEndings(text).Trim('\r', '\n'),
            specs[0].SuggestedAction);
    }

    private static CrashDialogContent _CreateUnknownContent(bool isHandAnalyze)
    {
        var spec = isHandAnalyze
            ? _Spec("Crash.Reason.Unknown.Manual")
            : _Spec("Crash.Reason.Unknown.Auto", "Crash.Suggestion.ExportReport");

        return new CrashDialogContent(
            _NormalizeLineEndings(_Render(spec)).Trim('\r', '\n'),
            CrashSuggestedAction.None);
    }

    private static string _AppendFollowUps(
        string text,
        IReadOnlyCollection<CrashMessageSpec> specs)
    {
        var followUps = new List<string>();

        if (specs.Any(spec => spec.AppendReportHint))
            followUps.Add(Lang.Text("Crash.Suggestion.ViewReport"));

        if (specs.Any(spec => spec.AppendHelpHint))
            followUps.Add(Lang.Text("Crash.Suggestion.ExportReport"));

        var launcherOutdatedSuggestion = _GetLauncherOutdatedSuggestion();
        if (!string.IsNullOrWhiteSpace(launcherOutdatedSuggestion))
            followUps.Add(launcherOutdatedSuggestion);

        return followUps.Count == 0
            ? text
            : Lang.Text(
                "Crash.Presentation.WithFollowUps",
                text,
                _JoinParagraphs(followUps));
    }

    private static string _Render(CrashMessageSpec spec)
    {
        var reason = Lang.Text(spec.ReasonKey, spec.ReasonArgs);
        if (spec.SuggestionKey is null)
            return Lang.Text("Crash.Presentation.ReasonOnly", reason);

        return Lang.Text(
            "Crash.Presentation.WithSuggestion",
            reason,
            Lang.Text(spec.SuggestionKey, spec.SuggestionArgs));
    }

    private static CrashMessageSpec _CreateMessageSpec(
        CrashFinding finding,
        string combinedLogText)
    {
        var additional = finding.Details.ToList();

        switch (finding.Cause)
        {
            case CrashCause.ExtractedModFile:
                return _Spec(
                    "Crash.Reason.ExtractedModFile",
                    "Crash.Suggestion.ExtractedModFile");

            case CrashCause.OutOfMemory:
                return _Spec(
                    "Crash.Reason.OutOfMemory",
                    "Crash.Suggestion.OutOfMemory",
                    help: true);

            case CrashCause.UsingOpenJ9:
                return _Spec(
                    "Crash.Reason.UsingOpenJ9",
                    "Crash.Suggestion.UsingOpenJ9");

            case CrashCause.UsingJdk:
                return _Spec(
                    "Crash.Reason.UsingJdk",
                    "Crash.Suggestion.UsingJdk");

            case CrashCause.JavaTooNew:
                return _Spec(
                    "Crash.Reason.JavaTooNew",
                    "Crash.Suggestion.JavaTooNew");

            case CrashCause.JavaIncompatible:
                return _Spec(
                    "Crash.Reason.JavaIncompatible",
                    "Crash.Suggestion.JavaIncompatible");

            case CrashCause.InvalidModFileName:
                return _Spec(
                    "Crash.Reason.InvalidModFileName",
                    "Crash.Suggestion.InvalidModFileName");

            case CrashCause.MissingMixinBootstrap:
                return _Spec(
                    "Crash.Reason.MissingMixinBootstrap",
                    "Crash.Suggestion.MissingMixinBootstrap");

            case CrashCause.X86JavaMemoryLimit:
                return Environment.Is64BitOperatingSystem
                    ? _Spec(
                        "Crash.Reason.X86JavaMemoryLimit.OnX64Os",
                        "Crash.Suggestion.X86JavaMemoryLimit.OnX64Os")
                    : _Spec(
                        "Crash.Reason.X86JavaMemoryLimit.OnX86Os",
                        "Crash.Suggestion.X86JavaMemoryLimit.OnX86Os",
                        help: true);

            case CrashCause.MissingDependencyOrWrongMcVersion:
                return _FormatMissingDependency(additional);

            case CrashCause.StackKeywordFound:
                return additional.Count == 1
                    ? _Spec(
                        "Crash.Reason.StackKeyword.Single",
                        "Crash.Suggestion.StackKeyword", _Args(additional[0]),
                        help: true)
                    : _Spec(
                        "Crash.Reason.StackKeyword.Multiple",
                        "Crash.Suggestion.StackKeyword",
                        _Args(_BulletList(additional)),
                        help: true);

            case CrashCause.StackModNameFound or CrashCause.SuspectedModCrash:
                return additional.Count == 1
                    ? _Spec(
                        "Crash.Reason.SuspectedMod.Single",
                        "Crash.Suggestion.DisableThisMod",
                        _Args(additional[0]),
                        report: true,
                        help: true)
                    : _Spec(
                        "Crash.Reason.SuspectedMod.Multiple",
                        "Crash.Suggestion.DisableTheseMods",
                        _Args(_BulletList(additional)),
                        report: true,
                        help: true);

            case CrashCause.ConfirmedModCrash:
                return additional.Count == 1
                    ? _Spec(
                        "Crash.Reason.ConfirmedMod.Single",
                        "Crash.Suggestion.DisableThisMod",
                        _Args(additional[0]),
                        report: true,
                        help: true)
                    : _Spec(
                        "Crash.Reason.ConfirmedMod.Multiple",
                        "Crash.Suggestion.DisableTheseMods",
                        _Args(_BulletList(additional)),
                        report: true,
                        help: true);

            case CrashCause.ModMixinFailed:
                if (additional.Count == 0)
                    return _Spec(
                        "Crash.Reason.ModMixinFailed.None",
                        "Crash.Suggestion.ModMixinFailed",
                        report: true,
                        help: true);

                return additional.Count == 1
                    ? _Spec(
                        "Crash.Reason.ModMixinFailed.Single",
                        "Crash.Suggestion.DisableThisMod",
                        _Args(additional[0]),
                        report: true,
                        help: true)
                    : _Spec(
                        "Crash.Reason.ModMixinFailed.Multiple",
                        "Crash.Suggestion.DisableTheseMods",
                        _Args(_BulletList(additional)),
                        report: true,
                        help: true);

            case CrashCause.ModConfigCrash:
                return additional.Count > 1 && additional[1] is not null
                    ? _Spec(
                        "Crash.Reason.ModConfigCrash.WithConfig",
                        "Crash.Suggestion.ModConfigCrash",
                        _Args(additional[0], additional[1]))
                    : _Spec(
                        "Crash.Reason.ModConfigCrash.Simple",
                        "Crash.Suggestion.DisableThisMod",
                        _Args(additional.FirstOrDefault() ?? string.Empty),
                        report: true,
                        help: true);

            case CrashCause.ModInitializationFailed:
                return additional.Count == 1
                    ? _Spec(
                        "Crash.Reason.ModInitializationFailed.Single",
                        "Crash.Suggestion.DisableThisMod",
                        _Args(additional[0]),
                        report: true,
                        help: true)
                    : _Spec(
                        "Crash.Reason.ModInitializationFailed.Multiple",
                        "Crash.Suggestion.DisableTheseMods",
                        _Args(_BulletList(additional)),
                        report: true,
                        help: true);

            case CrashCause.SpecificBlockCrash:
                return additional.Count == 1
                    ? _Spec(
                        "Crash.Reason.SpecificBlock.Single",
                        "Crash.Suggestion.SpecificBlock.Single",
                        _Args(additional[0]),
                        help: true)
                    : _Spec(
                        "Crash.Reason.SpecificBlock.Multiple",
                        "Crash.Suggestion.SpecificBlock.Multiple",
                        help: true);

            case CrashCause.DuplicateMods:
                return additional.Count >= 2
                    ? _Spec(
                        "Crash.Reason.DuplicateMods.Known",
                        "Crash.Suggestion.DuplicateMods",
                        _Args(_BulletList(additional)))
                    : _Spec(
                        "Crash.Reason.DuplicateMods.Unknown",
                        "Crash.Suggestion.DuplicateMods",
                        report: true,
                        help: true);

            case CrashCause.SpecificEntityCrash:
                return additional.Count == 1
                    ? _Spec(
                        "Crash.Reason.SpecificEntity.Single",
                        "Crash.Suggestion.SpecificEntity.Single",
                        _Args(additional[0]),
                        help: true)
                    : _Spec(
                        "Crash.Reason.SpecificEntity.Multiple",
                        "Crash.Suggestion.SpecificEntity.Multiple",
                        help: true);

            case CrashCause.OptiFineForgeIncompatible:
                return _Spec(
                    "Crash.Reason.OptiFineForgeIncompatible",
                    "Crash.Suggestion.OptiFineForgeIncompatible");

            case CrashCause.ShadersModWithOptiFine:
                return _Spec(
                    "Crash.Reason.ShadersModWithOptiFine",
                    "Crash.Suggestion.ShadersModWithOptiFine");

            case CrashCause.OldForgeNewJavaIncompatible:
                return _Spec(
                    "Crash.Reason.OldForgeNewJavaIncompatible",
                    "Crash.Suggestion.OldForgeNewJavaIncompatible");

            case CrashCause.MultipleForgeInInstanceJson:
                return _Spec(
                    "Crash.Reason.MultipleForgeInInstanceJson",
                    "Crash.Suggestion.MultipleForgeInInstanceJson");

            case CrashCause.ManualDebugCrash:
                return _Spec("Crash.Reason.ManualDebugCrash");

            case CrashCause.ModRequiresJava11:
                return _Spec(
                    "Crash.Reason.ModRequiresJava11",
                    "Crash.Suggestion.ModRequiresJava11");

            case CrashCause.VeryShortOutput:
                return _Spec(
                    "Crash.Reason.VeryShortOutput",
                    "Crash.Suggestion.ExportReport",
                    _Args(additional.FirstOrDefault() ?? string.Empty));

            case CrashCause.OptiFineWorldLoadCrash:
                return _Spec(
                    "Crash.Reason.OptiFineWorldLoadCrash",
                    "Crash.Suggestion.OptiFineWorldLoadCrash",
                    help: true);

            case CrashCause.PixelFormatNotSupported
                or CrashCause.IntelDriverAccessViolation
                or CrashCause.AmdDriverAccessViolation
                or CrashCause.NvidiaDriverAccessViolation
                or CrashCause.UnsupportedOpenGl:
                return combinedLogText.Contains("hd graphics ")
                    ? _Spec(
                        "Crash.Reason.GraphicsDriver.IntelOrIntegrated",
                        "Crash.Suggestion.GraphicsDriver.IntelOrIntegrated",
                        help: true)
                    : _Spec(
                        "Crash.Reason.GraphicsDriver.Generic",
                        "Crash.Suggestion.GraphicsDriver.Generic",
                        help: true);

            case CrashCause.ResourcePackTooLarge:
                return _Spec(
                    "Crash.Reason.ResourcePackTooLarge",
                    "Crash.Suggestion.ResourcePackTooLarge",
                    help: true);

            case CrashCause.NightConfigBug:
                return _Spec(
                    "Crash.Reason.NightConfigBug",
                    "Crash.Suggestion.NightConfigBug",
                    help: true);

            case CrashCause.OpenGl1282:
                return _Spec(
                    "Crash.Reason.OpenGl1282",
                    "Crash.Suggestion.OpenGl1282",
                    help: true);

            case CrashCause.TooManyModsIdLimit:
                return _Spec(
                    "Crash.Reason.TooManyModsIdLimit",
                    "Crash.Suggestion.TooManyModsIdLimit");

            case CrashCause.FileOrContentValidationFailed:
                return _Spec(
                    "Crash.Reason.FileOrContentValidationFailed",
                    "Crash.Suggestion.FileOrContentValidationFailed",
                    help: true);

            case CrashCause.IncompleteForgeInstallation:
                return _Spec(
                    "Crash.Reason.IncompleteForgeInstallation",
                    "Crash.Suggestion.IncompleteForgeInstallation",
                    help: true);

            case CrashCause.FabricError:
                return additional.Count == 1
                    ? _Spec(
                        "Crash.Reason.FabricError.WithDetail",
                        "Crash.Suggestion.FollowLoaderInstructions",
                        _Args(additional[0]))
                    : _Spec(
                        "Crash.Reason.FabricError.Generic",
                        "Crash.Suggestion.FollowLoaderInstructions",
                        help: true);

            case CrashCause.IncompatibleMods:
                return _FormatIncompatibleMods(additional);

            case CrashCause.ModLoaderError:
                return additional.Count == 1
                    ? _Spec(
                        "Crash.Reason.ModLoaderError.WithDetail",
                        "Crash.Suggestion.FollowLoaderInstructions",
                        _Args(additional[0]))
                    : _Spec(
                        "Crash.Reason.ModLoaderError.Generic",
                        "Crash.Suggestion.FollowLoaderInstructions",
                        help: true);

            case CrashCause.FabricSolutionProvided:
                return additional.Count == 1
                    ? _Spec(
                        "Crash.Reason.FabricSolution.WithDetail",
                        "Crash.Suggestion.FollowLoaderInstructions",
                        _Args(additional[0]))
                    : _Spec(
                        "Crash.Reason.FabricSolution.Generic",
                        "Crash.Suggestion.FollowLoaderInstructions",
                        help: true);

            case CrashCause.ForgeError:
                return additional.Count == 1
                    ? _Spec(
                        "Crash.Reason.ForgeError.WithDetail",
                        "Crash.Suggestion.FollowLoaderInstructions",
                        _Args(additional[0]))
                    : _Spec(
                        "Crash.Reason.ForgeError.Generic",
                        "Crash.Suggestion.FollowLoaderInstructions",
                        help: true);

            case CrashCause.NoAnalyzableFile:
                return _Spec(
                    "Crash.Reason.NoAnalyzableFile",
                    "Crash.Suggestion.ExportReport",
                    help: true);

            default:
                return _Spec(
                    "Crash.Reason.UnknownFinding",
                    "Crash.Suggestion.ExportReport",
                    _Args(finding.Cause.ToString()),
                    help: true);
        }
    }

    private static CrashMessageSpec _FormatMissingDependency(List<string> additional)
    {
        if (additional.Count == 0)
            return _Spec(
                "Crash.Reason.MissingDependency.Generic",
                "Crash.Suggestion.FollowLoaderInstructions",
                help: true);

        var info = _BulletList(additional);

        return info.IsMatch(RegexPatterns.IncompatibleModLoaderErrorHint)
            ? _Spec(
                "Crash.Reason.ModLoaderIncompatible",
                "Crash.Suggestion.ModLoaderIncompatible",
                _Args(info),
                action: CrashSuggestedAction.OpenInstanceSettings)
            : _Spec(
                "Crash.Reason.MissingDependency.WithDetail",
                "Crash.Suggestion.FollowLoaderInstructions",
                _Args(info));
    }

    private static CrashMessageSpec _FormatIncompatibleMods(List<string> additional)
    {
        if (additional.Count != 1)
            return _Spec(
                "Crash.Reason.IncompatibleMods.Generic",
                "Crash.Suggestion.FollowLoaderInstructions",
                help: true);

        var info = additional[0];

        return info.IsMatch(RegexPatterns.IncompatibleModLoaderErrorHint)
            ? _Spec(
                "Crash.Reason.ModLoaderIncompatible",
                "Crash.Suggestion.ModLoaderIncompatible",
                _Args(info),
                action: CrashSuggestedAction.OpenInstanceSettings)
            : _Spec(
                "Crash.Reason.IncompatibleMods.WithDetail",
                "Crash.Suggestion.FollowLoaderInstructions",
                _Args(info));
    }

    private static CrashMessageSpec _Spec(
        string reasonKey,
        string? suggestionKey = null,
        object?[]? reasonArgs = null,
        object?[]? suggestionArgs = null,
        bool report = false,
        bool help = false,
        CrashSuggestedAction action = CrashSuggestedAction.None)
    {
        return new CrashMessageSpec(
            reasonKey,
            reasonArgs ?? [],
            suggestionKey,
            suggestionArgs ?? [],
            report,
            help,
            action);
    }

    private static object?[] _Args(params object?[] args)
    {
        return args;
    }

    private static string _BulletList(IEnumerable<string> items)
    {
        return string.Join(
            Lang.Text("Crash.Presentation.LineSeparator"),
            items.Select(item => Lang.Text("Crash.Presentation.ListItem", item)));
    }

    private static string _JoinParagraphs(IEnumerable<string> items)
    {
        return string.Join(
            Lang.Text("Crash.Presentation.ParagraphSeparator"),
            items.Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static string _NormalizeLineEndings(string text)
    {
        return text
            .Replace("\r\n", "\r")
            .Replace("\n", "\r")
            .Replace("\r", "\r\n");
    }

    private static string? _GetLauncherOutdatedSuggestion()
    {
        try
        {
            return UpdateManager.GetVersionStatus() == UpdateEnums.VersionStatus.Latest
                ? null
                : Lang.Text("Crash.Suggestion.LauncherOutdated");
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Crash", "确认启动器更新失败");
            return null;
        }
    }
}
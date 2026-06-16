using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;

namespace PCL;

internal sealed class CrashResultFormatter
{
    public string Format(
        CrashAnalysisContext context,
        bool isHandAnalyze)
    {
        var result = context.Result ?? new CrashAnalysisResult();

        if (!result.Any)
            return isHandAnalyze
                ? Lang.Text("Crash.Result.NoReason.Manual")
                : Lang.Text("Crash.Result.NoReason.Auto");

        var specs = result.Findings
            .Select(finding => _CreateMessageSpec(finding, context.LogAll))
            .ToList();

        var text = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}{Lang.Text("Crash.Result.Joiner")}",
            specs.Select(_Render));

        if (isHandAnalyze)
            return _NormalizeLineEndings(text).Trim('\r', '\n');

        if (specs.Any(spec => spec.AppendReportHint))
            text += Environment.NewLine + Lang.Text("Crash.Result.ReportHint");

        if (specs.Any(spec => spec.AppendHelpHint))
            text += Environment.NewLine + Lang.Text("Crash.Result.HelpHint") + _GetLauncherOutdatedHint();

        return _NormalizeLineEndings(text).Trim('\r', '\n');
    }

    private static string _Render(CrashMessageSpec spec)
    {
        return Lang.Text(spec.ResourceKey, spec.Args);
    }

    private static CrashMessageSpec _CreateMessageSpec(
        CrashFinding finding,
        string combinedLogText)
    {
        var additional = finding.Details.ToList();

        switch (finding.Cause)
        {
            case CrashCause.ExtractedModFile:
                return _Spec("Crash.Result.ExtractedModFile");

            case CrashCause.OutOfMemory:
                return _Spec("Crash.Result.OutOfMemory", help: true);

            case CrashCause.UsingOpenJ9:
                return _Spec("Crash.Result.UsingOpenJ9");

            case CrashCause.UsingJdk:
                return _Spec("Crash.Result.UsingJdk");

            case CrashCause.JavaTooNew:
                return _Spec("Crash.Result.JavaTooNew");

            case CrashCause.JavaIncompatible:
                return _Spec("Crash.Result.IncompatibleJavaVersion");

            case CrashCause.InvalidModFileName:
                return _Spec("Crash.Result.InvalidModFileName");

            case CrashCause.MissingMixinBootstrap:
                return _Spec("Crash.Result.MissingMixinBootstrap");

            case CrashCause.X86JavaMemoryLimit:
                return Environment.Is64BitOperatingSystem
                    ? _Spec("Crash.Result.X86JavaMemoryLimit.OnX64Os")
                    : _Spec("Crash.Result.X86JavaMemoryLimit.OnX86Os", help: true);

            case CrashCause.MissingDependencyOrWrongMcVersion:
                return _FormatMissingDependency(additional);

            case CrashCause.StackKeywordFound:
                return additional.Count == 1
                    ? _Spec("Crash.Result.StackKeyword.Single", _Args(additional[0]), help: true)
                    : _Spec("Crash.Result.StackKeyword.Multiple", _Args(string.Join(", ", additional)), help: true);

            case CrashCause.StackModNameFound:
            case CrashCause.SuspectedModCrash:
                return additional.Count == 1
                    ? _Spec("Crash.Result.SuspectedMod.Single", _Args(additional[0]), true, true)
                    : _Spec("Crash.Result.SuspectedMod.Multiple", _Args(_BulletList(additional)), true, true);

            case CrashCause.ConfirmedModCrash:
                return additional.Count == 1
                    ? _Spec("Crash.Result.ConfirmedMod.Single", _Args(additional[0]), true, true)
                    : _Spec("Crash.Result.ConfirmedMod.Multiple", _Args(_BulletList(additional)), true, true);

            case CrashCause.ModMixinFailed:
                if (additional.Count == 0)
                    return _Spec("Crash.Result.ModMixinFailed.None", report: true, help: true);

                return additional.Count == 1
                    ? _Spec("Crash.Result.ModMixinFailed.Single", _Args(additional[0]), true, true)
                    : _Spec("Crash.Result.ModMixinFailed.Multiple", _Args(_BulletList(additional)), true, true);

            case CrashCause.ModConfigCrash:
                return additional.Count > 1 && additional[1] is not null
                    ? _Spec("Crash.Result.ModConfigCrash.WithConfig", _Args(additional[0], additional[1]))
                    : _Spec(
                        "Crash.Result.ModConfigCrash.Simple",
                        _Args(additional.FirstOrDefault() ?? string.Empty),
                        true,
                        true);

            case CrashCause.ModInitializationFailed:
                return additional.Count == 1
                    ? _Spec("Crash.Result.ModInitializationFailed.Single", _Args(additional[0]), true, true)
                    : _Spec("Crash.Result.ModInitializationFailed.Multiple", _Args(_BulletList(additional)), true,
                        true);

            case CrashCause.SpecificBlockCrash:
                return additional.Count == 1
                    ? _Spec("Crash.Result.SpecificBlock.Single", _Args(additional[0]), help: true)
                    : _Spec("Crash.Result.SpecificBlock.Multiple", help: true);

            case CrashCause.DuplicateMods:
                return additional.Count >= 2
                    ? _Spec("Crash.Result.DuplicateMods.Known", _Args(_BulletList(additional)))
                    : _Spec("Crash.Result.DuplicateMods.Unknown", report: true, help: true);

            case CrashCause.SpecificEntityCrash:
                return additional.Count == 1
                    ? _Spec("Crash.Result.SpecificEntity.Single", _Args(additional[0]), help: true)
                    : _Spec("Crash.Result.SpecificEntity.Multiple", help: true);

            case CrashCause.OptiFineForgeIncompatible:
                return _Spec("Crash.Result.OptiFineForgeIncompatible");

            case CrashCause.ShadersModWithOptiFine:
                return _Spec("Crash.Result.ShadersModWithOptiFine");

            case CrashCause.OldForgeNewJavaIncompatible:
                return _Spec("Crash.Result.OldForgeNewJavaIncompatible");

            case CrashCause.MultipleForgeInInstanceJson:
                return _Spec("Crash.Result.MultipleForgeInInstanceJson");

            case CrashCause.ManualDebugCrash:
                return _Spec("Crash.Result.ManualDebugCrash");

            case CrashCause.ModRequiresJava11:
                return _Spec("Crash.Result.ModRequiresJava11");

            case CrashCause.VeryShortOutput:
                return _Spec(
                    "Crash.Result.VeryShortOutput",
                    _Args(additional.FirstOrDefault() ?? string.Empty),
                    help: true);

            case CrashCause.OptiFineWorldLoadCrash:
                return _Spec("Crash.Result.OptiFineWorldLoadCrash", help: true);

            case CrashCause.PixelFormatNotSupported:
            case CrashCause.IntelDriverAccessViolation:
            case CrashCause.AmdDriverAccessViolation:
            case CrashCause.NvidiaDriverAccessViolation:
            case CrashCause.UnsupportedOpenGl:
                return combinedLogText.Contains("hd graphics ")
                    ? _Spec("Crash.Result.GraphicsDriver.IntelOrIntegrated", help: true)
                    : _Spec("Crash.Result.GraphicsDriver.Generic", help: true);

            case CrashCause.ResourcePackTooLarge:
                return _Spec("Crash.Result.ResourcePackTooLargeOrGpuInsufficient", help: true);

            case CrashCause.NightConfigBug:
                return _Spec("Crash.Result.NightConfigBug", help: true);

            case CrashCause.OpenGl1282:
                return _Spec("Crash.Result.OpenGl1282FromShaderOrResourcePack", help: true);

            case CrashCause.TooManyModsIdLimit:
                return _Spec("Crash.Result.TooManyModsIdLimit");

            case CrashCause.FileOrContentValidationFailed:
                return _Spec("Crash.Result.FileOrContentValidationFailed", help: true);

            case CrashCause.IncompleteForgeInstallation:
                return _Spec("Crash.Result.IncompleteForgeInstallation", help: true);

            case CrashCause.FabricError:
                return additional.Count == 1
                    ? _Spec("Crash.Result.FabricError.WithInfo", _Args(additional[0]))
                    : _Spec("Crash.Result.FabricError.Generic", help: true);

            case CrashCause.IncompatibleMods:
                return _FormatIncompatibleMods(additional);

            case CrashCause.ModLoaderError:
                return additional.Count == 1
                    ? _Spec("Crash.Result.ModLoaderError.WithInfo", _Args(additional[0]))
                    : _Spec("Crash.Result.ModLoaderError.Generic", help: true);

            case CrashCause.FabricSolutionProvided:
                return additional.Count == 1
                    ? _Spec("Crash.Result.FabricSolutionProvided.WithInfo", _Args(additional[0]))
                    : _Spec("Crash.Result.FabricSolutionProvided.Generic", help: true);

            case CrashCause.ForgeError:
                return additional.Count == 1
                    ? _Spec("Crash.Result.ForgeError.WithInfo", _Args(additional[0]))
                    : _Spec("Crash.Result.ForgeError.Generic", help: true);

            case CrashCause.NoAnalyzableFile:
                return _Spec("Crash.Result.NoAnalyzableFile", help: true);

            default:
                return _Spec("Crash.Result.UnknownReason", _Args(finding.Cause.ToString()), help: true);
        }
    }

    private static CrashMessageSpec _FormatMissingDependency(List<string> additional)
    {
        if (additional.Count == 0)
            return _Spec("Crash.Result.MissingDependencyOrWrongMcVersion.Generic", help: true);

        var info = string.Join("\n - ", additional);

        return info.IsMatch(RegexPatterns.IncompatibleModLoaderErrorHint)
            ? _Spec("Crash.Result.ModLoaderIncompatible", _Args(info))
            : _Spec("Crash.Result.MissingDependencyOrWrongMcVersion.WithInfo", _Args(info));
    }

    private static CrashMessageSpec _FormatIncompatibleMods(List<string> additional)
    {
        if (additional.Count != 1)
            return _Spec("Crash.Result.IncompatibleMods.Generic", help: true);

        var info = additional[0];

        return info.IsMatch(RegexPatterns.IncompatibleModLoaderErrorHint)
            ? _Spec("Crash.Result.ModLoaderIncompatible", _Args(info))
            : _Spec("Crash.Result.IncompatibleMods.WithInfo", _Args(info));
    }

    private static CrashMessageSpec _Spec(
        string key,
        object?[]? args = null,
        bool report = false,
        bool help = false)
    {
        return new CrashMessageSpec(
            key,
            args ?? [],
            report,
            help);
    }

    private static object?[] _Args(params object?[] args)
    {
        return args;
    }

    private static string _BulletList(IEnumerable<string> items)
    {
        return string.Join(
            "\n",
            items.Select(item => Lang.Text("Crash.Result.List.BulletItem", item)));
    }

    private static string _NormalizeLineEndings(string text)
    {
        return text
            .Replace("\r\n", "\r")
            .Replace("\n", "\r")
            .Replace("\r", "\r\n");
    }

    private static string _GetLauncherOutdatedHint()
    {
        try
        {
            if (UpdateManager.GetVersionStatus() == UpdateEnums.VersionStatus.Latest)
                return string.Empty;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Crash", "确认启动器更新失败");
        }

        return Environment.NewLine +
               Environment.NewLine +
               Lang.Text("Crash.Result.LauncherOutdatedHint");
    }
}
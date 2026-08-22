namespace PCL;

internal static class CrashRuleCatalog
{
    public static IReadOnlyList<CrashRule> Rules { get; } =
    [
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.CrashReport,
            "Unable to make protected final java.lang.Class java.lang.ClassLoader.defineClass",
            CrashCause.JavaTooNew),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            "Found multiple arguments for option fml.forgeVersion, but you asked for only one",
            CrashCause.MultipleForgeInInstanceJson),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            "The driver does not appear to support OpenGL",
            CrashCause.UnsupportedOpenGl),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            "java.lang.ClassCastException: java.base/jdk",
            CrashCause.UsingJdk),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            "java.lang.ClassCastException: class jdk.",
            CrashCause.UsingJdk),
        _ContainsAny(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            [
                "TRANSFORMER/net.optifine/net.optifine.reflect.Reflector.<clinit>(Reflector.java",
                "java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.texture.SpriteContents.<init>",
                "java.lang.NoSuchMethodError: 'java.lang.String com.mojang.blaze3d.systems.RenderSystem.getBackendDescription",
                "java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.block.model.BakedQuad.<init>",
                "java.lang.NoSuchMethodError: 'void net.minecraftforge.client.gui.overlay.ForgeGui.renderSelectedItemName",
                "java.lang.NoSuchMethodError: 'void net.minecraft.server.level.DistanceManager",
                "java.lang.NoSuchMethodError: 'net.minecraft.network.chat.FormattedText net.minecraft.client.gui.Font.ellipsize"
            ],
            CrashCause.OptiFineForgeIncompatible),
        _ContainsAny(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            [
                "Open J9 is not supported",
                "OpenJ9 is incompatible",
                ".J9VMInternals."
            ],
            CrashCause.UsingOpenJ9),
        _ContainsAny(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            [
                "java.lang.NoSuchFieldException: ucp",
                "because module java.base does not export",
                "java.lang.ClassNotFoundException: jdk.nashorn.api.scripting.NashornScriptEngineFactory",
                "java.lang.ClassNotFoundException: java.lang.invoke.LambdaMetafactory"
            ],
            CrashCause.JavaTooNew),
        _ContainsAny(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            [
                "The directories below appear to be extracted jar files. Fix this before you continue.",
                "Extracted mod jars found, loading will NOT continue"
            ],
            CrashCause.ExtractedModFile),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            "java.lang.ClassNotFoundException: org.spongepowered.asm.launch.MixinTweaker",
            CrashCause.MissingMixinBootstrap),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            "Couldn't set pixel format",
            CrashCause.PixelFormatNotSupported),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            "java.lang.RuntimeException: Shaders Mod detected. Please remove it, OptiFine has built-in support for shaders.",
            CrashCause.ShadersModWithOptiFine),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            "java.lang.NoSuchMethodError: sun.security.util.ManifestEntryVerifier",
            CrashCause.OldForgeNewJavaIncompatible),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            "1282: Invalid operation",
            CrashCause.OpenGl1282),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            "Maybe try a lower resolution resourcepack?",
            CrashCause.ResourcePackTooLarge),
        _ContainsAll(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            [
                "java.lang.NoSuchMethodError: net.minecraft.world.server.ChunkManager$ProxyTicketManager.shouldForceTicks(J)Z",
                "OptiFine"
            ],
            CrashCause.OptiFineWorldLoadCrash),
        _ContainsAny(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            ["Unsupported class file major version", "Unsupported major.minor version"], CrashCause.JavaIncompatible),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            "com.electronwill.nightconfig.core.io.ParsingException: Not enough data available",
            CrashCause.NightConfigBug),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            "Cannot find launch target fmlclient, unable to launch",
            CrashCause.IncompleteForgeInstallation),
        _ContainsAll(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            [
                "Invalid paths argument, contained no existing paths",
                @"libraries\net\minecraftforge\fmlcore"
            ],
            CrashCause.IncompleteForgeInstallation),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            "Invalid module name: '' is not a Java identifier",
            CrashCause.InvalidModFileName),
        _ContainsAny(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            [
                "has been compiled by a more recent version of the Java Runtime (class file version 55.0), this version of the Java Runtime only recognizes class file versions up to",
                "java.lang.RuntimeException: java.lang.NoSuchMethodException: no such method: sun.misc.Unsafe.defineAnonymousClass(Class,byte[],Object[])Class/invokeVirtual",
                "java.lang.IllegalArgumentException: The requested compatibility level JAVA_11 could not be set. Level is not supported by the active JRE or ASM version"
            ],
            CrashCause.ModRequiresJava11),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.Game,
            "Invalid maximum heap size",
            CrashCause.X86JavaMemoryLimit),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.CrashReport,
            "maximum id range exceeded",
            CrashCause.TooManyModsIdLimit),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.CrashReport,
            "Pixel format not accelerated",
            CrashCause.PixelFormatNotSupported),
        _Contains(
            DetectionPhase.Fatal,
            CrashLogKind.CrashReport,
            "Manually triggered debug crash",
            CrashCause.ManualDebugCrash)
    ];

    private static CrashRule _Contains(
        DetectionPhase phase,
        CrashLogKind source,
        string pattern,
        CrashCause cause,
        bool stopOnMatch = true)
    {
        return new CrashRule
        {
            Phase = phase,
            Cause = cause,
            StopOnMatch = stopOnMatch,
            Evaluate = input =>
            {
                var text = input.Logs.GetText(source);
                return text?.Contains(pattern, StringComparison.Ordinal) != true
                    ? null
                    : _CreateFinding(cause, CrashConfidence.High, source, pattern, stopOnMatch);
            }
        };
    }

    private static CrashRule _ContainsAny(
        DetectionPhase phase,
        CrashLogKind source,
        IReadOnlyList<string> patterns,
        CrashCause cause,
        bool stopOnMatch = true)
    {
        return new CrashRule
        {
            Phase = phase,
            Cause = cause,
            StopOnMatch = stopOnMatch,
            Evaluate = input =>
            {
                var text = input.Logs.GetText(source);
                if (string.IsNullOrEmpty(text))
                    return null;

                var pattern = patterns.FirstOrDefault(item => text.Contains(item, StringComparison.Ordinal));
                return pattern is null
                    ? null
                    : _CreateFinding(cause, CrashConfidence.High, source, pattern, stopOnMatch);
            }
        };
    }

    private static CrashRule _ContainsAll(
        DetectionPhase phase,
        CrashLogKind source,
        IReadOnlyList<string> patterns,
        CrashCause cause,
        bool stopOnMatch = true)
    {
        return new CrashRule
        {
            Phase = phase,
            Cause = cause,
            StopOnMatch = stopOnMatch,
            Evaluate = input =>
            {
                var text = input.Logs.GetText(source);
                if (string.IsNullOrEmpty(text) ||
                    patterns.Any(pattern => !text.Contains(pattern, StringComparison.Ordinal)))
                    return null;

                return _CreateFinding(
                    cause,
                    CrashConfidence.High,
                    source,
                    string.Join(" | ", patterns),
                    stopOnMatch);
            }
        };
    }

    private static CrashFinding _CreateFinding(
        CrashCause cause,
        CrashConfidence confidence,
        CrashLogKind source,
        string pattern,
        bool stopOnMatch)
    {
        return new CrashFinding(
            cause,
            confidence,
            [
                new CrashEvidence
                {
                    Source = source,
                    Pattern = pattern,
                    Value = pattern,
                    DisplayKind = "pattern"
                }
            ])
        {
            ShouldStop = stopOnMatch
        };
    }
}
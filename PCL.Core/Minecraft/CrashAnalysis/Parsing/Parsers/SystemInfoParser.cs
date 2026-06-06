namespace PCL.Core.Minecraft.CrashAnalysis;

internal sealed class SystemInfoParser : ICrashLogParser
{
    private static readonly string[] _SensitiveLaunchArgumentKeywords =
    [
        "accessToken",
        "uuid",
        "clientId",
        "xuid",
        "username"
    ];

    public IReadOnlyList<CrashFact> Parse(CrashLogBundle bundle, CrashAnalysisRequest request)
    {
        var facts = new List<CrashFact>();
        _AppendSystemFacts(facts, request);
        _AppendGpuFacts(facts, request);
        _AppendLaunchArgumentFacts(facts, request);

        return facts;
    }

    private static void _AppendSystemFacts(List<CrashFact> facts, CrashAnalysisRequest request)
    {
        var context = request.RuntimeContext;

        _AddContextFactIfPresent(facts, CrashFactKind.OsVersionDetected, context.OperatingSystem);
        _AddContextFactIfPresent(facts, CrashFactKind.MemoryAllocationDetected, context.AllocatedMemory);

        if (context.Is32BitSystem == true)
            facts.Add(CrashFactFactory.CreateFromContext(
                CrashFactKind.ProcessBitnessDetected,
                "x86"));
    }

    private static void _AppendGpuFacts(List<CrashFact> facts, CrashAnalysisRequest request)
    {
        facts.AddRange(
            from gpu in request.RuntimeContext.Gpus
            where !string.IsNullOrWhiteSpace(gpu.Name)
            select CrashFactFactory.CreateFromContext(CrashFactKind.GpuVendorDetected, gpu.Name));
    }

    private static void _AppendLaunchArgumentFacts(List<CrashFact> facts, CrashAnalysisRequest request)
    {
        facts.AddRange(request.RuntimeContext.LaunchArguments.Select(argument =>
            CrashFactFactory.CreateFromContext(
                CrashFactKind.LaunchArgumentDetected,
                argument,
                visibility: _LaunchArgumentVisibility(argument))));
    }

    private static void _AddContextFactIfPresent(
        List<CrashFact> facts,
        CrashFactKind kind,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        facts.Add(CrashFactFactory.CreateFromContext(kind, value));
    }

    private static CrashFactVisibility _LaunchArgumentVisibility(string argument)
    {
        return _SensitiveLaunchArgumentKeywords.Any(keyword =>
            argument.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            ? CrashFactVisibility.Hidden
            : CrashFactVisibility.Technical;
    }
}
namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashDiagnosisNote
{
    public required string Key { get; init; }
    public CrashDiagnosisNoteLevel Level { get; init; } = CrashDiagnosisNoteLevel.Info;
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
}

public enum CrashDiagnosisNoteLevel
{
    Info,
    Warning
}
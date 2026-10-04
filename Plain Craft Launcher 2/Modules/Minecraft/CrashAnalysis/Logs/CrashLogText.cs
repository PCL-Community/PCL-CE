namespace PCL;

internal sealed class CrashLogText
{
    public required CrashLogKind Kind { get; init; }

    public required string Text { get; init; }

    public string? FilePath { get; init; }
}
namespace PCL;

internal sealed class CrashEvidence
{
    public required string Value { get; init; }

    public CrashLogKind? Source { get; init; }

    public string? Pattern { get; init; }

    public string? DisplayKind { get; init; }

    public bool EqualsTo(CrashEvidence other)
    {
        return Source == other.Source &&
               string.Equals(Value, other.Value, StringComparison.Ordinal) &&
               string.Equals(DisplayKind, other.DisplayKind, StringComparison.Ordinal);
    }
}
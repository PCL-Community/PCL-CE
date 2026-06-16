namespace PCL;

internal sealed class CrashMessageSpec(
    string resourceKey,
    object?[] args,
    bool appendReportHint,
    bool appendHelpHint)
{
    public string ResourceKey { get; } = resourceKey;

    public object?[] Args { get; } = args;

    public bool AppendReportHint { get; } = appendReportHint;

    public bool AppendHelpHint { get; } = appendHelpHint;
}
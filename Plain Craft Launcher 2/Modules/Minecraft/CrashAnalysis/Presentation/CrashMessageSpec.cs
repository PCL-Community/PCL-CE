namespace PCL;

internal enum CrashSuggestedAction
{
    None,
    OpenInstanceSettings
}

internal sealed class CrashMessageSpec(
    string reasonKey,
    object?[] reasonArgs,
    string? suggestionKey,
    object?[] suggestionArgs,
    bool appendReportHint,
    bool appendHelpHint,
    CrashSuggestedAction suggestedAction = CrashSuggestedAction.None)
{
    public string ReasonKey { get; } = reasonKey;

    public object?[] ReasonArgs { get; } = reasonArgs;

    public string? SuggestionKey { get; } = suggestionKey;

    public object?[] SuggestionArgs { get; } = suggestionArgs;

    public bool AppendReportHint { get; } = appendReportHint;

    public bool AppendHelpHint { get; } = appendHelpHint;

    public CrashSuggestedAction SuggestedAction { get; } = suggestedAction;
}

internal sealed class CrashDialogContent(string text, CrashSuggestedAction suggestedAction)
{
    public string Text { get; } = text;

    public CrashSuggestedAction SuggestedAction { get; } = suggestedAction;
}

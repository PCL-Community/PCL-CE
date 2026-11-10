namespace PCL;

public static class UpdateEnums
{
    public enum UpdateType
    {
        Silent = 0,
        PromptOnly = 1,
        DownloadAndPrompt = 2,
        UpdateNow = 3
    }

    public enum VersionStatus
    {
        Latest,
        NotLatest,
        Unknown
    }
}
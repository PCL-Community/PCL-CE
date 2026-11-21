namespace PCL.Core.Platform;

public enum RecycleBinOutcome
{
    Moved,
    Unsupported,
    Failed
}

public interface IRecycleBinService
{
    bool IsSupported(string? path);
    RecycleBinOutcome Move(string? path, out string? error);
}
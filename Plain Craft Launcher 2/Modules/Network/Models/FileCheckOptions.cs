namespace PCL.Network;

/// <summary>
///     文件下载后的校验规则。
/// </summary>
public sealed class FileCheckOptions(
    long minSize = -1,
    long actualSize = -1,
    string? hash = null,
    bool canUseExistingFile = true,
    bool validateJson = false)
{
    public long MinSize { get; } = minSize;
    public long ActualSize { get; } = actualSize;
    public string? Hash { get; } = hash;
    public bool CanUseExistingFile { get; } = canUseExistingFile;
    public bool ValidateJson { get; } = validateJson;

    public string? Check(string localPath)
    {
        return Files.CheckAsync(localPath, MinSize, ActualSize, Hash, ValidateJson).GetAwaiter().GetResult();
    }

    public Task<string?> CheckAsync(string localPath)
    {
        return Files.CheckAsync(localPath, MinSize, ActualSize, Hash, ValidateJson);
    }
}
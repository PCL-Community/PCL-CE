using PCL.Core.Utils;

namespace PCL;

/// <summary>
///     文件的校验规则。
/// </summary>
public class FileChecker(
    long minSize = -1,
    long actualSize = -1,
    string? hash = null,
    bool canUseExistsFile = true,
    bool isJson = false)
{
    /// <summary>文件的准确大小。不检查则为 -1。</summary>
    public readonly long actualSize = actualSize;

    /// <summary>是否可以使用已经存在的文件。</summary>
    public readonly bool canUseExistsFile = canUseExistsFile;

    /// <summary>文件的 MD5、SHA1 或 SHA256。会根据输入字符串的长度自动判断种类。</summary>
    public readonly string? hash = hash;

    /// <summary>是否要求为 JSON 文件。</summary>
    public readonly bool isJson = isJson;

    /// <summary>文件的最小大小。不检查则为 -1。</summary>
    public readonly long minSize = minSize;

    /// <summary>
    ///     检查文件。若成功则返回 <c>null</c>，失败则返回错误描述。不会抛出错误。
    /// </summary>
    public string? Check(string localPath)
    {
        try
        {
            LauncherCommon.Log($"[Checker] 开始校验文件 {localPath}", LauncherLogLevel.Developer);
            var info = new FileInfo(localPath);
            if (!info.Exists)
                return "文件不存在：" + localPath;
            var fileSize = info.Length;
            var errorMessage = new List<string>();
            var allowIgnore = false;
            if (!string.IsNullOrEmpty(hash))
            {
                switch (hash.Length)
                {
                    case < 35:
                    {
                        var computedHash = LauncherCommon.GetFileMd5(localPath);
                        if ((hash.ToLowerInvariant() ?? "") != (computedHash ?? ""))
                            errorMessage.Add($"文件 MD5 应为 {hash}，实际为 {computedHash}");
                        break;
                    }
                    case 64:
                    {
                        var computedHash = LauncherCommon.GetFileSha256(localPath);
                        if ((hash.ToLowerInvariant() ?? "") != (computedHash ?? ""))
                            errorMessage.Add($"文件 SHA256 应为 {hash}，实际为 {computedHash}");
                        break;
                    }
                    default:
                    {
                        var computedHash = LauncherCommon.GetFileSha1(localPath);
                        if ((hash.ToLowerInvariant() ?? "") != (computedHash ?? ""))
                            errorMessage.Add($"文件 SHA1 应为 {hash}，实际为 {computedHash}");
                        break;
                    }
                }

                allowIgnore = errorMessage.Count == 0;
            }

            var sizeMismatch = actualSize >= 0L && actualSize != fileSize && !allowIgnore;
            var sizeTooSmall = minSize >= 0L && fileSize < minSize;

            if (sizeMismatch || sizeTooSmall)
            {
                var contentSuffix = fileSize < 2000L
                    ? $"，内容为：{LauncherCommon.ReadFile(localPath)}"
                    : string.Empty;

                if (sizeMismatch)
                    errorMessage.Add(
                        $"文件大小应为 {actualSize} B，实际为 {fileSize} B{contentSuffix}");

                if (sizeTooSmall)
                    errorMessage.Add(
                        $"文件大小应不小于 {minSize} B，实际为 {fileSize} B{contentSuffix}");
            }

            if (isJson)
            {
                var content = LauncherCommon.ReadFile(localPath);
                if (string.IsNullOrEmpty(content))
                    throw new Exception("读取到的文件为空");
                try
                {
                    LauncherCommon.GetJson(content);
                }
                catch (Exception ex)
                {
                    throw new InvalidJsonException(ex);
                }
            }

            if (errorMessage.Count == 0) return null;
            errorMessage.Insert(0, $"实际校验地址：{localPath}");
            return string.Join(";", errorMessage);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "检查文件出错");
            return ex.ToString();
        }
    }
}
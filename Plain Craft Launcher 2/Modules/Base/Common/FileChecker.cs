using System.IO;
using PCL.Core.App.Localization;

namespace PCL;

public static partial class Common
{
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
        /// <summary>
        ///     文件的准确大小。
        ///     不检查则为 -1。
        /// </summary>
        public long actualSize = actualSize;

        /// <summary>
        ///     是否可以使用已经存在的文件。
        /// </summary>
        public bool canUseExistsFile = canUseExistsFile;

        /// <summary>
        ///     文件的 MD5、SHA1 或 SHA256。会根据输入字符串的长度自动判断种类。
        ///     不检查则为 Nothing。
        /// </summary>
        public string? hash = hash;

        /// <summary>
        ///     是否要求为 JSON 文件。
        ///     即，开头结尾必须为 {} 或 []。
        /// </summary>
        public bool isJson = isJson;

        /// <summary>
        ///     文件的最小大小。
        ///     不检查则为 -1。
        /// </summary>
        public long minSize = minSize;

        /// <summary>
        ///     检查文件。若成功则返回 Nothing，失败则返回错误的描述文本，描述文本不以句号结尾。不会抛出错误。
        /// </summary>
        public string? Check(string localPath)
        {
            try
            {
                Log($"[Checker] 开始校验文件 {localPath}", LogLevel.Developer);
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
                            var computedHash = GetFileMd5(localPath);
                            if ((hash.ToLowerInvariant() ?? "") != (computedHash ?? ""))
                                errorMessage.Add($"文件 MD5 应为 {hash}，实际为 {computedHash}");
                            break;
                        }
                        case 64:
                        {
                            var computedHash = GetFileSha256(localPath);
                            if ((hash.ToLowerInvariant() ?? "") != (computedHash ?? ""))
                                errorMessage.Add($"文件 SHA256 应为 {hash}，实际为 {computedHash}");
                            break;
                        }
                        default:
                        {
                            var computedHash = GetFileSha1(localPath);
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
                        ? $"，内容为：{ReadFile(localPath)}"
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
                    var content = ReadFile(localPath);
                    if (string.IsNullOrEmpty(content))
                        throw new Exception("读取到的文件为空");
                    try
                    {
                        GetJson(content);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(Lang.Text("Common.Error.InvalidJson"), ex);
                    }
                }

                if (errorMessage.Count == 0) return null;
                errorMessage.Insert(0, $"实际校验地址：{localPath}");
                return errorMessage.Join(";");
            }
            catch (Exception ex)
            {
                Log(ex, "检查文件出错");
                return ex.ToString();
            }
        }
    }
}
using System.IO;
using System.Text;

namespace PCL;

public static partial class ModBase
{
    #region LegacyFiles

    // 路径处理
    /// <summary>
    ///     从文件路径或者 Url 获取不包含文件名的路径，或获取文件夹的父文件夹路径。
    ///     取决于原路径格式，路径以 / 或 \ 结尾。
    ///     不包含路径将会抛出异常。
    /// </summary>
    public static string GetPathFromFullPath(string filePath)
    {
        return LegacyFileFacade.GetPathFromFullPath(filePath);
    }

    /// <summary>
    ///     从文件路径或者 Url 获取不包含路径的文件名。不包含文件名将会抛出异常。
    /// </summary>
    public static string GetFileNameFromPath(string filePath)
    {
        return LegacyFileFacade.GetFileNameFromPath(filePath);
    }

    /// <summary>
    ///     从文件路径或者 Url 获取不包含路径与扩展名的文件名。不包含文件名将会抛出异常。
    /// </summary>
    public static string GetFileNameWithoutExtentionFromPath(string filePath)
    {
        return LegacyFileFacade.GetFileNameWithoutExtensionFromPath(filePath);
    }

    /// <summary>
    ///     从文件夹路径获取文件夹名。
    /// </summary>
    public static string GetFolderNameFromPath(string folderPath)
    {
        return LegacyFileFacade.GetFolderNameFromPath(folderPath);
    }

    // 读取、写入、复制文件
    /// <summary>
    ///     复制文件。会自动创建文件夹、会覆盖已有的文件。
    /// </summary>
    public static void CopyFile(string fromPath, string toPath)
    {
        LegacyFileFacade.CopyFile(fromPath, toPath);
    }

    /// <summary>
    ///     读取文件，如果失败则返回空数组。
    /// </summary>
    public static byte[] ReadFileBytes(string filePath, Encoding encoding = null)
    {
        return LegacyFileFacade.ReadBytes(filePath, encoding);
    }

    /// <summary>
    ///     读取文件，如果失败则返回空字符串。
    /// </summary>
    public static string ReadFile(string filePath, Encoding encoding = null)
    {
        return LegacyFileFacade.ReadText(filePath, encoding);
    }

    /// <summary>
    ///     读取流中的所有文本。
    /// </summary>
    public static string ReadFile(Stream stream, Encoding encoding = null)
    {
        return LegacyFileFacade.ReadText(stream, encoding);
    }

    /// <summary>
    ///     写入文件。
    /// </summary>
    public static void WriteFile(string filePath, string text, bool append = false, Encoding? encoding = null)
    {
        LegacyFileFacade.WriteText(filePath, text, append, encoding);
    }

    /// <summary>
    ///     写入文件。
    ///     如果 CanThrow 设置为 False，返回是否写入成功。
    /// </summary>
    public static void WriteFile(string filePath, byte[] content, bool append = false)
    {
        LegacyFileFacade.WriteBytes(filePath, content, append);
    }

    /// <summary>
    ///     将流写入文件。
    /// </summary>
    public static bool WriteFile(string filePath, Stream stream)
    {
        return LegacyFileFacade.WriteStream(filePath, stream);
    }

    /// <summary>
    ///     解码 Bytes。
    /// </summary>
    public static string DecodeBytes(byte[] bytes)
    {
        return LegacyFileFacade.DecodeBytes(bytes);
    }

    public static object GetHexString(Memory<byte> bytes)
    {
        return LegacyFileFacade.GetHexString(bytes);
    }

    // 文件校验
    /// <summary>
    ///     获取文件 MD5，若失败则返回空字符串。
    /// </summary>
    public static string GetFileMD5(string filePath)
    {
        return LegacyFileFacade.GetFileMd5(filePath);
    }

    /// <summary>
    ///     获取文件 SHA512，若失败则返回空字符串。
    /// </summary>
    public static string GetFileSHA512(string filePath)
    {
        return LegacyFileFacade.GetFileSha512(filePath);
    }

    /// <summary>
    ///     获取文件 SHA256，若失败则返回空字符串。
    /// </summary>
    public static string GetFileSHA256(string filePath)
    {
        return LegacyFileFacade.GetFileSha256(filePath);
    }

    /// <summary>
    ///     获取文件 SHA1，若失败则返回空字符串。
    /// </summary>
    public static string GetFileSHA1(string filePath)
    {
        return LegacyFileFacade.GetFileSha1(filePath);
    }

    /// <summary>
    ///     获取流的 SHA1，若失败则返回空字符串。
    /// </summary>
    public static string GetAuthSHA1(Stream inputStream)
    {
        return LegacyFileFacade.GetStreamSha1(inputStream);
    }

    /// <summary>
    ///     文件的校验规则。
    /// </summary>
    public class FileChecker
    {
        public long actualSize = -1;
        public bool canUseExistsFile = true;
        public string hash;
        public bool isJson;
        public long minSize = -1;

        public FileChecker(long minSize = -1, long actualSize = -1, string hash = null,
            bool canUseExistsFile = true, bool isJson = false)
        {
            this.actualSize = actualSize;
            this.minSize = minSize;
            this.hash = hash;
            this.canUseExistsFile = canUseExistsFile;
            this.isJson = isJson;
        }

        /// <summary>
        ///     检查文件。若成功则返回 Nothing，失败则返回错误的描述文本，描述文本不以句号结尾。不会抛出错误。
        /// </summary>
        public string Check(string localPath)
        {
            return LegacyFileFacade.CheckFile(localPath, minSize, actualSize, hash, isJson);
        }
    }

    /// <summary>
    ///     等待文件就绪可读，在指定超时时间内轮询检查文件是否存在且内容非空。
    /// </summary>
    public static void WaitForFileReady(string filePath, int timeoutMs = 2000)
    {
        LegacyFileFacade.WaitForFileReady(filePath, timeoutMs);
    }

    /// <summary>
    ///     等待文件就绪可读，在指定超时时间内轮询检查文件是否存在且内容非空。
    /// </summary>
    public static void WaitForFileReady(string filePath, int timeoutMs, bool requireJson)
    {
        LegacyFileFacade.WaitForFileReady(filePath, timeoutMs, requireJson);
    }

    /// <summary>
    ///     尝试根据后缀名判断文件种类并解压文件，支持 gz 与 zip，会尝试将 Jar 以 zip 方式解压。
    ///     会尝试创建，但不会清空目标文件夹。
    /// </summary>
    public static void ExtractFile(string compressFilePath, string destDirectory, Encoding encode = null,
        Action<double> progressIncrementHandler = null)
    {
        LegacyFileFacade.ExtractFile(compressFilePath, destDirectory, encode, progressIncrementHandler);
    }

    /// <summary>
    ///     删除文件夹，返回删除的文件个数。通过参数选择是否抛出异常。
    /// </summary>
    public static int DeleteDirectory(string path, bool ignoreIssue = false)
    {
        return LegacyFileFacade.DeleteDirectory(path, ignoreIssue);
    }

    /// <summary>
    ///     复制文件夹，失败会抛出异常。
    /// </summary>
    public static void CopyDirectory(string fromPath, string toPath, Action<double> progressIncrementHandler = null)
    {
        LegacyFileFacade.CopyDirectory(fromPath, toPath, progressIncrementHandler);
    }

    /// <summary>
    ///     遍历文件夹中的所有文件。
    /// </summary>
    public static IEnumerable<FileInfo> EnumerateFiles(string directory)
    {
        return LegacyFileFacade.EnumerateFiles(directory);
    }

    /// <summary>
    ///     若路径长度大于指定值，则将长路径转换为短路径。
    /// </summary>
    public static string ShortenPath(string longPath, int shortenThreshold = 247)
    {
        return LegacyFileFacade.ShortenPath(longPath, shortenThreshold);
    }

    public static void MoveDirectory(string sourceDir, string targetDir)
    {
        LegacyFileFacade.MoveDirectory(sourceDir, targetDir);
    }

    public static void CreateSymbolicLink(string linkPath, string targetPath, int flags)
    {
        LegacyFileFacade.CreateSymbolicLink(linkPath, targetPath, flags);
    }

    /// <summary>
    ///     检查是否拥有某一文件夹的 I/O 权限。如果文件夹不存在，会返回 False。
    /// </summary>
    public static bool CheckPermission(string path)
    {
        return LegacyFileFacade.CheckPermission(path);
    }

    /// <summary>
    ///     检查是否拥有某一文件夹的 I/O 权限。如果出错，则抛出异常。
    /// </summary>
    public static void CheckPermissionWithException(string path)
    {
        LegacyFileFacade.CheckPermissionWithException(path);
    }

    #endregion
}
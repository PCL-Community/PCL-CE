using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xaml;
using System.Xml.Linq;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Microsoft.Win32;
using Newtonsoft.Json;
using PCL.Core.App;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Utils;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Hash;
using PCL.Core.Utils.OS;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;
using Size = System.Windows.Size;

namespace PCL
{
    public static partial class ModBase
    {
        #region 文件

        // 龙猫味石山小记: 用最不靠谱的实现写出能跑的代码 (AppDomain.CurrentDomain.SetupInformation.ApplicationBase 获取到的是当前工作目录而不是可执行文件所在目录)
        /// <summary>
        ///     程序可执行文件所在目录，以“\”结尾。
        /// </summary>
        public static readonly string ExePath = Conversions.ToString(Basics.ExecutableDirectory.EndsWith(@"\")
            ? Basics.ExecutableDirectory
            : Basics.ExecutableDirectory + @"\");

        /// <summary>
        ///     程序可执行文件完整路径。
        /// </summary>
        public static readonly string ExePathWithName = Basics.ExecutablePath;
        /// <summary>
        ///     系统盘盘符，以 \ 结尾。例如 "C:\"。
        /// </summary>
        public static string OsDrive =
            Environment.GetLogicalDrives().Where(p => Directory.Exists(p)).First().ToUpper().First() + @":\"; // #3799

        /// <summary>
        ///     程序的缓存文件夹路径，以 \ 结尾。
        /// </summary>
        public static string PathTemp = Paths.Temp + @"\";

        /// <summary>
        ///     AppData 中的 PCL 文件夹路径，以 \ 结尾。
        /// </summary>
        public static string PathAppdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\PCL\";

        /// <summary>
        ///     AppData 中的 PCLCE 配置文件夹路径，以 \ 结尾。
        /// </summary>
        public static string PathAppdataConfig = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                                                 (VersionBranchName == "Debug" ? @"\.pclcedebug\" : @"\.pclce\");

        public static string PathHelpFolder = PathTemp + @"CE\Help\";
        /// <summary>
        ///     读取注册表，默认为程序所属。
        /// </summary>
        public static string ReadReg(string Key, string DefaultValue = "", string Path = "")
        {
            string ReadRegRet = default;
            try
            {
                RegistryKey parentKey;
                RegistryKey softKey;
                parentKey = Registry.CurrentUser;
                softKey = parentKey.OpenSubKey(@"Software\" + (string.IsNullOrEmpty(Path) ? ModSecret.RegFolder : Path),
                    true);
                if (softKey is null)
                {
                    ReadRegRet = DefaultValue; // 不存在则返回默认值
                }
                else
                {
                    var readValue = new StringBuilder();
                    readValue.AppendLine(softKey.GetValue(Key).ToString());
                    var value = readValue.ToString().Replace("\r\n", ""); // 去除莫名的回车
                    return string.IsNullOrEmpty(value) ? DefaultValue : value;
                } // 错误则返回默认值
            }
            catch (Exception ex)
            {
                Log(ex, "读取注册表出错：" + Key, LogLevel.Hint);
                return DefaultValue;
            }

            return ReadRegRet;
        }

        /// <summary>
        ///     写入注册表，默认为程序所属。
        /// </summary>
        public static void WriteReg(string Key, string Value, bool ShowException = false, string Path = "",
            bool ThrowException = false)
        {
            try
            {
                RegistryKey parentKey;
                RegistryKey softKey;
                parentKey = Registry.CurrentUser;
                softKey = parentKey.OpenSubKey(@"Software\" + (string.IsNullOrEmpty(Path) ? ModSecret.RegFolder : Path),
                    true);
                if (softKey is null)
                    softKey = parentKey.CreateSubKey(@"Software\" +
                                                     (string.IsNullOrEmpty(Path)
                                                         ? ModSecret.RegFolder
                                                         : Path)); // 如果不存在就创建  
                softKey.SetValue(Key, Value);
            }
            catch (Exception ex)
            {
                Log(ex, "写入注册表出错：" + Key, ThrowException ? LogLevel.Hint : LogLevel.Developer);
                if (ThrowException)
                    throw;
            }
        }
        // 路径处理
        /// <summary>
        ///     从文件路径或者 Url 获取不包含文件名的路径，或获取文件夹的父文件夹路径。
        ///     取决于原路径格式，路径以 / 或 \ 结尾。
        ///     不包含路径将会抛出异常。
        /// </summary>
        public static string GetPathFromFullPath(string FilePath)
        {
            string GetPathFromFullPathRet = default;
            if (!(FilePath.Contains(@"\") || FilePath.Contains("/")))
                throw new Exception("不包含路径：" + FilePath);
            if (FilePath.EndsWithF(@"\") || FilePath.EndsWithF("/"))
            {
                // 是文件夹路径
                var IsRight = FilePath.EndsWithF(@"\");
                FilePath = Strings.Left(FilePath, Strings.Len(FilePath) - 1);
                GetPathFromFullPathRet = Strings.Left(FilePath, FilePath.LastIndexOfAny(new[] { '\\', '/' })) +
                                         (IsRight ? @"\" : "/");
            }
            else
            {
                // 是文件路径
                GetPathFromFullPathRet = Strings.Left(FilePath, FilePath.LastIndexOfAny(new[] { '\\', '/' }) + 1);
                if (string.IsNullOrEmpty(GetPathFromFullPathRet))
                    throw new Exception("不包含路径：" + FilePath);
            }

            return GetPathFromFullPathRet;
        }

        /// <summary>
        ///     从文件路径或者 Url 获取不包含路径的文件名。不包含文件名将会抛出异常。
        /// </summary>
        public static string GetFileNameFromPath(string FilePath)
        {
            FilePath = FilePath.Replace("/", @"\");
            if (FilePath.EndsWithF(@"\"))
                throw new Exception("不包含文件名：" + FilePath);
            if (FilePath.Contains("?"))
                FilePath = FilePath.Substring(0, FilePath.IndexOfF("?")); // 去掉网络参数后的 ?
            if (FilePath.Contains(@"\"))
                FilePath = FilePath.Substring(FilePath.LastIndexOfF(@"\") + 1);
            var length = FilePath.Length;
            if (length == 0)
                throw new Exception("不包含文件名：" + FilePath);
            if (length > 250)
                throw new PathTooLongException("文件名过长：" + FilePath);
            return FilePath;
        }

        /// <summary>
        ///     从文件路径或者 Url 获取不包含路径与扩展名的文件名。不包含文件名将会抛出异常。
        /// </summary>
        public static string GetFileNameWithoutExtentionFromPath(string FilePath)
        {
            return Path.GetFileNameWithoutExtension(FilePath);
        }

        /// <summary>
        ///     从文件夹路径获取文件夹名。
        /// </summary>
        public static string GetFolderNameFromPath(string FolderPath)
        {
            if (FolderPath.EndsWithF(@":\") || FolderPath.EndsWithF(@":\\"))
                return FolderPath.Substring(0, 1);
            if (FolderPath.EndsWithF(@"\") || FolderPath.EndsWithF("/"))
                FolderPath = Strings.Left(FolderPath, FolderPath.Length - 1);
            return GetFileNameFromPath(FolderPath);
        }

        // 读取、写入、复制文件
        /// <summary>
        ///     复制文件。会自动创建文件夹、会覆盖已有的文件。
        /// </summary>
        public static void CopyFile(string FromPath, string ToPath)
        {
            try
            {
                // 还原文件路径
                if (!FromPath.Contains(@":\"))
                    FromPath = ExePath + FromPath;
                if (!ToPath.Contains(@":\"))
                    ToPath = ExePath + ToPath;
                // 如果复制同一个文件则跳过
                if ((FromPath ?? "") == (ToPath ?? ""))
                    return;
                // 确保目录存在
                Directory.CreateDirectory(GetPathFromFullPath(ToPath));
                // 复制文件
                File.Copy(FromPath, ToPath, true);
            }
            catch (Exception ex)
            {
                throw new Exception("复制文件出错：" + FromPath + " → " + ToPath, ex);
            }
        }

        /// <summary>
        ///     读取文件，如果失败则返回空数组。
        /// </summary>
        public static byte[] ReadFileBytes(string FilePath, Encoding Encoding = null)
        {
            try
            {
                // 还原文件路径
                if (!FilePath.Contains(@":\"))
                    FilePath = ExePath + FilePath;
                if (File.Exists(FilePath))
                    using (var ReadStream =
                           new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) // 支持读取使用中的文件
                    {
                        using (var ms = new MemoryStream())
                        {
                            ReadStream.CopyTo(ms);
                            return ms.ToArray();
                        }
                    }

                Log("[System] 欲读取的文件不存在，已返回空内容：" + FilePath);
                return Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                Log(ex, "读取文件出错：" + FilePath);
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        ///     读取文件，如果失败则返回空字符串。
        /// </summary>
        /// <param name="FilePath">文件完整或相对路径。</param>
        public static string ReadFile(string FilePath, Encoding Encoding = null)
        {
            string ReadFileRet = default;
            var FileBytes = ReadFileBytes(FilePath);
            ReadFileRet = Encoding is null ? DecodeBytes(FileBytes) : Encoding.GetString(FileBytes);
            return ReadFileRet;
        }

        /// <summary>
        ///     读取流中的所有文本。
        /// </summary>
        public static string ReadFile(Stream Stream, Encoding Encoding = null)
        {
            try
            {
                var readedContent = new MemoryStream();
                Stream.CopyTo(readedContent);
                var Bts = readedContent.ToArray();
                return (Encoding ?? EncodingDetector.DetectEncoding(Bts)).GetString(Bts);
            }
            catch (Exception ex)
            {
                Log(ex, "读取流出错");
                return "";
            }
        }

        /// <summary>
        ///     写入文件。
        /// </summary>
        /// <param name="FilePath">文件完整或相对路径。</param>
        /// <param name="Text">文件内容。</param>
        /// <param name="Append">是否将文件内容追加到当前文件，而不是覆盖它。</param>
        public static void WriteFile(string FilePath, string Text, bool Append = false, Encoding? Encoding = null)
        {
            // 处理相对路径
            if (!FilePath.Contains(@":\"))
                FilePath = ExePath + FilePath;
            // 确保目录存在
            Directory.CreateDirectory(GetPathFromFullPath(FilePath));
            // 写入文件
            if (Append)
                // 追加目前文件
                using (var writer = new StreamWriter(FilePath, true,
                           Encoding ?? EncodingDetector.DetectEncoding(ReadFileBytes(FilePath))))
                {
                    writer.Write(Text);
                }
            else
                // 直接写入字节
                File.WriteAllBytes(FilePath,
                    Encoding is null ? new UTF8Encoding(false).GetBytes(Text) : Encoding.GetBytes(Text));
        }

        /// <summary>
        ///     写入文件。
        ///     如果 CanThrow 设置为 False，返回是否写入成功。
        /// </summary>
        /// <param name="FilePath">文件完整或相对路径。</param>
        /// <param name="Content">文件内容。</param>
        /// <param name="Append">是否将文件内容追加到当前文件，而不是覆盖它。</param>
        public static void WriteFile(string FilePath, byte[] Content, bool Append = false)
        {
            // 处理相对路径
            if (!FilePath.Contains(@":\"))
                FilePath = ExePath + FilePath;
            // 确保目录存在
            Directory.CreateDirectory(GetPathFromFullPath(FilePath));
            // 写入文件
            File.WriteAllBytes(FilePath, Content);
        }

        /// <summary>
        ///     将流写入文件。
        /// </summary>
        /// <param name="FilePath">文件完整或相对路径。</param>
        public static bool WriteFile(string FilePath, Stream Stream)
        {
            try
            {
                // 还原文件路径
                if (!FilePath.Contains(@":\"))
                    FilePath = ExePath + FilePath;
                // 确保目录存在
                Directory.CreateDirectory(GetPathFromFullPath(FilePath));
                // 读取流
                using (var fs = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    fs.SetLength(0L);
                    Stream.CopyTo(fs);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log(ex, "保存流出错");
                return false;
            }
        }

        /// <summary>
        ///     解码 Bytes。
        /// </summary>
        public static string DecodeBytes(byte[] Bytes)
        {
            var Length = Bytes.Length;
            if (Length < 3)
                return Encoding.UTF8.GetString(Bytes);
            // 根据 BOM 判断编码
            if (Bytes[0] >= 0xEF)
            {
                // 有 BOM 类型
                if (Bytes[0] == 0xEF && Bytes[1] == 0xBB) return Encoding.UTF8.GetString(Bytes, 3, Length - 3);

                if (Bytes[0] == 0xFE && Bytes[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(Bytes, 3, Length - 3);

                if (Bytes[0] == 0xFF && Bytes[1] == 0xFE) return Encoding.Unicode.GetString(Bytes, 3, Length - 3);

                return Encoding.GetEncoding("GB18030").GetString(Bytes, 3, Length - 3);
            }

            // 无 BOM 文件：GB18030（ANSI）或 UTF8
            var UTF8 = Encoding.UTF8.GetString(Bytes);
            var ErrorChar = Encoding.UTF8.GetString(new[] { (byte)239, (byte)191, (byte)189 }).ToCharArray()[0];
            if (UTF8.Contains(ErrorChar)) return Encoding.GetEncoding("GB18030").GetString(Bytes);

            return UTF8;
        }

        public static object GetHexString(Memory<byte> bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var c in bytes.Span)
                sb.Append(c.ToString("x2"));

            return sb.ToString();
        }

        // 文件校验
        /// <summary>
        ///     获取文件 MD5，若失败则返回空字符串。
        /// </summary>
        public static string GetFileMD5(string FilePath)
        {
            var Retry = false;
            Re: ;

            try
            {
                // 获取 MD5
                using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return Conversions.ToString(GetHexString(MD5Provider.Instance.ComputeHash(fs)));
                }
            }
            catch (Exception ex)
            {
                if (Retry || ex is FileNotFoundException)
                {
                    Log(ex, "获取文件 MD5 失败：" + FilePath);
                    return "";
                }

                Retry = true;
                Log(ex, "获取文件 MD5 可重试失败：" + FilePath, LogLevel.Normal);
                Thread.Sleep(RandomUtils.NextInt(200, 500));
                goto Re;
            }
        }

        /// <summary>
        ///     获取文件 SHA512，若失败则返回空字符串。
        /// </summary>
        public static string GetFileSHA512(string FilePath)
        {
            var Retry = false;
            Re: ;

            try
            {
                // '检测该文件是否在下载中，若在下载则放弃检测
                // If IgnoreOnDownloading AndAlso NetManage.Files.ContainsKey(FilePath) AndAlso NetManage.Files(FilePath).State <= NetState.Merge Then Return ""
                // 获取 SHA512
                using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return Conversions.ToString(GetHexString(SHA512Provider.Instance.ComputeHash(fs)));
                }
            }
            catch (Exception ex)
            {
                if (Retry || ex is FileNotFoundException)
                {
                    Log(ex, "获取文件 SHA512 失败：" + FilePath);
                    return "";
                }

                Retry = true;
                Log(ex, "获取文件 SHA512 可重试失败：" + FilePath, LogLevel.Normal);
                Thread.Sleep(RandomUtils.NextInt(200, 500));
                goto Re;
            }
        }

        /// <summary>
        ///     获取文件 SHA256，若失败则返回空字符串。
        /// </summary>
        public static string GetFileSHA256(string FilePath)
        {
            var Retry = false;
            Re: ;

            try
            {
                // '检测该文件是否在下载中，若在下载则放弃检测
                // If IgnoreOnDownloading AndAlso NetManage.Files.ContainsKey(FilePath) AndAlso NetManage.Files(FilePath).State <= NetState.Merge Then Return ""
                // 获取 SHA256
                using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return Conversions.ToString(GetHexString(SHA256Provider.Instance.ComputeHash(fs)));
                }
            }
            catch (Exception ex)
            {
                if (Retry || ex is FileNotFoundException)
                {
                    Log(ex, "获取文件 SHA256 失败：" + FilePath);
                    return "";
                }

                Retry = true;
                Log(ex, "获取文件 SHA256 可重试失败：" + FilePath, LogLevel.Normal);
                Thread.Sleep(RandomUtils.NextInt(200, 500));
                goto Re;
            }
        }

        /// <summary>
        ///     获取文件 SHA1，若失败则返回空字符串。
        /// </summary>
        public static string GetFileSHA1(string FilePath)
        {
            var Retry = false;
            Re: ;

            try
            {
                // 获取 SHA1
                using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return Conversions.ToString(GetHexString(SHA1Provider.Instance.ComputeHash(fs)));
                }
            }
            catch (Exception ex)
            {
                if (Retry || ex is FileNotFoundException)
                {
                    Log(ex, "获取文件 SHA1 失败：" + FilePath);
                    return "";
                }

                Retry = true;
                Log(ex, "获取文件 SHA1 可重试失败：" + FilePath, LogLevel.Normal);
                Thread.Sleep(RandomUtils.NextInt(200, 500));
                goto Re;
            }
        }
        /// <summary>
        ///     文件的校验规则。
        /// </summary>
        public class FileChecker
        {
            /// <summary>
            ///     文件的准确大小。
            ///     不检查则为 -1。
            /// </summary>
            public long ActualSize = -1;

            /// <summary>
            ///     是否可以使用已经存在的文件。
            /// </summary>
            public bool CanUseExistsFile = true;

            /// <summary>
            ///     文件的 MD5、SHA1 或 SHA256。会根据输入字符串的长度自动判断种类。
            ///     不检查则为 Nothing。
            /// </summary>
            public string Hash;

            /// <summary>
            ///     是否要求为 JSON 文件。
            ///     即，开头结尾必须为 {} 或 []。
            /// </summary>
            public bool IsJson;

            /// <summary>
            ///     文件的最小大小。
            ///     不检查则为 -1。
            /// </summary>
            public long MinSize = -1;

            public FileChecker(long MinSize = -1, long ActualSize = -1, string Hash = null, bool CanUseExistsFile = true,
                bool IsJson = false)
            {
                this.ActualSize = ActualSize;
                this.MinSize = MinSize;
                this.Hash = Hash;
                this.CanUseExistsFile = CanUseExistsFile;
                this.IsJson = IsJson;
            }

            /// <summary>
            ///     检查文件。若成功则返回 Nothing，失败则返回错误的描述文本，描述文本不以句号结尾。不会抛出错误。
            /// </summary>
            public string Check(string LocalPath)
            {
                try
                {
                    Log($"[Checker] 开始校验文件 {LocalPath}", LogLevel.Developer);
                    var Info = new FileInfo(LocalPath);
                    if (!Info.Exists)
                        return "文件不存在：" + LocalPath;
                    var FileSize = Info.Length;
                    var ErrorMessage = new List<string>();
                    var AllowIgnore = false; // 允许相信哈希正确但是大小不正确
                    if (!string.IsNullOrEmpty(Hash))
                    {
                        if (Hash.Length < 35) // MD5
                        {
                            var ComputedHash = GetFileMD5(LocalPath);
                            if ((Hash.ToLowerInvariant() ?? "") != (ComputedHash ?? ""))
                                ErrorMessage.Add("文件 MD5 应为 " + Hash + "，实际为 " + ComputedHash);
                        }
                        else if (Hash.Length == 64) // SHA256
                        {
                            var ComputedHash = GetFileSHA256(LocalPath);
                            if ((Hash.ToLowerInvariant() ?? "") != (ComputedHash ?? ""))
                                ErrorMessage.Add("文件 SHA256 应为 " + Hash + "，实际为 " + ComputedHash);
                        }
                        else // SHA1 (40)
                        {
                            var ComputedHash = GetFileSHA1(LocalPath);
                            if ((Hash.ToLowerInvariant() ?? "") != (ComputedHash ?? ""))
                                ErrorMessage.Add("文件 SHA1 应为 " + Hash + "，实际为 " + ComputedHash);
                        }

                        AllowIgnore = ErrorMessage.Count == 0;
                    }

                    if (ActualSize >= 0L && ActualSize != FileSize && !AllowIgnore) // 不允许忽略大小不正确的情况
                        ErrorMessage.Add($"文件大小应为 {ActualSize} B，实际为 {FileSize} B" +
                                         (FileSize < 2000L ? "，内容为" + ReadFile(LocalPath) : ""));

                    if (MinSize >= 0L && MinSize > FileSize)
                        ErrorMessage.Add($"文件大小应大于 {MinSize} B，实际为 {FileSize} B" +
                                         (FileSize < 2000L ? "，内容为：" + ReadFile(LocalPath) : ""));

                    if (IsJson)
                    {
                        var Content = ReadFile(LocalPath);
                        if (string.IsNullOrEmpty(Content))
                            throw new Exception("读取到的文件为空");
                        try
                        {
                            GetJson(Content);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("不是有效的 Json 文件", ex);
                        }
                    }

                    if (ErrorMessage.Count != 0)
                    {
                        ErrorMessage.Insert(0, $"实际校验地址：{LocalPath}");
                        return ErrorMessage.Join(";");
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    Log(ex, "检查文件出错");
                    return ex.ToString();
                }
            }
        }

        /// <summary>
        ///     尝试根据后缀名判断文件种类并解压文件，支持 gz 与 zip，会尝试将 Jar 以 zip 方式解压。
        ///     会尝试创建，但不会清空目标文件夹。
        /// </summary>
        public static void ExtractFile(string CompressFilePath, string DestDirectory, Encoding Encode = null,
            Action<double> ProgressIncrementHandler = null)
        {
            Directory.CreateDirectory(DestDirectory);
            DestDirectory = Path.GetFullPath(DestDirectory);
            if (!DestDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
                DestDirectory += Conversions.ToString(Path.DirectorySeparatorChar);
            if (CompressFilePath.EndsWithF(".gz", true))
                // 以 gz 方式解压
                using (var compressedFile = new FileStream(CompressFilePath, FileMode.Open, FileAccess.Read))
                {
                    using (var decompressStream = new GZipStream(compressedFile, CompressionMode.Decompress))
                    {
                        using (var extractFileStream =
                               new FileStream(
                                   Path.Combine(DestDirectory,
                                       GetFileNameFromPath(CompressFilePath).ToLower().Replace(".tar", "")
                                           .Replace(".gz", "")), FileMode.OpenOrCreate, FileAccess.Write))
                        {
                            decompressStream.CopyTo(extractFileStream);
                        }
                    }
                }
            else
                // 以 zip 方式解压
                using (var Archive = ZipFile.Open(CompressFilePath, ZipArchiveMode.Read,
                           Encode ?? Encoding.GetEncoding("GB18030")))
                {
                    var TotalCount = Archive.Entries.Count;
                    foreach (var Entry in Archive.Entries)
                    {
                        if (ProgressIncrementHandler is not null)
                            ProgressIncrementHandler(1d / TotalCount);
                        var DestinationPath = Path.GetFullPath(Path.Combine(DestDirectory, Entry.FullName));
                        if (!DestinationPath.StartsWithF(DestDirectory))
                            throw new Exception(
                                $"解压文件 {Entry.FullName} 错误：解压文件路径 {DestinationPath} 不在目标目录 {DestDirectory} 内");
                        if (DestinationPath.EndsWithF(@"\") || DestinationPath.EndsWithF("/"))
                        {
                        }
                        else
                        {
                            Directory.CreateDirectory(GetPathFromFullPath(DestinationPath));
                            Entry.ExtractToFile(DestinationPath, true);
                        }
                    }
                }
        }

        /// <summary>
        ///     删除文件夹，返回删除的文件个数。通过参数选择是否抛出异常。
        /// </summary>
        public static int DeleteDirectory(string Path, bool IgnoreIssue = false)
        {
            if (!Directory.Exists(Path))
                return 0;
            var DeletedCount = 0;
            string[] Files;
            try
            {
                Files = Directory.GetFiles(Path);
            }
            catch (DirectoryNotFoundException ex) // #4549
            {
                Log(ex, $"疑似为孤立符号链接，尝试直接删除（{Path}）", LogLevel.Developer);
                Directory.Delete(Path);
                return 0;
            }

            foreach (var FilePath in Files)
            {
                var RetriedFile = false;
                RetryFile: ;

                try
                {
                    File.Delete(FilePath);
                    DeletedCount += 1;
                }
                catch (Exception ex)
                {
                    if (!RetriedFile)
                    {
                        RetriedFile = true;
                        Log(ex, $"删除文件失败，将在 0.3s 后重试（{FilePath}）");
                        Thread.Sleep(300);
                        goto RetryFile;
                    }

                    if (IgnoreIssue)
                        Log(ex, "删除单个文件可忽略地失败");
                    else
                        throw;
                }
            }

            foreach (var str in Directory.GetDirectories(Path))
                DeleteDirectory(str, IgnoreIssue);
            var RetriedDir = false;
            RetryDir: ;

            try
            {
                Directory.Delete(Path, true);
            }
            catch (Exception ex)
            {
                if (!RetriedDir && !RunInUi())
                {
                    RetriedDir = true;
                    Log(ex, $"删除文件夹失败，将在 0.3s 后重试（{Path}）");
                    Thread.Sleep(300);
                    goto RetryDir;
                }

                if (IgnoreIssue)
                    Log(ex, "删除单个文件夹可忽略地失败");
                else
                    throw;
            }

            return DeletedCount;
        }

        /// <summary>
        ///     复制文件夹，失败会抛出异常。
        /// </summary>
        public static void CopyDirectory(string FromPath, string ToPath, Action<double> ProgressIncrementHandler = null)
        {
            FromPath = FromPath.Replace("/", @"\");
            if (!FromPath.EndsWithF(@"\"))
                FromPath += @"\";
            ToPath = ToPath.Replace("/", @"\");
            if (!ToPath.EndsWithF(@"\"))
                ToPath += @"\";
            var AllFiles = EnumerateFiles(FromPath).ToList();
            var FileCount = AllFiles.Count;
            foreach (var File in AllFiles)
            {
                CopyFile(File.FullName, File.FullName.Replace(FromPath, ToPath));
                if (ProgressIncrementHandler is not null)
                    ProgressIncrementHandler(1d / FileCount);
            }
        }

        /// <summary>
        ///     遍历文件夹中的所有文件。
        /// </summary>
        public static IEnumerable<FileInfo> EnumerateFiles(string Directory)
        {
            var Info = new DirectoryInfo(ShortenPath(Directory));
            if (!Info.Exists)
                return new List<FileInfo>();
            return Info.EnumerateFiles("*", SearchOption.AllDirectories);
        }

        /// <summary>
        ///     若路径长度大于指定值，则将长路径转换为短路径。
        /// </summary>
        public static string ShortenPath(string LongPath, int ShortenThreshold = 247)
        {
            if (LongPath.Length <= ShortenThreshold)
                return LongPath;
            var ShortPath = new StringBuilder(260);
            GetShortPathName(LongPath, ShortPath, 260);
            return ShortPath.ToString();
        }

        public static void MoveDirectory(string SourceDir, string TargetDir)
        {
            if (!Directory.Exists(TargetDir))
                Directory.CreateDirectory(TargetDir);
            foreach (var FilePath in Directory.GetFiles(SourceDir))
            {
                var FileName = GetFileNameFromPath(FilePath);
                File.Move(FilePath, Path.Combine(TargetDir, FileName));
            }

            foreach (var DirPath in Directory.GetDirectories(SourceDir))
            {
                var DirName = GetFolderNameFromPath(DirPath);
                MoveDirectory(DirPath, Path.Combine(TargetDir, DirName));
            }
        }

        [DllImport("kernel32", EntryPoint = "GetShortPathNameA")]
        private static extern int GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, int cchBuffer);

        public static void CreateSymbolicLink(string LinkPath, string TargetPath, int Flags)
        {
            var CMDProcess = new Process();
            var LinkDPath = ModLaunch.ExtractLinkD();
            {
                var withBlock = CMDProcess.StartInfo;
                withBlock.FileName = LinkDPath;
                withBlock.Arguments = $"\"{LinkPath}\" \"{TargetPath}\"";
                withBlock.CreateNoWindow = true;
                withBlock.UseShellExecute = false;
            }
            CMDProcess.Start();
            while (!CMDProcess.HasExited)
            {
            }
        }

        #endregion
        /// <summary>
        ///     可用于临时存放文件的，不含任何特殊字符的文件夹路径，以“\”结尾。
        /// </summary>
        public static string PathPure = GetPureASCIIDir();

        private static string GetPureASCIIDir()
        {
            if (ExePath.IsASCII()) return ExePath + @"PCL\";

            if (PathAppdata.IsASCII()) return PathAppdata;

            if (PathTemp.IsASCII()) return PathTemp;

            return OsDrive + @"ProgramData\PCL\";
        }
        /// <summary>
        ///     静默运行文件并返回输出流字符串。执行失败会抛出异常。
        /// </summary>
        /// <param name="FileName">文件名。可以为“notepad”等缩写。</param>
        /// <param name="Arguments">运行参数。</param>
        /// <param name="Timeout">等待该程序结束的最长时间（毫秒）。超时会抛出错误。</param>
        public static string ShellAndGetOutput(string FileName, string Arguments = "", int Timeout = 1000000,
            string WorkingDirectory = null)
        {
            var Info = new ProcessStartInfo
            {
                FileName = FileName,
                Arguments = Arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // 设置工作目录（如果提供）
            if (!string.IsNullOrEmpty(WorkingDirectory)) Info.WorkingDirectory = WorkingDirectory.TrimEnd('\\');

            Log("[System] 执行外部命令并等待返回结果：" + FileName + " " + Arguments);

            using (var Program = new Process { StartInfo = Info })
            {
                Program.Start();

                // 异步读取输出和错误流
                var outputTask = Program.StandardOutput.ReadToEndAsync();
                var errorTask = Program.StandardError.ReadToEndAsync();

                // 等待进程退出或超时
                if (Program.WaitForExit(Timeout))
                {
                    // 确保异步读取完成
                    Task.WaitAll(outputTask, errorTask);
                }
                else
                {
                    // 超时后终止进程
                    Program.Kill();
                    // 仍然尝试获取已输出的内容
                    Task.WaitAll(outputTask, errorTask);
                }

                // 合并结果并返回
                return outputTask.Result + errorTask.Result;
            }
        }
        /// <summary>
        ///     从剪切板粘贴文件或文件夹
        /// </summary>
        /// <param name="dest">目标文件夹</param>
        /// <param name="copyFile">是否粘贴文件</param>
        /// <param name="copyDir">是否粘贴文件夹</param>
        /// <returns>总共粘贴的数量</returns>
        public static int PasteFileFromClipboard(string dest, bool copyFile = true, bool copyDir = true)
        {
            Log("[System] 从剪贴板粘贴文件到：" + dest);
            try
            {
                var files = Clipboard.GetFileDropList();
                if (files.Count.Equals(0))
                {
                    Log("[System] 剪贴板内无文件可粘贴");
                    return 0;
                }

                var CopiedFiles = 0;
                var CopiedFolders = 0;
                foreach (var i in files)
                {
                    if (copyFile && File.Exists(i)) // 文件
                        try
                        {
                            var thisDest = dest + GetFileNameFromPath(i);
                            if (File.Exists(thisDest))
                            {
                                Log("[System] 已存在同名文件：" + thisDest);
                            }
                            else
                            {
                                File.Copy(i, thisDest);
                                CopiedFiles += 1;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log(ex, "[System] 复制文件时出错");
                            continue;
                        }

                    if (copyDir && Directory.Exists(i)) // 文件夹
                        try
                        {
                            var thisDest = dest + GetFolderNameFromPath(i);
                            if (Directory.Exists(thisDest))
                            {
                                Log("[System] 已存在同名文件夹：" + thisDest);
                            }
                            else
                            {
                                CopyDirectory(i, thisDest);
                                CopiedFolders += 1;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log(ex, "[System] 复制文件时出错");
                        }
                }

                ModMain.Hint("[System] 已粘贴 " + CopiedFiles + " 个文件和 " + CopiedFolders + " 个文件夹");
            }
            catch (Exception ex)
            {
                Log(ex, "[System] 从剪切板粘贴文件失败", LogLevel.Hint);
            }

            return 0;
        }
        /// <summary>
        ///     获取程序打包资源的输入流。该资源必须声明为 <c>Resource</c> 类型，否则将会报错，<c>Images</c>
        ///     和 <c>Resources</c> 目录已默认声明该类型。
        /// </summary>
        public static Stream GetResourceStream(string path)
        {
            var resourceInfo =
                System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/{path}", UriKind.Absolute));
            return resourceInfo?.Stream;
        }
        /// <summary>
        ///     检查是否拥有某一文件夹的 I/O 权限。如果文件夹不存在，会返回 False。
        /// </summary>
        public static bool CheckPermission(string Path)
        {
            try
            {
                if (string.IsNullOrEmpty(Path))
                    return false;
                if (!Path.EndsWithF(@"\"))
                    Path += @"\";
                if (Path.EndsWithF(@":\System Volume Information\") || Path.EndsWithF(@":\$RECYCLE.BIN\"))
                    return false;
                if (!Directory.Exists(Path))
                    return false;
                var FileName = "CheckPermission" + GetUuid();
                if (File.Exists(Path + FileName))
                    File.Delete(Path + FileName);
                File.Create(Path + FileName).Dispose();
                File.Delete(Path + FileName);
                return true;
            }
            catch (Exception ex)
            {
                Log(ex, "没有对文件夹 " + Path + " 的权限，请尝试以管理员权限运行 PCL");
                return false;
            }
        }

        /// <summary>
        ///     检查是否拥有某一文件夹的 I/O 权限。如果出错，则抛出异常。
        /// </summary>
        public static void CheckPermissionWithException(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
                throw new ArgumentNullException("文件夹名不能为空！");
            if (!Path.EndsWithF(@"\"))
                Path += @"\";
            if (!Directory.Exists(Path))
                throw new DirectoryNotFoundException("文件夹不存在！");
            if (File.Exists(Path + "CheckPermission"))
                File.Delete(Path + "CheckPermission");
            File.Create(Path + "CheckPermission").Dispose();
            File.Delete(Path + "CheckPermission");
        }
        // UI 截图
        /// <summary>
        ///     将某个控件的呈现转换为图片。
        /// </summary>
        public static ImageBrush ControlBrush(FrameworkElement UI)
        {
            var Width = UI.ActualWidth;
            var Height = UI.ActualHeight;
            if (Width < 1d || Height < 1d)
                return new ImageBrush();
            var bmp = new RenderTargetBitmap((int)Math.Round(GetPixelSize(Width)), (int)Math.Round(GetPixelSize(Height)),
                DPI, DPI, PixelFormats.Pbgra32);
            bmp.Render(UI);
            return new ImageBrush(bmp);
        }

        /// <summary>
        ///     将某个控件的模拟呈现转换为图片。
        /// </summary>
        public static ImageBrush ControlBrush(FrameworkElement UI, double Width, double Height, double Left = 0d,
            double Top = 0d)
        {
            UI.Measure(new Size(Width, Height));
            UI.Arrange(new Rect(0d, 0d, Width, Height));
            var bmp = new RenderTargetBitmap((int)Math.Round(GetPixelSize(Width)), (int)Math.Round(GetPixelSize(Height)),
                DPI, DPI, PixelFormats.Default);
            bmp.Render(UI);
            if (!(Left == 0d && Top == 0d))
                UI.Arrange(new Rect(Left, Top, Width, Height));
            return new ImageBrush(bmp);
        }
    }
}

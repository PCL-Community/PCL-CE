using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Microsoft.Win32;
using Newtonsoft.Json;
using PCL.Core.App;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Utils;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;
using System.Collections;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xaml;
using System.Xml.Linq;
using FontFamily = System.Windows.Media.FontFamily;
using Size = System.Windows.Size;

namespace PCL;

public static class ModBase
{
    #region 声明

    // 下列版本信息由更新器自动修改
    public static readonly string VersionBaseName = Basics.VersionName;
    public static readonly string VersionStandardCode = Basics.Metadata.Version.StandardVersion;
    public static readonly string UpstreamVersion = Basics.Metadata.Version.UpstreamVersion;
    public static readonly string CommitHash = Basics.Metadata.Version.Commit;
    public static readonly string CommitHashShort = Basics.Metadata.Version.CommitDigest;
    public static readonly int VersionCode = Basics.VersionCode;

#if DEBUG
    public const string VersionBranchName = "Debug";
    public const string VersionBranchCode = "100";
#elif DEBUGCI
    public const string VersionBranchName = "CI";
    public const string VersionBranchCode = "50";
#else
    public const string VersionBranchName = "Publish";
    public const string VersionBranchCode = "0";
#endif
    /// <summary>
    /// 主窗口句柄。
    /// </summary>
    public static nint FrmHandle;

    // 龙猫味石山小记: 用最不靠谱的实现写出能跑的代码 (AppDomain.CurrentDomain.SetupInformation.ApplicationBase 获取到的是当前工作目录而不是可执行文件所在目录)
    /// <summary>
    /// 程序可执行文件所在目录，以“\”结尾。
    /// </summary>
    public static readonly string ExePath = Conversions.ToString(Basics.ExecutableDirectory.EndsWith(@"\")
        ? Basics.ExecutableDirectory
        : Basics.ExecutableDirectory + @"\");

    /// <summary>
    /// 程序可执行文件完整路径。
    /// </summary>
    public static readonly string ExePathWithName = Basics.ExecutablePath;

    /// <summary>
    /// 程序内嵌图片文件夹路径，以“/”结尾。
    /// </summary>
    public static readonly string PathImage = "pack://application:,,,/Plain Craft Launcher 2;component/Images/";

    /// <summary>
    /// 当前程序的语言。
    /// </summary>
    public static string Lang = "zh_CN";

    /// <summary>
    /// 设置对象。
    /// </summary>
    public static ModSetup Setup = new();

    /// <summary>
    /// 程序的打开计时。
    /// </summary>
    public static long ApplicationStartTick = TimeUtils.GetTimeTick();

    /// <summary>
    /// 程序打开时的时间。
    /// </summary>
    public static DateTime ApplicationOpenTime = DateTime.Now;

    /// <summary>
    /// 识别码。
    /// </summary>
    public static string UniqueAddress = ModSecret.SecretGetUniqueAddress();

    /// <summary>
    /// 程序是否已结束。
    /// </summary>
    public static bool IsProgramEnded = false;

    /// <summary>
    /// 是否为 32 位系统。
    /// </summary>
    public static bool Is32BitSystem = !Environment.Is64BitOperatingSystem;

    /// <summary>
    /// 是否为 ARM64 架构。
    /// </summary>
    public static bool IsArm64System = RuntimeInformation.OSArchitecture == Architecture.Arm64;

    /// <summary>
    /// 是否使用 GBK 编码。
    /// </summary>
    public static bool IsGBKEncoding = Encoding.Default.CodePage == 936;

    /// <summary>
    /// 系统盘盘符，以 \ 结尾。例如 “C:\”。
    /// </summary>
    public static string OsDrive =
        Environment.GetLogicalDrives().Where(p => Directory.Exists(p)).First().ToUpper().First() + @":\"; // #3799

    /// <summary>
    /// 程序的缓存文件夹路径，以 \ 结尾。
    /// </summary>
    public static string PathTemp = Core.App.Paths.Temp + @"\";

    /// <summary>
    /// AppData 中的 PCL 文件夹路径，以 \ 结尾。
    /// </summary>
    public static string PathAppdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\PCL\";

    /// <summary>
    /// AppData 中的 PCLCE 配置文件夹路径，以 \ 结尾。
    /// </summary>
    public static string PathAppdataConfig = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                                             (VersionBranchName == "Debug" ? @"\.pclcedebug\" : @"\.pclce\");

    public static string PathHelpFolder = PathTemp + @"CE\Help\";

    #endregion

    // =============================
    // 注册表
    // =============================

    /// <summary>
    /// 重命名一个注册表子键。不可用于包含子键的子键。
    /// </summary>
    /// <exception cref="NotSupportedException">在尝试对包含子键的子键进行重命名时抛出</exception>
    public static void RenameReg(RegistryKey parentKey, string subKeyName, string newSubKeyName)
    {
        if (parentKey.GetSubKeyNames().Contains(newSubKeyName))
        {
            parentKey.DeleteSubKeyTree(newSubKeyName, false);
        }

        var sourceKey = parentKey.OpenSubKey(subKeyName);
        if (sourceKey == null)
        {
            return; // 没有目标项
        }

        var newKey = parentKey.CreateSubKey(newSubKeyName);
        if (sourceKey.GetSubKeyNames().Length > 0)
        {
            throw new NotSupportedException($"不支持对包含子键的子键进行重命名：{sourceKey.GetSubKeyNames()[0]}。");
        }

        foreach (var valueName in sourceKey.GetValueNames())
        {
            var objValue = sourceKey.GetValue(valueName);
            var valKind = sourceKey.GetValueKind(valueName);
            newKey.SetValue(valueName, objValue, valKind);
        }

        parentKey.DeleteSubKeyTree(subKeyName, false);
    }

    /// <summary>
    /// 读取注册表，默认为程序所属。
    /// </summary>
    public static string ReadReg(string key, string defaultValue = "", string path = "")
    {
        string readRegRet;
        try
        {
            var parentKey = Registry.CurrentUser;
            var softKey = parentKey.OpenSubKey($"Software\\{(string.IsNullOrEmpty(path) ? ModSecret.RegFolder : path)}", true);
            if (softKey is null)
            {
                readRegRet = defaultValue; // 不存在则返回默认值
            }
            else
            {
                var readValue = new StringBuilder();
                readValue.AppendLine(softKey.GetValue(key).ToString());
                var value = readValue.ToString().Replace("\r\n", ""); // 去除莫名的回车
                return string.IsNullOrEmpty(value) ? defaultValue : value;
            } // 错误则返回默认值
        }
        catch (Exception ex)
        {
            Log(ex, "读取注册表出错：" + key, LogLevel.Hint);
            return defaultValue;
        }

        return readRegRet;
    }

    /// <summary>
    /// 写入注册表，默认为程序所属。
    /// </summary>
    /// <exception cref="Exception">Throws if failed to write..</exception>
    public static void WriteReg(string key,
        string value,
        bool showException = false,
        string path = "",
        bool throwException = false)
    {
        try
        {
            var parentKey = Registry.CurrentUser;
            var softKey =
                parentKey.OpenSubKey($"Software\\ {(string.IsNullOrEmpty(path) ? ModSecret.RegFolder : path)}", true) ??
                parentKey.CreateSubKey($"Software\\{(string.IsNullOrEmpty(path)
                    ? ModSecret.RegFolder
                    : path)}"); // 如果不存在就创建

            softKey.SetValue(key, value);
        }
        catch (Exception ex)
        {
            Log(ex, "写入注册表出错：" + key, throwException ? LogLevel.Hint : LogLevel.Developer);
            if (throwException)
                throw;
        }
    }

    /// <summary>
    /// 是否存在某个注册表键。
    /// </summary>
    public static bool HasReg(string key)
    {
        return !(ReadReg(key, "\0").Equals("\0", StringComparison.InvariantCulture));
    }

    /// <summary>
    /// 删除注册表键。
    /// </summary>
    public static void DeleteReg(string key, bool throwException = false)
    {
        try
        {
            var subKey = Registry.CurrentUser.OpenSubKey(@"Software\" + ModSecret.RegFolder, true);
            subKey?.DeleteValue(key);
        }
        catch (Exception ex)
        {
            Log(ex, "删除注册表出错：" + key, throwException ? LogLevel.Hint : LogLevel.Developer);
            if (throwException)
                throw;
        }
    }

    #region 文件



    /// <summary>
    /// 从文件路径或者 Url 获取不包含路径与扩展名的文件名。不包含文件名将会抛出异常。
    /// </summary>
    public static string GetFileNameWithoutExtentionFromPath(string FilePath)
    {
        return Path.GetFileNameWithoutExtension(FilePath);
    }

    // 读取、写入、复制文件
    /// <summary>
    /// 复制文件。会自动创建文件夹、会覆盖已有的文件。
    /// </summary>
    /// <exception cref="Exception">在复制文件时发生错误</exception>
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
            Directory.CreateDirectory(PathUtils.GetPathFromFullPath(ToPath));
            // 复制文件
            File.Copy(FromPath, ToPath, true);
        }
        catch (Exception ex)
        {
            throw new Exception($"复制文件出错：{FromPath} → {ToPath}", ex);
        }
    }

    /// <summary>
    /// 读取文件，如果失败则返回空数组。
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
    /// 读取文件，如果失败则返回空字符串。
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
    /// 读取流中的所有文本。
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
    /// 写入文件。
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
        Directory.CreateDirectory(PathUtils.GetPathFromFullPath(FilePath));
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
    /// 写入文件。
    /// 如果 CanThrow 设置为 False，返回是否写入成功。
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
        Directory.CreateDirectory(PathUtils.GetPathFromFullPath(FilePath));
        // 写入文件
        File.WriteAllBytes(FilePath, Content);
    }

    /// <summary>
    /// 将流写入文件。
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
            Directory.CreateDirectory(PathUtils.GetPathFromFullPath(FilePath));
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
    /// 解码 Bytes。
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


    // 文件校验

    #endregion

    #region 文本

    public static char vbLQ = Convert.ToChar(8220);
    public static char vbRQ = Convert.ToChar(8221);

    /// <summary>
    /// 获取 JSON 对象。
    /// </summary>
    [Obsolete("Need replace this in the future")]
    public static object GetJson(string Data)
    {
        try
        {
            return JsonConvert.DeserializeObject(Data,
                new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Local });
        }
        catch (Exception ex)
        {
            var Length = (Data ?? "").Length;
            throw new Exception("格式化 JSON 失败：" + (Length > 2000
                ? Data.Substring(0, 500) + $"...(全长 {Length} 个字符)..." + Strings.Right(Data, 500)
                : Data));
        }
    }


    #endregion

    #region 系统

    public static bool IsUtf8CodePage()
    {
        return Encoding.Default.CodePage == 65001;
    }

    /// <summary>
    /// 线程安全的 List。
    /// 通过在 For Each 循环中使用一个浅表副本规避多线程操作或移除自身导致的异常。
    /// </summary>
    public class SafeList<T> : IEnumerable<T>, IDisposable, ICollection<T>
    {
        private readonly List<T> _internalList;
        private readonly ReaderWriterLockSlim _lock = new();

        public SafeList()
        {
            _internalList = new List<T>();
        }

        public SafeList(IEnumerable<T> data)
        {
            _internalList = new List<T>(data);
        }

        public T this[int index]
        {
            get => _internalList[index];
            set => _internalList[index] = value;
        }

        public void Add(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                _internalList.Add(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool Remove(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                return _internalList.Remove(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _internalList.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _internalList.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public bool IsReadOnly => ((ICollection<T>)_internalList).IsReadOnly;

        public bool Contains(T item)
        {
            return ((ICollection<T>)_internalList).Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            ((ICollection<T>)_internalList).CopyTo(array, arrayIndex);
        }

        public void Dispose()
        {
            _lock.Dispose();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public List<T> ToList()
        {
            _lock.EnterReadLock();
            try
            {
                return _internalList.ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void RemoveAt(int index)
        {
            _lock.EnterWriteLock();
            try
            {
                _internalList.RemoveAt(index);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }

    /// <summary>
    /// 指示接取到这个异常的函数进行重试。
    /// </summary>
    public class RestartException : Exception
    {
    }

    /// <summary>
    /// 判断对象是否为某个泛型类型的实例。
    /// </summary>
    public static bool IsInstanceOfGenericType(this Type genericType, object obj)
    {
        if (obj is null)
            return false;
        var t = obj.GetType();
        while (t is not null)
        {
            if (t.IsGenericType && ReferenceEquals(t.GetGenericTypeDefinition(), genericType))
                return true;
            t = t.BaseType;
        }

        return false;
    }

    private static int Uuid = 1;
    private static object UuidLock;

    /// <summary>
    /// 获取一个全程序内不会重复的数字（伪 Uuid）。
    /// </summary>
    public static int GetUuid()
    {
        if (UuidLock is null)
            UuidLock = new object();
        lock (UuidLock)
        {
            Uuid += 1;
            return Uuid;
        }
    }

    /// <summary>
    /// 将元素与 List 的混合体拆分为元素组。
    /// </summary>
    [Obsolete("由于非泛型导致的这个方法的存在，计划在未来的版本中移除")]
    public static List<T> GetFullList<T>(IList data)
    {
        List<T> GetFullListRet = default;
        GetFullListRet = new List<T>();
        for (int i = 0, loopTo = data.Count - 1; i <= loopTo; i++)
            if (data[i] is ICollection)
                GetFullListRet.AddRange((IEnumerable<T>)data[i]);
            else
                GetFullListRet.Add(Conversions.ToGenericParameter<T>(data[i]));

        return GetFullListRet;
    }


    /// <summary>
    /// 前台运行文件。
    /// </summary>
    /// <param name="FileName">文件名。可以为“notepad”等缩写。</param>
    /// <param name="Arguments">运行参数。</param>
    public static void ShellOnly(string FileName, string Arguments = "")
    {
        try
        {
            FileName = PathUtils.ToShortenPath(FileName);
            using (var Program = new Process())
            {
                Program.StartInfo.Arguments = Arguments;
                Program.StartInfo.FileName = FileName;
                Program.StartInfo.UseShellExecute = true;
                Log("[System] 执行外部命令：" + FileName + " " + Arguments);
                Program.Start();
            }
        }
        catch (Exception ex)
        {
            Log(ex, "打开文件或程序失败：" + FileName, LogLevel.Msgbox);
        }
    }

    /// <summary>
    /// 前台运行文件并返回返回值。
    /// </summary>
    /// <param name="FileName">文件名。可以为“notepad”等缩写。</param>
    /// <param name="Arguments">运行参数。</param>
    /// <param name="Timeout">等待该程序结束的最长时间（毫秒）。超时会返回 Result.Timeout。</param>
    public static Enums.ProcessReturnValues ShellAndGetExitCode(string FileName, string Arguments = "", int Timeout = 1000000)
    {
        try
        {
            using (var Program = new Process())
            {
                Program.StartInfo.Arguments = Arguments;
                Program.StartInfo.FileName = FileName;
                Log("[System] 执行外部命令并等待返回码：" + FileName + " " + Arguments);
                Program.Start();
                if (Program.WaitForExit(Timeout)) return (Enums.ProcessReturnValues)Program.ExitCode;

                return Enums.ProcessReturnValues.Timeout;
            }
        }
        catch (Exception ex)
        {
            Log(ex, "执行命令失败：" + FileName, LogLevel.Msgbox);
            return Enums.ProcessReturnValues.Fail;
        }
    }

    /// <summary>
    /// 静默运行文件并返回输出流字符串。执行失败会抛出异常。
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
    /// 在新的工作线程中执行代码。
    /// </summary>
    public static Thread RunInNewThread(Action Action, string Name = null,
        ThreadPriority Priority = ThreadPriority.Normal)
    {
        var th = new Thread(() =>
            {
                try
                {
                    Action();
                }
                catch (ThreadInterruptedException ex)
                {
                    Log(Name + "：线程已中止");
                }
                catch (Exception ex)
                {
                    Log(ex, Name + "：线程执行失败", LogLevel.Feedback);
                }
            })
        { Name = Name ?? "Runtime New Invoke " + GetUuid() + "#", Priority = Priority };
        th.Start();
        return th;
    }

    /// <summary>
    /// 确保在 UI 线程中执行代码。
    /// 如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
    /// 为防止线程互锁，请仅在开始加载动画、从 UI 获取输入时使用！
    /// </summary>
    public static Output RunInUiWait<Output>(Func<Output> Action)
    {
        if (RunInUi()) return Action();

        return System.Windows.Application.Current.Dispatcher.Invoke(Action);
    }

    /// <summary>
    /// 确保在 UI 线程中执行代码。
    /// 如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
    /// 为防止线程互锁，请仅在开始加载动画、从 UI 获取输入时使用！
    /// </summary>
    public static void RunInUiWait(Action Action)
    {
        if (System.Windows.Application.Current is null)
            return;
        if (RunInUi())
            Action();
        else
            System.Windows.Application.Current.Dispatcher.Invoke(Action);
    }

    /// <summary>
    /// 确保在 UI 线程中执行代码，代码按触发顺序执行。
    /// 如果当前并非 UI 线程，也不阻断当前线程的执行。
    /// </summary>
    public static void RunInUi(Action Action, bool ForceWaitUntilLoaded = false)
    {
        if (System.Windows.Application.Current is null)
            return;
        if (RunInUi())
            Action();
        else
            System.Windows.Application.Current.Dispatcher.InvokeAsync(Action,
                ForceWaitUntilLoaded ? DispatcherPriority.Loaded : DispatcherPriority.Normal);
    }

    /// <summary>
    /// 确保在工作线程中执行代码。
    /// </summary>
    public static void RunInThread(Action Action)
    {
        if (RunInUi())
            RunInNewThread(Action, "Runtime Invoke " + GetUuid() + "#");
        else
            Action();
    }

    /// <summary>
    /// 获取程序启动参数。
    /// </summary>
    /// <param name="Name">参数名。</param>
    /// <param name="DefaultValue">默认值。</param>
    public static object GetProgramArgument(string Name, object DefaultValue = null)
    {
        var AllArguments = Interaction.Command().Split(" ");
        for (int i = 0, loopTo = AllArguments.Length - 1; i <= loopTo; i++)
            if ((AllArguments[i] ?? "") == ("-" + Name ?? ""))
            {
                if (AllArguments.Length == i + 1 || AllArguments[i + 1].StartsWithF("-"))
                    return true;
                return AllArguments[i + 1];
            }

        return DefaultValue;
    }

    /// <summary>
    /// 打开网页。
    /// </summary>
    public static void OpenWebsite(string Url)
    {
        try
        {
            if (!Url.StartsWithF("http", true) && !Url.StartsWithF("minecraft://", true))
                throw new Exception(Url + " 不是一个有效的网址，它必须以 http 开头！");
            Log("[System] 正在打开网页：" + Url);
            var psi = new ProcessStartInfo(Url)
            {
                UseShellExecute = true,
            };
            _ = Task.Run(() => Process.Start(psi));
        }
        catch (Exception ex)
        {
            Log(ex, "无法打开网页（" + Url + "）");
            ClipboardSet(Url, false);
            ModMain.MyMsgBox(
                "可能由于浏览器未正确配置，PCL 无法为你打开网页。" + "\r\n" + "网址已经复制到剪贴板，若有需要可以手动粘贴访问。" + "\r\n" +
                $"网址：{Url}", "无法打开网页");
        }
    }

    /// <summary>
    /// 打开 explorer。
    /// 若不以 \ 结尾，则将视作文件路径，打开并选中此文件。
    /// </summary>
    public static void OpenExplorer(string Location)
    {
        try
        {
            Location = PathUtils.ToShortenPath(Location.Replace('/', '\\').Trim(' ', '"'));
            Log("[System] 正在打开资源管理器：" + Location);
            if (Location is [.., '\\'])
                ShellOnly(Location);
            else
                ShellOnly("explorer", $"/select,\"{Location}\"");
        }
        catch (Exception ex)
        {
            Log(ex, "打开资源管理器失败，请尝试关闭安全软件（如 360 安全卫士）", LogLevel.Msgbox);
        }
    }

    /// <summary>
    /// 设置剪贴板。将在另一线程运行，且不会抛出异常。
    /// </summary>
    public static void ClipboardSet(string Text, bool ShowSuccessHint = true)
    {
        RunInThread(() =>
        {
            var success = false;

            for (var attempt = 0; attempt <= 5; attempt++)
                try
                {
                    RunInUi(() => Clipboard.SetText(Text));
                    success = true;
                    break;
                }
                catch (Exception ex) when (attempt < 5)
                {
                    Thread.Sleep(20);
                }
                catch (Exception finalEx)
                {
                    Log(finalEx, "剪贴板被占用，文本复制失败", LogLevel.Hint);
                }

            if (success && ShowSuccessHint) RunInUi(() => ModMain.Hint("已成功复制！", ModMain.HintType.Finish));
        });
    }

    /// <summary>
    /// 从剪切板粘贴文件或文件夹
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
                        var thisDest = dest + PathUtils.GetFileNameFromPath(i);
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
                        var thisDest = dest + PathUtils.GetFolderNameFromPath(i);
                        if (Directory.Exists(thisDest))
                        {
                            Log("[System] 已存在同名文件夹：" + thisDest);
                        }
                        else
                        {
                            Directories.CopyDirectoryAsync(i, thisDest).GetAwaiter().GetResult();
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
    /// 获取程序打包资源的输入流。该资源必须声明为 <c>Resource</c> 类型，否则将会报错，<c>Images</c>
    /// 和 <c>Resources</c> 目录已默认声明该类型。
    /// </summary>
    public static Stream GetResourceStream(string path)
    {
        var resourceInfo =
            System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/{path}", UriKind.Absolute));
        return resourceInfo?.Stream;
    }

    #endregion

    /// <summary>
    /// 检查是否拥有某一文件夹的 I/O 权限。如果文件夹不存在，会返回 False。
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
    /// 检查是否拥有某一文件夹的 I/O 权限。如果出错，则抛出异常。
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

    #region UI

    public static void SetLaunchFont(string FontName = null)
    {
        try
        {
            FontFamily TargetFont;
            if (string.IsNullOrEmpty(FontName))
                TargetFont = new FontFamily(new Uri("pack://application:,,,/"),
                    "./Resources/#PCL English, Segoe UI, Microsoft YaHei UI");
            else
                TargetFont = new FontFamily($"{FontName}, Segoe UI, Microsoft YaHei UI");
            System.Windows.Application.Current.Resources["LaunchFontFamily"] = TargetFont;
        }
        catch (Exception ex)
        {
            Log(ex, "设置字体失败", LogLevel.Hint);
        }
    }

    // 边距改变
    /// <summary>
    /// 相对增减控件的左边距。
    /// </summary>
    public static void DeltaLeft(FrameworkElement control, double newValue)
    {
        // 安全性检查
        DebugAssert(!double.IsNaN(newValue));
        DebugAssert(!double.IsInfinity(newValue));

        if (control is Window)
            // 窗口改变
            ((Window)control).Left += newValue;
        else
            // 根据 HorizontalAlignment 改变数值
            switch (control.HorizontalAlignment)
            {
                case HorizontalAlignment.Left:
                case HorizontalAlignment.Stretch:
                    {
                        control.Margin = new Thickness(control.Margin.Left + newValue, control.Margin.Top,
                            control.Margin.Right, control.Margin.Bottom);
                        break;
                    }
                case HorizontalAlignment.Right:
                    {
                        // control.Margin = New Thickness(control.Margin.Left, control.Margin.Top, CType(control.Parent, Object).ActualWidth - control.ActualWidth - newValue, control.Margin.Bottom)
                        control.Margin = new Thickness(control.Margin.Left, control.Margin.Top,
                            control.Margin.Right - newValue, control.Margin.Bottom);
                        break;
                    }

                default:
                    {
                        DebugAssert(false);
                        break;
                    }
            }
    }

    /// <summary>
    /// 设置控件的左边距。（仅针对置左控件）
    /// </summary>
    public static void SetLeft(FrameworkElement control, double newValue)
    {
        DebugAssert(control.HorizontalAlignment == HorizontalAlignment.Left);
        control.Margin = new Thickness(newValue, control.Margin.Top, control.Margin.Right, control.Margin.Bottom);
    }

    /// <summary>
    /// 相对增减控件的上边距。
    /// </summary>
    public static void DeltaTop(FrameworkElement control, double newValue)
    {
        // 安全性检查
        DebugAssert(!double.IsNaN(newValue));
        DebugAssert(!double.IsInfinity(newValue));

        if (control is Window)
            // 窗口改变
            ((Window)control).Top += newValue;
        else
            // 根据 VerticalAlignment 改变数值
            switch (control.VerticalAlignment)
            {
                case VerticalAlignment.Top:
                    {
                        control.Margin = new Thickness(control.Margin.Left, control.Margin.Top + newValue,
                            control.Margin.Right, control.Margin.Bottom);
                        break;
                    }
                case VerticalAlignment.Bottom:
                    {
                        // control.Margin = New Thickness(control.Margin.Left, control.Margin.Top, CType(control.Parent, Object).ActualWidth - control.ActualWidth - newValue, control.Margin.Bottom)
                        control.Margin = new Thickness(control.Margin.Left, control.Margin.Top, control.Margin.Right,
                            control.Margin.Bottom - newValue);
                        break;
                    }

                default:
                    {
                        DebugAssert(false);
                        break;
                    }
            }

        // If Double.IsNaN(newValue) OrElse Double.IsInfinity(newValue) Then Return '安全性检查
        // Select Case control.VerticalAlignment
        // Case VerticalAlignment.Top, VerticalAlignment.Stretch, VerticalAlignment.Center
        // control.Margin = New Thickness(control.Margin.Left, newValue, control.Margin.Right, control.Margin.Bottom)
        // Case VerticalAlignment.Bottom
        // control.Margin = New Thickness(control.Margin.Left, control.Margin.Top, control.Margin.Right, -newValue)
        // 'control.Margin = New Thickness(control.Margin.Left, control.Margin.Top, control.Margin.Right, CType(control.Parent, Object).ActualHeight - control.ActualHeight - newValue)
        // End Select
    }

    /// <summary>
    /// 设置控件的顶边距。（仅针对置上控件）
    /// </summary>
    public static void SetTop(FrameworkElement control, double newValue)
    {
        DebugAssert(control.VerticalAlignment == VerticalAlignment.Top);
        control.Margin = new Thickness(control.Margin.Left, newValue, control.Margin.Right, control.Margin.Bottom);
    }

    // DPI 转换
    public static readonly int DPI = (int)Math.Round(Graphics.FromHwnd(nint.Zero).DpiX);

    /// <summary>
    /// 将经过 DPI 缩放的 WPF 尺寸转化为实际的像素尺寸。
    /// </summary>
    public static double GetPixelSize(double WPFSize)
    {
        return WPFSize / 96d * DPI;
    }

    /// <summary>
    /// 将实际的像素尺寸转化为经过 DPI 缩放的 WPF 尺寸。
    /// </summary>
    public static double GetWPFSize(double PixelSize)
    {
        return PixelSize * 96d / DPI;
    }

    // UI 截图
    /// <summary>
    /// 将某个控件的呈现转换为图片。
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
    /// 将某个控件的模拟呈现转换为图片。
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

    /// <summary>
    /// 将 UI 内容固定为图片并进行 Clear。
    /// </summary>
    public static void ControlFreeze(Panel UI)
    {
        UI.Background = ControlBrush(UI);
        UI.Children.Clear();
    }

    /// <summary>
    /// 将 UI 内容固定为图片并进行 Clear。
    /// </summary>
    public static void ControlFreeze(Border UI)
    {
        UI.Background = ControlBrush(UI);
        UI.Child = null;
    }

    /// <summary>
    /// 将 XML 转换为对应 UI 对象。
    /// </summary>
    public static object GetObjectFromXML(XElement Str)
    {
        return GetObjectFromXML(Str.ToString());
    }

    /// <summary>
    /// 将 XML 转换为对应 UI 对象。
    /// </summary>
    public static object GetObjectFromXML(string Str)
    {
        Str = Str. // 兼容旧版自定义事件写法
            Replace("EventType=\"", "local:CustomEventService.EventType=\"")
            .Replace("EventData=\"", "local:CustomEventService.EventData=\"")
            .Replace("Property=\"EventType\"", "Property=\"local:CustomEventService.EventType\"")
            .Replace("Property=\"EventData\"", "Property=\"local:CustomEventService.EventData\"");
        using (var Stream = new MemoryStream(Encoding.UTF8.GetBytes(Str)))
        {
            // 类型检查
            using (var Reader = new XamlXmlReader(Stream))
            {
                while (Reader.Read())
                {
                    foreach (var BlackListType in new[]
                             {
                                 typeof(WebBrowser), typeof(Frame), typeof(MediaElement), typeof(ObjectDataProvider),
                                 typeof(XamlReader), typeof(Window), typeof(XmlDataProvider)
                             })
                    {
                        if (Reader.Type is not null && BlackListType.IsAssignableFrom(Reader.Type.UnderlyingType))
                            throw new UnauthorizedAccessException($"不允许使用 {BlackListType.Name} 类型。");
                        if (Reader.Value is not null && Conversions.ToBoolean(
                                Operators.ConditionalCompareObjectEqual(Reader.Value, BlackListType.Name, false)))
                            throw new UnauthorizedAccessException($"不允许使用 {BlackListType.Name} 值。");
                    }

                    foreach (var BlackListMember in new[] { "Code", "FactoryMethod", "Static" })
                        if (Reader.Member is not null && (Reader.Member.Name ?? "") == (BlackListMember ?? ""))
                            throw new UnauthorizedAccessException($"不允许使用 {BlackListMember} 成员。");
                }
            }

            // 实际的加载
            Stream.Position = 0L;
            using (var Writer = new StreamWriter(Stream))
            {
                Writer.Write(Str);
                Writer.Flush();
                Stream.Position = 0L;
                return System.Windows.Markup.XamlReader.Load(Stream);
            }
        }
    }

    private static readonly int UiThreadId = Thread.CurrentThread.ManagedThreadId;

    /// <summary>
    /// 当前线程是否为主线程。
    /// </summary>
    public static bool RunInUi()
    {
        return Thread.CurrentThread.ManagedThreadId == UiThreadId;
    }

    #endregion

    #region Debug

    public static bool ModeDebug = false;

    // Log
    public enum LogLevel
    {
        /// <summary>
        /// 不提示，只记录日志。
        /// </summary>
        Normal = 0,

        /// <summary>
        /// 只提示开发者。
        /// </summary>
        Developer = 1,

        /// <summary>
        /// 只提示开发者与调试模式用户。
        /// </summary>
        Debug = 2,

        /// <summary>
        /// 弹出提示所有用户。
        /// </summary>
        Hint = 3,

        /// <summary>
        /// 弹窗，不要求反馈。
        /// </summary>
        Msgbox = 4,

        /// <summary>
        /// 弹窗，要求反馈。
        /// </summary>
        Feedback = 5,

        /// <summary>
        /// 弹出 Windows 原生弹窗，要求反馈。在无法保证 WPF 窗口能正常运行时使用此级别。
        /// 在第二次触发后会直接结束程序。
        /// </summary>
        Critical = 6
    }

    private static bool IsCriticalErrorTriggered;

    /// <summary>
    /// 输出 Log。
    /// </summary>
    /// <param name="Title">如果要求弹窗，指定弹窗的标题。</param>
    public static void Log(string Text, LogLevel Level = LogLevel.Normal, string Title = "出现错误")
    {
        // On Error Resume Next
        // 放在最后会导致无法显示极端错误下的弹窗（如无法写入日志文件）
        // 处理错误会导致再次调用 Log() 导致无限循环

        // 输出日志
        if (new[] { LogLevel.Msgbox, LogLevel.Hint }.Contains(Level))
            LogWrapper.Warn(Text);
        else if (LogLevel.Feedback == Level)
            LogWrapper.Error(Text);
        else if (LogLevel.Critical == Level)
            LogWrapper.Fatal(Text);
        else if (LogLevel.Debug == Level)
            LogWrapper.Debug(Text);
        else if (LogLevel.Developer == Level)
            LogWrapper.Trace(Text);
        else
            LogWrapper.Info(Text);

        if (IsProgramEnded || Level == LogLevel.Normal)
            return;

        // 去除前缀
        Text = Text.RegexReplace(@"\[[^\]]+?\] ", "");

        // 输出提示
        switch (Level)
        {
            case LogLevel.Developer:
                {
                    break;
                }
            case LogLevel.Debug:
                {
                    if (ModeDebug)
                        ModMain.Hint("[调试模式] " + Text, ModMain.HintType.Info, false);
                    break;
                }
            /* TODO ERROR: Skipped EndIfDirectiveTrivia
            #End If
            */
            case LogLevel.Hint:
                {
                    ModMain.Hint(Text, ModMain.HintType.Critical, false);
                    break;
                }
            case LogLevel.Msgbox:
                {
                    ModMain.MyMsgBox(Text, Title, IsWarn: true);
                    break;
                }
            case LogLevel.Feedback:
                {
                    if (CanFeedback(false))
                    {
                        if (ModMain.MyMsgBox(Text + "\r\n" + "\r\n" + "是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！",
                                Title, "反馈", "取消", IsWarn: true) == 1)
                            Feedback(false, true);
                    }
                    else
                    {
                        ModMain.MyMsgBox(Text + "\r\n" + "\r\n" + "将 PCL 更新至最新版或许可以解决这个问题……", Title,
                            IsWarn: true);
                    }

                    break;
                }
            case LogLevel.Critical:
                {
                    if (IsCriticalErrorTriggered)
                    {
                        FormMain.EndProgramForce(Enums.ProcessReturnValues.Exception);
                        return;
                    }

                    IsCriticalErrorTriggered = true;
                    if (CanFeedback(false))
                    {
                        if (Interaction.MsgBox(Text + "\r\n" + "\r\n" + "是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！",
                                (MsgBoxStyle)((int)MsgBoxStyle.Critical + (int)MsgBoxStyle.YesNo), Title) ==
                            MsgBoxResult.Yes)
                            Feedback(false, true);
                    }
                    else
                    {
                        Interaction.MsgBox(Text + "\r\n" + "\r\n" + "将 PCL 更新至最新版或许可以解决这个问题……",
                            MsgBoxStyle.Critical, Title);
                    }

                    break;
                }
        }
    }

    /// <summary>
    /// 输出错误信息。
    /// </summary>
    /// <param name="Desc">错误描述。会在处理时在末尾加入冒号。</param>
    public static void Log(Exception Ex, string Desc, LogLevel Level = LogLevel.Debug, string Title = "出现错误")
    {
        // On Error Resume Next
        if (Ex is ThreadInterruptedException)
            return;

        // 获取错误信息
        var ExFull = Desc + "：" + Ex.Message;

        // 输出日志
        if (new[] { LogLevel.Msgbox, LogLevel.Hint }.Contains(Level))
            LogWrapper.Warn(Ex, Desc);
        else if (LogLevel.Feedback == Level)
            LogWrapper.Error(Ex, Desc);
        else if (LogLevel.Critical == Level)
            LogWrapper.Fatal(Ex, Desc);
        else if (LogLevel.Debug == Level)
            LogWrapper.Debug($"{Desc}:{Ex}");
        else if (LogLevel.Developer == Level)
            LogWrapper.Trace($"{Desc}:{Ex}");
        else
            LogWrapper.Error(Ex, Desc);

        if (IsProgramEnded)
            return;

        if (Ex.GetType() == typeof(Win32Exception))
            ExFull += "\r\n" + "与系统底层交互失败，请尝试重新安装 .NET 8 解决此问题";

        // 输出提示
        switch (Level)
        {
            case LogLevel.Normal:
                {
                    break;
                }
            case LogLevel.Developer:
                {
                    break;
                }
            case LogLevel.Debug:
                {
                    var ExLine = Desc + "：" + Ex;
                    if (ModeDebug)
                        ModMain.Hint("[调试模式] " + ExLine, ModMain.HintType.Info, false);
                    break;
                }
            /* TODO ERROR: Skipped EndIfDirectiveTrivia
            #End If
            */
            case LogLevel.Hint:
                {
                    var ExLine = Desc + "：" + Ex;
                    ModMain.Hint(ExLine, ModMain.HintType.Critical, false);
                    break;
                }
            case LogLevel.Msgbox:
                {
                    ModMain.MyMsgBox(ExFull, Title, IsWarn: true);
                    break;
                }
            case LogLevel.Feedback:
                {
                    if (CanFeedback(false))
                    {
                        if (ModMain.MyMsgBox(ExFull + "\r\n" + "\r\n" + "是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！",
                                Title, "反馈", "取消", IsWarn: true) == 1)
                            Feedback(false, true);
                    }
                    else
                    {
                        ModMain.MyMsgBox(ExFull + "\r\n" + "\r\n" + "将 PCL 更新至最新版或许可以解决这个问题……", Title,
                            IsWarn: true);
                    }

                    break;
                }
            case LogLevel.Critical:
                {
                    if (IsCriticalErrorTriggered)
                    {
                        FormMain.EndProgramForce(Enums.ProcessReturnValues.Exception);
                        return;
                    }

                    IsCriticalErrorTriggered = true;
                    if (CanFeedback(false))
                    {
                        if (Interaction.MsgBox(
                                ExFull + "\r\n" + "\r\n" + "是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！",
                                (MsgBoxStyle)((int)MsgBoxStyle.Critical + (int)MsgBoxStyle.YesNo), Title) ==
                            MsgBoxResult.Yes)
                            Feedback(false, true);
                    }
                    else
                    {
                        Interaction.MsgBox(ExFull + "\r\n" + "\r\n" + "将 PCL 更新至最新版或许可以解决这个问题……",
                            MsgBoxStyle.Critical, Title);
                    }

                    break;
                }
        }
    }

    // 反馈
    public static void Feedback(bool ShowMsgbox = true, bool ForceOpenLog = false)
    {
        // On Error Resume Next
        FeedbackInfo();
        string currentDate;
        currentDate = Strings.Format(DateTime.Now, "yyyy-M-dd");

        if (ForceOpenLog || (ShowMsgbox &&
                             ModMain.MyMsgBox(
                                 "若你在汇报一个 Bug，请点击 打开文件夹 按钮，并上传 Launch-" + currentDate + "-[一串数字].log 中包含错误信息的文件。" +
                                 "\r\n" + "游戏崩溃一般与启动器无关，请不要因为游戏崩溃而提交反馈。", "反馈提交提醒", "打开文件夹", "不需要") ==
                             1)) OpenExplorer(ExePath + @"PCL\Log\");
        OpenWebsite("https://github.com/PCL-Community/PCL2-CE/issues/");
    }

    public static bool CanFeedback(bool ShowHint)
    {
        var stat = ModSecret.GetVersionStatus();
        if (stat != ModSecret.VersionStatus.Latest)
        {
            if (ShowHint)
                if (ModMain.MyMsgBox(
                        stat == ModSecret.VersionStatus.NotLatest
                            ? $"你的 PCL 不是最新版，因此无法提交反馈。{"\r\n"}请在更新后，确认该问题在最新版中依然存在，然后再提交反馈。"
                            : $"你的 PCL 检查更新失败，因此无法提交反馈。{"\r\n"}请连接到互联网，在检查更新后，确认该问题在最新版中依然存在，然后再提交反馈。",
                        "无法提交反馈", stat == ModSecret.VersionStatus.NotLatest ? "更新" : "重新检查更新", "取消") == 1)
                    ModMain.FrmMain.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupUpdate);

            return false;
        }

        return true;
    }

    /// <summary>
    /// 在日志中输出系统诊断信息。
    /// </summary>
    public static void FeedbackInfo()
    {
        try
        {
            // Get system memory info
            var phyRam = KernelInterop.GetPhysicalMemoryBytes();

            // Calculate memory and DPI scale
            var availableMb = phyRam.Available / 1024 / 1024;
            var totalMb = phyRam.Total / 1024 / 1024;
            var dpiScale = Math.Round(DPI / 96.0, 2);

            // Build diagnostic information string
            var info = $"[System] Diagnostic Information:{"\r\n"}" +
                       $"OS: {RuntimeInformation.OSDescription} (32-bit: {Is32BitSystem}){"\r\n"}" +
                       $"Memory: {availableMb} MB / {totalMb} MB{"\r\n"}" +
                       $"DPI: {DPI} ({dpiScale * 100}%){"\r\n"}" +
                       $"MC Folder: {ModMinecraft.McFolderSelected ?? "Nothing"}{"\r\n"}" +
                       $"Executable Path: {ExePath}";

            LogWrapper.Info(info);
        }
        catch (Exception ex)
        {
            // Basic fail-safe to replace "On Error Resume Next"
            LogWrapper.Error(ex, "Failed to collect feedback information");
        }
    }

    // 断言
    public static void DebugAssert(bool Exp)
    {
        if (!Exp)
            throw new Exception("断言命中");
    }

    // 获取当前的堆栈信息
    public static string GetStackTrace()
    {
        var stack = new StackTrace();
        var formated = stack.GetFrames()
            .Skip(1)
            .Select(f => f.GetMethod())
            .Select(methodBase => methodBase.Name +
                                  "(" +
                                  string.Join(", ", methodBase.GetParameters().Select(p => p.ToString())) +
                                  ") - " +
                                  methodBase.Module)
            .ToImmutableArray();
        var res = string.Join("\r\n", formated);
        return res.Replace("\r\n" + "\r\n", "\r\n");
    }

    #endregion
}
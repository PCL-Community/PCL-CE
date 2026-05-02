using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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

        // 下列版本信息由更新器自动修改
        public static readonly string VersionBaseName = Basics.VersionName;
        public static readonly string VersionStandardCode = Basics.Metadata.Version.StandardVersion;
        public static readonly string UpstreamVersion = Basics.Metadata.Version.UpstreamVersion;
        public static readonly string CommitHash = Basics.Metadata.Version.Commit;
        public static readonly string CommitHashShort = Basics.Metadata.Version.CommitDigest;
        public static readonly int VersionCode = Basics.VersionCode;
        /// <summary>
        ///     重命名一个注册表子键。不可用于包含子键的子键。
        /// </summary>
        public static void RenameReg(RegistryKey parentKey, string subKeyName, string newSubKeyName)
        {
            if (parentKey.GetSubKeyNames().Contains(newSubKeyName))
                parentKey.DeleteSubKeyTree(newSubKeyName, false);
            var SourceKey = parentKey.OpenSubKey(subKeyName);
            if (SourceKey == null)
                return; // 没有目标项
            var NewKey = parentKey.CreateSubKey(newSubKeyName);
            if (SourceKey.GetSubKeyNames().Length > 0)
                throw new NotSupportedException("不支持对包含子键的子键进行重命名：" + SourceKey.GetSubKeyNames()[0] + "。");
            foreach (var valueName in SourceKey.GetValueNames())
            {
                var objValue = SourceKey.GetValue(valueName);
                var valKind = SourceKey.GetValueKind(valueName);
                NewKey.SetValue(valueName, objValue, valKind);
            }

            parentKey.DeleteSubKeyTree(subKeyName, false);
        }
        /// <summary>
        ///     是否存在某个注册表键。
        /// </summary>
        public static bool HasReg(string Key)
        {
            return ReadReg(Key, null) is not null;
        }
        // =============================
        // ini
        // =============================

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> IniCache = new();

        /// <summary>
        ///     清除某 ini 文件的运行时缓存。
        /// </summary>
        /// <param name="FileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
        public static void IniClearCache(string FileName)
        {
            if (!FileName.Contains(@":\"))
                FileName = $@"{ExePath}PCL\{FileName}.ini";
            if (IniCache.ContainsKey(FileName))
                IniCache.Remove(FileName, out _);
        }

        /// <summary>
        ///     获取 ini 文件缓存。如果没有，则新读取 ini 文件内容。
        ///     在文件不存在或读取失败时返回 Nothing。
        /// </summary>
        /// <param name="FileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
        private static ConcurrentDictionary<string, string> IniGetContent(string FileName)
        {
            try
            {
                // 还原文件路径
                if (!FileName.Contains(@":\"))
                    FileName = $@"{ExePath}PCL\{FileName}.ini";
                // 检索缓存
                if (IniCache.ContainsKey(FileName))
                    return IniCache[FileName];
                // 读取文件
                if (!File.Exists(FileName))
                    return null;
                var Ini = new ConcurrentDictionary<string, string>();
                foreach (var Line in ReadFile(FileName)
                             .Split("\r\n".ToArray(), StringSplitOptions.RemoveEmptyEntries))
                {
                    var Index = Line.IndexOfF(":");
                    if (Index > 0)
                        Ini[Line.Substring(0, Index)] = Line.Substring(Index + 1); // 可能会有重复键，见 #3616
                }

                IniCache[FileName] = Ini;
                return Ini;
            }
            catch (Exception ex)
            {
                Log(ex, $"生成 ini 文件缓存失败（{FileName}）", LogLevel.Hint);
                return null;
            }
        }

        /// <summary>
        ///     读取 ini 文件。这可能会使用到缓存。
        /// </summary>
        /// <param name="FileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
        /// <param name="Key">键。</param>
        /// <param name="DefaultValue">没有找到键时返回的默认值。</param>
        public static string ReadIni(string FileName, string Key, string DefaultValue = "")
        {
            var Content = IniGetContent(FileName);
            if (Content is null || !Content.ContainsKey(Key))
                return DefaultValue;
            return Content[Key];
        }

        /// <summary>
        ///     判断 ini 文件中是否包含某个键。这可能会使用到缓存。
        /// </summary>
        public static bool HasIniKey(string FileName, string Key)
        {
            var Content = IniGetContent(FileName);
            return Content is not null && Content.ContainsKey(Key);
        }

        /// <summary>
        ///     从 ini 文件中移除某个键。这会更新缓存。
        /// </summary>
        public static void DeleteIniKey(string FileName, string Key)
        {
            WriteIni(FileName, Key, null);
        }

        /// <summary>
        ///     写入 ini 文件，这会更新缓存。
        ///     若 Value 为 Nothing，则删除该键。
        /// </summary>
        /// <param name="FileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
        /// <param name="Key">键。</param>
        /// <param name="Value">值。</param>
        /// <remarks></remarks>
        public static void WriteIni(string FileName, string Key, string Value)
        {
            try
            {
                // 预处理
                if (Key.Contains(":"))
                    throw new Exception($"尝试写入 ini 文件 {FileName} 的键名中包含了冒号：{Key}");
                Key = Key.Replace("\r", "").Replace("\n", "");
                Value = Value?.Replace("\r", "").Replace("\n", "");
                // 防止争用
                lock (WriteIniLock)
                {
                    // 获取目前文件
                    var Content = IniGetContent(FileName);
                    if (Content is null)
                        Content = new ConcurrentDictionary<string, string>();
                    // 更新值
                    if (Value is null)
                    {
                        if (!Content.ContainsKey(Key))
                            return; // 无需处理
                        Content.Remove(Key, out _);
                    }
                    else
                    {
                        if (Content.ContainsKey(Key) && (Content[Key] ?? "") == (Value ?? ""))
                            return; // 无需处理
                        Content[Key] = Value;
                    }

                    // 写入文件
                    var FileContent = new StringBuilder();
                    foreach (var Pair in Content)
                    {
                        FileContent.Append(Pair.Key);
                        FileContent.Append(":");
                        FileContent.Append(Pair.Value);
                        FileContent.Append("\r\n");
                    }

                    if (!FileName.Contains(@":\"))
                        FileName = $@"{ExePath}PCL\{FileName}.ini";
                    WriteFile(FileName, FileContent.ToString());
                }
            }
            catch (Exception ex)
            {
                Log(ex, $"写入文件失败（{FileName} → {Key}:{Value}）", LogLevel.Hint);
            }
        }

        private static readonly object WriteIniLock = new();
        /// <summary>
        ///     获取流的 SHA1，若失败则返回空字符串。
        /// </summary>
        public static string GetAuthSHA1(Stream inputStream)
        {
            try
            {
                return Conversions.ToString(GetHexString(SHA1Provider.Instance.ComputeHash(inputStream)));
            }
            catch (Exception ex)
            {
                Log(ex, "获取流 SHA1 失败");
                return "";
            }
        }
        /// <summary>
        ///     获取 JSON 对象。
        /// </summary>
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
        /// <summary>
        ///     获取字符串哈希值。
        /// </summary>
        public static ulong GetHash(string Str)
        {
            ulong GetHashRet = default;
            GetHashRet = 5381UL;
            for (int i = 0, loopTo = Str.Length - 1; i <= loopTo; i++)
                GetHashRet = (GetHashRet << 5) ^ GetHashRet ^ (ulong)Strings.AscW(Str[i]);
            return GetHashRet ^ 0xA98F501BC684032FUL;
        }

        /// <summary>
        ///     获取字符串 MD5。
        /// </summary>
        public static string GetStringMD5(string Str)
        {
            return Conversions.ToString(GetHexString(MD5Provider.Instance.ComputeHash(Str)));
        }

        /// <summary>
        ///     检查字符串中的字符是否均为 ASCII 字符。
        /// </summary>
        public static bool IsASCII(this string Input)
        {
            return Input.All(c => Strings.AscW(c) < 128);
        }
        // 转义
        /// <summary>
        ///     为字符串进行 XML 转义。
        /// </summary>
        public static string EscapeXML(string Str)
        {
            if (Str.StartsWithF("{"))
                Str = "{}" + Str; // #4187
            return Str.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&apos;")
                .Replace("\"", "&quot;").Replace("\r\n", "&#xa;");
        }
        // 正则
        /// <summary>
        ///     搜索字符串中的所有正则匹配项。
        /// </summary>
        public static List<string> RegexSearch(this string str, string regex, RegexOptions options = RegexOptions.None)
        {
            List<string> RegexSearchRet = default;
            try
            {
                RegexSearchRet = new List<string>();
                var RegexSearchRes = new Regex(regex, options).Matches(str);
                if (RegexSearchRes is null)
                    return RegexSearchRet;
                foreach (Match item in RegexSearchRes)
                    RegexSearchRet.Add(item.Value);
            }
            catch (Exception ex)
            {
                Log(ex, "正则匹配全部项出错");
                return new List<string>();
            }

            return RegexSearchRet;
        }
        
        /// <summary>
        /// 搜索字符串中的所有正则匹配项。
        /// </summary>
        /// <param name="str">要搜索的字符串</param>
        /// <param name="regex">正则表达式对象</param>
        /// <returns>所有匹配项的列表</returns>
        public static List<string> RegexSearch(this string str, Regex regex)
        {
            try
            {
                var result = new List<string>();
                foreach (Match item in regex.Matches(str))
                {
                    result.Add(item.Value);
                }
                return result;
            }
            catch (Exception ex)
            {
                Log(ex, "正则匹配全部项出错");
                return new List<string>();
            }
        }
        
        /// <summary>
        ///     获取字符串中的第一个正则匹配项，若无匹配则返回 Nothing。
        /// </summary>
        public static string RegexSeek(this string str, string regex, RegexOptions options = RegexOptions.None)
        {
            try
            {
                var Result = Regex.Match(str, regex, options).Value;
                return string.IsNullOrEmpty(Result) ? null : Result;
            }
            catch (Exception ex)
            {
                Log(ex, "正则匹配第一项出错");
                return null;
            }
        }

        /// <summary>
        ///     获取字符串中的第一个正则匹配项，若无匹配则返回 Nothing。
        /// </summary>
        public static string RegexSeek(this string str, Regex regex, RegexOptions options = RegexOptions.None)
        {
            try
            {
                var Result = regex.Match(str, (int)options).Value;
                return string.IsNullOrEmpty(Result) ? null : Result;
            }
            catch (Exception ex)
            {
                Log(ex, "正则匹配第一项出错");
                return null;
            }
        }

        /// <summary>
        ///     检查字符串是否匹配某正则模式。
        /// </summary>
        public static bool RegexCheck(this string str, string regex, RegexOptions options = RegexOptions.None)
        {
            try
            {
                return Regex.IsMatch(str, regex, options);
            }
            catch (Exception ex)
            {
                Log(ex, "正则检查出错");
                return false;
            }
        }

        /// <summary>
        ///     进行正则替换，会抛出错误。
        /// </summary>
        public static string RegexReplace(this string AllContents, string SearchRegex, string ReplaceTo,
            RegexOptions options = RegexOptions.None)
        {
            return Regex.Replace(AllContents, SearchRegex, ReplaceTo, options);
        }

        /// <summary>
        ///     对每个正则匹配分别进行替换，会抛出错误。
        /// </summary>
        public static string RegexReplaceEach(this string AllContents, string SearchRegex, MatchEvaluator ReplaceTo,
            RegexOptions options = RegexOptions.None)
        {
            return Regex.Replace(AllContents, SearchRegex, ReplaceTo, options);
        }

        #endregion
        /// <summary>
        ///     前台运行文件。
        /// </summary>
        /// <param name="FileName">文件名。可以为“notepad”等缩写。</param>
        /// <param name="Arguments">运行参数。</param>
        public static void ShellOnly(string FileName, string Arguments = "")
        {
            try
            {
                FileName = ShortenPath(FileName);
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
        ///     前台运行文件并返回返回值。
        /// </summary>
        /// <param name="FileName">文件名。可以为“notepad”等缩写。</param>
        /// <param name="Arguments">运行参数。</param>
        /// <param name="Timeout">等待该程序结束的最长时间（毫秒）。超时会返回 Result.Timeout。</param>
        public static ProcessReturnValues ShellAndGetExitCode(string FileName, string Arguments = "", int Timeout = 1000000)
        {
            try
            {
                using (var Program = new Process())
                {
                    Program.StartInfo.Arguments = Arguments;
                    Program.StartInfo.FileName = FileName;
                    Log("[System] 执行外部命令并等待返回码：" + FileName + " " + Arguments);
                    Program.Start();
                    if (Program.WaitForExit(Timeout)) return (ProcessReturnValues)Program.ExitCode;

                    return ProcessReturnValues.Timeout;
                }
            }
            catch (Exception ex)
            {
                Log(ex, "执行命令失败：" + FileName, LogLevel.Msgbox);
                return ProcessReturnValues.Fail;
            }
        }
        /// <summary>
        ///     在新的工作线程中执行代码。
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
            }) { Name = Name ?? "Runtime New Invoke " + GetUuid() + "#", Priority = Priority };
            th.Start();
            return th;
        }
        /// <summary>
        ///     确保在工作线程中执行代码。
        /// </summary>
        public static void RunInThread(Action Action)
        {
            if (RunInUi())
                RunInNewThread(Action, "Runtime Invoke " + GetUuid() + "#");
            else
                Action();
        }
        // DPI 转换
        public static readonly int DPI = (int)Math.Round(Graphics.FromHwnd(nint.Zero).DpiX);

        /// <summary>
        ///     将经过 DPI 缩放的 WPF 尺寸转化为实际的像素尺寸。
        /// </summary>
        public static double GetPixelSize(double WPFSize)
        {
            return WPFSize / 96d * DPI;
        }

        /// <summary>
        ///     将实际的像素尺寸转化为经过 DPI 缩放的 WPF 尺寸。
        /// </summary>
        public static double GetWPFSize(double PixelSize)
        {
            return PixelSize * 96d / DPI;
        }
        /// <summary>
        ///     将 XML 转换为对应 UI 对象。
        /// </summary>
        public static object GetObjectFromXML(XElement Str)
        {
            return GetObjectFromXML(Str.ToString());
        }

        /// <summary>
        ///     将 XML 转换为对应 UI 对象。
        /// </summary>
        public static object GetObjectFromXML(string Str)
        {
            Str = Str. // 兼容旧版自定义事件写法
                Replace("EventType=\"", "local:CustomEventService.EventType=\"").
                Replace("EventData=\"", "local:CustomEventService.EventData=\"").
                Replace("Property=\"EventType\"", "Property=\"local:CustomEventService.EventType\"").
                Replace("Property=\"EventData\"", "Property=\"local:CustomEventService.EventData\"");
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
        public static string Base64Decode(string Text)
        {
            if (string.IsNullOrWhiteSpace(Text))
                return "";
            var decodedBytes = Convert.FromBase64String(Text);
            return Encoding.UTF8.GetString(decodedBytes);
        }

        public static string Base64Encode(string Text)
        {
            var bytes = Encoding.UTF8.GetBytes(Text);
            return Convert.ToBase64String(bytes);
        }

        public static string Base64Encode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }
    }
}

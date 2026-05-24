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
using System.Text.Json.Nodes;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Utils;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Hash;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
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
    ///     主窗口句柄。
    /// </summary>
    public static nint FrmHandle;

    // 龙猫味石山小记: 用最不靠谱的实现写出能跑的代码 (AppDomain.CurrentDomain.SetupInformation.ApplicationBase 获取到的是当前工作目录而不是可执行文件所在目录)
    /// <summary>
    ///     程序可执行文件所在目录，以“\”结尾。
    /// </summary>
    public static readonly string ExePath = (Basics.ExecutableDirectory.EndsWith(@"\")
        ? Basics.ExecutableDirectory
        : Basics.ExecutableDirectory + @"\");

    /// <summary>
    ///     程序可执行文件完整路径。
    /// </summary>
    public static readonly string ExePathWithName = Basics.ExecutablePath;

    /// <summary>
    ///     程序内嵌图片文件夹路径，以“/”结尾。
    /// </summary>
    public static readonly string PathImage = "pack://application:,,,/Plain Craft Launcher 2;component/Images/";

    /// <summary>
    ///     当前程序的语言。
    /// </summary>
    public static string CurrentLang = "zh_CN";

    /// <summary>
    ///     设置对象。
    /// </summary>
    public static ModSetup Setup = new();

    /// <summary>
    ///     程序的打开计时。
    /// </summary>
    public static long ApplicationStartTick = TimeUtils.GetTimeTick();

    /// <summary>
    ///     程序打开时的时间。
    /// </summary>
    public static DateTime ApplicationOpenTime = DateTime.Now;

    /// <summary>
    ///     识别码。
    /// </summary>
    public static string UniqueAddress = Identify.LauncherId;

    /// <summary>
    ///     程序是否已结束。
    /// </summary>
    public static bool IsProgramEnded = false;

    /// <summary>
    ///     是否为 32 位系统。
    /// </summary>
    public static bool Is32BitSystem = !Environment.Is64BitOperatingSystem;

    /// <summary>
    ///     是否为 ARM64 架构。
    /// </summary>
    public static bool IsArm64System = RuntimeInformation.OSArchitecture == Architecture.Arm64;

    /// <summary>
    ///     是否使用 GBK 编码。
    /// </summary>
    public static bool IsGBKEncoding = Encoding.Default.CodePage == 936;

    /// <summary>
    ///     系统盘盘符，以 \ 结尾。例如 “C:\”。
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
    public static string PathAppdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCL") + @"\";

    /// <summary>
    ///     AppData 中的 PCLCE 配置文件夹路径，以 \ 结尾。
    /// </summary>
    public static string PathAppdataConfig = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                                             (VersionBranchName == "Debug" ? @"\.pclcedebug\" : @"\.pclce\");

    public static string PathHelpFolder = PathTemp + @"CE\Help\";

    #endregion

    #region 自定义类

    /// <summary>
    ///     支持小数与常见类型隐式转换的颜色。
    /// </summary>
    public class MyColor
    {
        public double A = 255d;
        public double B;
        public double G;
        public double R;

        // 构造函数
        public MyColor()
        {
        }

        public MyColor(Color col)
        {
            A = col.A;
            R = col.R;
            G = col.G;
            B = col.B;
        }

        public MyColor(string HexString)
        {
            var StringColor = (Color)ColorConverter.ConvertFromString(HexString);
            A = StringColor.A;
            R = StringColor.R;
            G = StringColor.G;
            B = StringColor.B;
        }

        public MyColor(double newA, MyColor col)
        {
            A = newA;
            R = col.R;
            G = col.G;
            B = col.B;
        }

        public MyColor(double newR, double newG, double newB)
        {
            A = 255d;
            R = newR;
            G = newG;
            B = newB;
        }

        public MyColor(double newA, double newR, double newG, double newB)
        {
            A = newA;
            R = newR;
            G = newG;
            B = newB;
        }

        public MyColor(Brush brush)
        {
            var Color = ((SolidColorBrush)brush).Color;
            A = Color.A;
            R = Color.R;
            G = Color.G;
            B = Color.B;
        }

        public MyColor(SolidColorBrush brush)
        {
            var Color = brush.Color;
            A = Color.A;
            R = Color.R;
            G = Color.G;
            B = Color.B;
        }

        public MyColor(object obj)
        {
            if (obj is null)
            {
                A = 255d;
                R = 255d;
                G = 255d;
                B = 255d;
            }
            else if (obj is SolidColorBrush)
            {
                // 避免反复获取 Color 对象造成性能下降
                var Color = ((SolidColorBrush)obj).Color;
                A = Color.A;
                R = Color.R;
                G = Color.G;
                B = Color.B;
            }
            else
            {
                A = Conversions.ToDouble(((dynamic)obj).A);
                R = Conversions.ToDouble(((dynamic)obj).R);
                G = Conversions.ToDouble(((dynamic)obj).G);
                B = Conversions.ToDouble(((dynamic)obj).B);
            }
        }

        // 类型转换
        public static implicit operator MyColor(string str)
        {
            return new MyColor(str);
        }

        public static implicit operator MyColor(Color col)
        {
            return new MyColor(col);
        }

        public static implicit operator Color(MyColor conv)
        {
            return Color.FromArgb(MathByte(conv.A), MathByte(conv.R), MathByte(conv.G), MathByte(conv.B));
        }

        public static implicit operator System.Drawing.Color(MyColor conv)
        {
            return System.Drawing.Color.FromArgb(MathByte(conv.A), MathByte(conv.R), MathByte(conv.G),
                MathByte(conv.B));
        }

        public static implicit operator MyColor(SolidColorBrush bru)
        {
            return new MyColor(bru.Color);
        }

        public static implicit operator SolidColorBrush(MyColor conv)
        {
            return new SolidColorBrush(Color.FromArgb(MathByte(conv.A), MathByte(conv.R), MathByte(conv.G),
                MathByte(conv.B)));
        }

        public static implicit operator MyColor(Brush bru)
        {
            return new MyColor(bru);
        }

        public static implicit operator Brush(MyColor conv)
        {
            return new SolidColorBrush(Color.FromArgb(MathByte(conv.A), MathByte(conv.R), MathByte(conv.G),
                MathByte(conv.B)));
        }

        // 颜色运算
        public static MyColor operator +(MyColor a, MyColor b)
        {
            return new MyColor { A = a.A + b.A, B = a.B + b.B, G = a.G + b.G, R = a.R + b.R };
        }

        public static MyColor operator -(MyColor a, MyColor b)
        {
            return new MyColor { A = a.A - b.A, B = a.B - b.B, G = a.G - b.G, R = a.R - b.R };
        }

        public static MyColor operator *(MyColor a, double b)
        {
            return new MyColor { A = a.A * b, B = a.B * b, G = a.G * b, R = a.R * b };
        }

        public static MyColor operator /(MyColor a, double b)
        {
            return new MyColor { A = a.A / b, B = a.B / b, G = a.G / b, R = a.R / b };
        }

        public static bool operator ==(MyColor a, MyColor b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;
            return a.A == b.A && a.R == b.R && a.G == b.G && a.B == b.B;
        }

        public static bool operator !=(MyColor a, MyColor b)
        {
            if (a == null && b == null)
                return false;
            if (a == null || b == null)
                return true;
            return !(a.A == b.A && a.R == b.R && a.G == b.G && a.B == b.B);
        }

        // HSL
        public double Hue(double v1, double v2, double vH)
        {
            if (vH < 0d)
                vH += 1d;
            if (vH > 1d)
                vH -= 1d;
            if (vH < 0.16667d)
                return v1 + (v2 - v1) * 6d * vH;
            if (vH < 0.5d)
                return v2;
            if (vH < 0.66667d)
                return v1 + (v2 - v1) * (4d - vH * 6d);
            return v1;
        }

        public MyColor FromHSL(double sH, double sS, double sL)
        {
            if (sS == 0d)
            {
                R = sL * 2.55d;
                G = R;
                B = R;
            }
            else
            {
                var H = sH / 360d;
                var S = sS / 100d;
                var L = sL / 100d;
                S = L < 0.5d ? S * L + L : S * (1.0d - L) + L;
                L = 2d * L - S;
                R = 255d * Hue(L, S, H + 1d / 3d);
                G = 255d * Hue(L, S, H);
                B = 255d * Hue(L, S, H - 1d / 3d);
            }

            A = 255d;
            return this;
        }

        public MyColor FromHSL2(double sH, double sS, double sL)
        {
            if (sS == 0d)
            {
                R = sL * 2.55d;
                G = R;
                B = R;
            }
            else
            {
                // 初始化
                sH = (sH + 3600000d) % 360d;
                var cent = new[]
                {
                    +0.1d, -0.06d, -0.3d, -0.19d, -0.15d, -0.24d, -0.32d, -0.09d, +0.18d, +0.05d, -0.12d, -0.02d, +0.1d,
                    -0.06d
                }; // 0, 30, 60
                // 90, 120, 150
                // 180, 210, 240
                // 270, 300, 330
                // 最后两位与前两位一致，加是变亮，减是变暗
                // 计算色调对应的亮度片区
                var center = sH / 30.0d;
                var intCenter = (int)Math.Round(Math.Floor(center)); // 亮度片区编号
                center = 50d -
                         ((1d - center + intCenter) * cent[intCenter] + (center - intCenter) * cent[intCenter + 1]) *
                         sS;
                // center = 50 + (cent(intCenter) + (center - intCenter) * (cent(intCenter + 1) - cent(intCenter))) * sS
                sL = (sL < center ? sL / center : 1d + (sL - center) / (100d - center)) * 50d;
                FromHSL(sH, sS, sL);
            }

            A = 255d;
            return this;
        }

        public MyColor Alpha(double sA)
        {
            A = sA;
            return this;
        }

        public override string ToString()
        {
            return "(" + A + "," + R + "," + G + "," + B + ")";
        }

        public override bool Equals(object obj)
        {
            return Operators.ConditionalCompareObjectEqual(this, obj, false);
        }
    }

    /// <summary>
    ///     支持负数与浮点数的矩形。
    /// </summary>
    public class MyRect
    {
        // 构造函数
        public MyRect()
        {
        }

        public MyRect(double left, double top, double width, double height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        // 属性
        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
    }

    /// <summary>
    ///     模块加载状态枚举。
    /// </summary>
    public enum LoadState
    {
        Waiting,
        Loading,
        Finished,
        Failed,
        Aborted
    }

    /// <summary>
    ///     执行返回值。
    /// </summary>
    public enum ProcessReturnValues
    {
        /// <summary>
        ///     执行成功，或进程被中断。
        /// </summary>
        Aborted = -1,

        /// <summary>
        ///     执行成功。
        /// </summary>
        Success = 0,

        /// <summary>
        ///     执行失败。
        /// </summary>
        Fail = 1,

        /// <summary>
        ///     执行时出现未经处理的异常。
        /// </summary>
        Exception = 2,

        /// <summary>
        ///     执行超时。
        /// </summary>
        Timeout = 3,

        /// <summary>
        ///     取消执行。可能是由于不满足执行的前置条件。
        /// </summary>
        Cancel = 4,

        /// <summary>
        ///     任务成功完成。
        /// </summary>
        TaskDone = 5
    }

    /// <summary>
    ///     可以使用 Equals 和等号的 List。
    /// </summary>
    public class EqualableList<T> : List<T>
    {
        public override bool Equals(object obj)
        {
            if (obj as List<T> is null)
                // 类型不同
                return false;

            // 类型相同
            var objList = (List<T>)obj;
            if (objList.Count != Count)
                return false;
            for (int i = 0, loopTo = objList.Count - 1; i <= loopTo; i++)
                if (!objList[i].Equals(this[i]))
                    return false;
            return true;
        }

        public static bool operator ==(EqualableList<T> left, EqualableList<T> right)
        {
            return EqualityComparer<EqualableList<T>>.Default.Equals(left, right);
        }

        public static bool operator !=(EqualableList<T> left, EqualableList<T> right)
        {
            return !(left == right);
        }
    }

    #endregion

    #region 数学

    /// <summary>
    ///     2~65 进制的转换。
    /// </summary>
    public static string RadixConvert(string Input, int FromRadix, int ToRadix)
    {
        const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz/+=";
        // 零与负数的处理
        if (string.IsNullOrEmpty(Input))
            return "0";
        var IsNegative = Input.StartsWithF("-");
        if (IsNegative)
            Input = Input.TrimStart('-');
        // 转换为十进制
        var RealNum = 0L;
        var Scale = 1L;
        foreach (var Digit in Input.Reverse().Select(l => Digits.IndexOfF(Conversions.ToString(l))))
        {
            RealNum += Digit * Scale;
            Scale *= FromRadix;
        }

        // 转换为指定进制
        var Result = "";
        while (RealNum > 0L)
        {
            var NewNum = (int)(RealNum % ToRadix);
            RealNum = (long)Math.Round((RealNum - NewNum) / (double)ToRadix);
            Result = Digits[NewNum] + Result;
        }

        // 负数的结束处理与返回
        return (IsNegative ? "-" : "") + Result;
    }

    /// <summary>
    ///     计算二阶贝塞尔曲线。
    /// </summary>
    public static double MathBezier(double x, double x1, double y1, double x2, double y2, double acc = 0.01d)
    {
        if (x <= 0d || double.IsNaN(x)) return 0d;
        if (x >= 1d) return 1d;
        double a, b;
        a = x;
        do
        {
            b = 3 * a * ((0.33333333 + x1 - x2) * a * a + (x2 - 2 * x1) * a + x1);
            a += (x - b) * 0.5;
        } while (!(Math.Abs(b - x) < acc)); // 精度

        return 3 * a * ((0.33333333 + y1 - y2) * a * a + (y2 - 2 * y1) * a + y1);
    }

    /// <summary>
    ///     将一个数字限制为 0~255 的 Byte 值。
    /// </summary>
    public static byte MathByte(double d)
    {
        if (d < 0d)
            d = 0d;
        if (d > 255d)
            d = 255d;
        return (byte)Math.Round(Math.Round(d));
    }

    /// <summary>
    ///     提供 MyColor 类型支持的 Math.Round。
    /// </summary>
    public static MyColor MathRound(MyColor col, int w = 0)
    {
        return new MyColor
            { A = Math.Round(col.A, w), R = Math.Round(col.R, w), G = Math.Round(col.G, w), B = Math.Round(col.B, w) };
    }

    /// <summary>
    ///     获取两数间的百分比。小数点精确到 6 位。
    /// </summary>
    /// <returns></returns>
    public static double MathPercent(double ValueA, double ValueB, double Percent)
    {
        return Math.Round(ValueA * (1d - Percent) + ValueB * Percent, 6); // 解决 Double 计算错误
    }

    /// <summary>
    ///     获取两颜色间的百分比，根据 RGB 计算。小数点精确到 6 位。
    /// </summary>
    public static MyColor MathPercent(MyColor ValueA, MyColor ValueB, double Percent)
    {
        return MathRound(ValueA * (1d - Percent) + ValueB * Percent, 6); // 解决Double计算错误
    }

    /// <summary>
    ///     将数值限定在某个范围内。
    /// </summary>
    public static double MathClamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    /// <summary>
    ///     符号函数。
    /// </summary>
    public static int MathSgn(double Value)
    {
        if (Value == 0d) return 0;

        if (Value > 0d) return 1;

        return -1;
    }

    #endregion

    #region 文件

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
                        throw new Exception(Lang.Text("Common.Error.InvalidJson"), ex);
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

    #region 文本

    public static char vbLQ = Convert.ToChar(8220);
    public static char vbRQ = Convert.ToChar(8221);

    /// <summary>
    ///     返回一个枚举对应的字符串。
    /// </summary>
    /// <param name="EnumData">一个已经实例化的枚举类型。</param>
    public static string GetStringFromEnum(Enum EnumData)
    {
        return Enum.GetName(EnumData.GetType(), EnumData);
    }

    /// <summary>
    ///     将文件大小转化为适合的文本形式，如“1.28 M”。
    /// </summary>
    /// <param name="FileSize">以字节为单位的大小表示。</param>
    public static string GetString(long FileSize)
    {
        return ByteStream.GetReadableLength(FileSize, provider: Lang.Culture);
    }

    /// <summary>
    ///     获取 JSON 对象。
    /// </summary>
    public static JsonNode GetJson(string Data)
    {
        try
        {
            return JsonNode.Parse(Data)!;
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
    ///     将第一个字符转换为大写，其余字符转换为小写。
    /// </summary>
    public static string Capitalize(this string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;
        return word.Substring(0, 1).ToUpperInvariant() + word.Substring(1).ToLowerInvariant();
    }

    /// <summary>
    ///     将字符串统一至某个长度，过短则以 Code 将其右侧填充，过长则截取靠左的指定长度。
    /// </summary>
    public static string StrFill(string Str, string Code, byte Length)
    {
        if (Str.Length > Length)
            return Strings.Mid(Str, 1, Length);
        return Strings.Mid(Str.PadRight(Length, Conversions.ToChar(Code)), Str.Length + 1) + Str;
    }

    /// <summary>
    ///     将一个小数显示为固定的小数点后位数形式，将向零取整。
    ///     如 12 保留 2 位则输出 12.00，而 95.678 保留 2 位则输出 95.67。
    /// </summary>
    public static string StrFillNum(double Num, int Length)
    {
        return Lang.Number(Num, $"F{Length}");
    }

    /// <summary>
    ///     移除字符串首尾的标点符号、回车，以及括号中、冒号后的补充说明内容。
    /// </summary>
    public static object StrTrim(string Str, bool RemoveQuote = true)
    {
        if (RemoveQuote)
            Str = Str.Split("（")[0].Split("：")[0].Split("(")[0].Split(":")[0];
        return Str.Trim('.', '。', '！', ' ', '!', '?', '？', Conversions.ToChar("\r"),
            Conversions.ToChar("\n"));
    }

    /// <summary>
    ///     连接字符串。
    /// </summary>
    public static string Join(this IEnumerable List, string Split)
    {
        var Builder = new StringBuilder();
        var IsFirst = true;
        foreach (var Element in List)
        {
            if (IsFirst)
                IsFirst = false;
            else
                Builder.Append(Split);
            if (Element is not null)
                Builder.Append(Element);
        }

        return Builder.ToString();
    }

    /// <summary>
    ///     分割字符串。
    /// </summary>
    public static string[] Split(this string FullStr, string SplitStr)
    {
        if (SplitStr.Length == 1) return FullStr.Split(SplitStr[0]);

        return FullStr.Split(new[] { SplitStr }, StringSplitOptions.None);
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

    /// <summary>
    ///     获取在子字符串第一次出现之前的部分，例如对 2024/11/08 拆切 / 会得到 2024。
    ///     如果未找到子字符串则不裁切。
    /// </summary>
    public static string BeforeFirst(this string Str, string Text, bool IgnoreCase = false)
    {
        var Pos = string.IsNullOrEmpty(Text) ? -1 : Str.IndexOfF(Text, IgnoreCase);
        if (Pos >= 0) return Str.Substring(0, Pos);

        return Str;
    }

    /// <summary>
    ///     获取在子字符串最后一次出现之前的部分，例如对 2024/11/08 拆切 / 会得到 2024/11。
    ///     如果未找到子字符串则不裁切。
    /// </summary>
    public static string BeforeLast(this string Str, string Text, bool IgnoreCase = false)
    {
        var Pos = string.IsNullOrEmpty(Text) ? -1 : Str.LastIndexOfF(Text, IgnoreCase);
        if (Pos >= 0) return Str.Substring(0, Pos);

        return Str;
    }

    /// <summary>
    ///     获取在子字符串第一次出现之后的部分，例如对 2024/11/08 拆切 / 会得到 11/08。
    ///     如果未找到子字符串则不裁切。
    /// </summary>
    public static string AfterFirst(this string Str, string Text, bool IgnoreCase = false)
    {
        var Pos = string.IsNullOrEmpty(Text) ? -1 : Str.IndexOfF(Text, IgnoreCase);
        if (Pos >= 0) return Str.Substring(Pos + Text.Length);

        return Str;
    }

    /// <summary>
    ///     获取在子字符串最后一次出现之后的部分，例如对 2024/11/08 拆切 / 会得到 08。
    ///     如果未找到子字符串则不裁切。
    /// </summary>
    public static string AfterLast(this string Str, string Text, bool IgnoreCase = false)
    {
        var Pos = string.IsNullOrEmpty(Text) ? -1 : Str.LastIndexOfF(Text, IgnoreCase);
        if (Pos >= 0) return Str.Substring(Pos + Text.Length);

        return Str;
    }

    /// <summary>
    ///     获取处于两个子字符串之间的部分，裁切尽可能多的内容。
    ///     等效于 AfterLast 后接 BeforeFirst。
    ///     如果未找到子字符串则不裁切。
    /// </summary>
    public static string Between(this string Str, string After, string Before, bool IgnoreCase = false)
    {
        var StartPos = string.IsNullOrEmpty(After) ? -1 : Str.LastIndexOfF(After, IgnoreCase);
        if (StartPos >= 0)
            StartPos += After.Length;
        else
            StartPos = 0;
        var EndPos = string.IsNullOrEmpty(Before) ? -1 : Str.IndexOfF(Before, StartPos, IgnoreCase);
        if (EndPos >= 0) return Str.Substring(StartPos, EndPos - StartPos);

        if (StartPos > 0) return Str.Substring(StartPos);

        return Str;
    }

    /// <summary>
    ///     高速的 StartsWith。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool StartsWithF(this string Str, string Prefix, bool IgnoreCase = false)
    {
        return Str.StartsWith(Prefix, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    ///     高速的 EndsWith。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EndsWithF(this string Str, string Suffix, bool IgnoreCase = false)
    {
        return Str.EndsWith(Suffix, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    ///     支持可变大小写判断的 Contains。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsF(this string Str, string SubStr, bool IgnoreCase = false)
    {
        return Str.IndexOf(SubStr, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    ///     高速的 IndexOf。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfF(this string Str, string SubStr, bool IgnoreCase = false)
    {
        return Str.IndexOf(SubStr, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    ///     高速的 IndexOf。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfF(this string Str, string SubStr, int StartIndex, bool IgnoreCase = false)
    {
        return Str.IndexOf(SubStr, StartIndex,
            IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    ///     高速的 LastIndexOf。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LastIndexOfF(this string Str, string SubStr, bool IgnoreCase = false)
    {
        return Str.LastIndexOf(SubStr, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    ///     高速的 LastIndexOf。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LastIndexOfF(this string Str, string SubStr, int StartIndex, bool IgnoreCase = false)
    {
        return Str.LastIndexOf(SubStr, StartIndex,
            IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    ///     不会报错的 Val。
    ///     如果输入有误，返回 0。
    /// </summary>
    public static double Val(object Str)
    {
        try
        {
            return Str is "&" ? 0d : Conversion.Val(Str);
        }
        catch
        {
            return 0d;
        }
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

    /// <summary>
    ///     为字符串进行 Like 关键字转义。
    /// </summary>
    public static string EscapeLikePattern(string input)
    {
        var sb = new StringBuilder();
        foreach (var c in input)
            switch (c)
            {
                case '[':
                case ']':
                case '*':
                case '?':
                case '#':
                {
                    sb.Append('[').Append(c).Append(']');
                    break;
                }

                default:
                {
                    sb.Append(c);
                    break;
                }
            }

        return sb.ToString();
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

    #region 搜索

    /// <summary>
    ///     获取搜索文本的相似度。
    /// </summary>
    /// <param name="Source">被搜索的长内容。</param>
    /// <param name="Query">用户输入的搜索文本。</param>
    private static double SearchSimilarity(string Source, string Query)
    {
        var qp = 0;
        var lenSum = 0d;
        Source = Source.ToLower().Replace(" ", "");
        Query = Query.ToLower().Replace(" ", "");
        var sourceLength = Source.Length;
        var queryLength = Query.Length; // 用于计算最后因数的长度缓存
        while (qp < queryLength)
        {
            // 对 qp 作为开始位置计算
            var sp = 0;
            var lenMax = 0;
            var spMax = 0;
            // 查找以 qp 为头的最大子串
            while (sp < Source.Length)
            {
                // 对每个 sp 作为开始位置计算最大子串
                var len = 0;
                while (qp + len < queryLength && sp + len < Source.Length && Source[sp + len] == Query[qp + len])
                    len += 1;
                // 存储 len
                if (len > lenMax)
                {
                    lenMax = len;
                    spMax = sp;
                }

                // 根据结果增加 sp
                sp += Math.Max(1, len);
            }

            if (lenMax > 0)
            {
                Source = Source.Substring(0, spMax) +
                         (Source.Count() > spMax + lenMax
                             ? Source.Substring(spMax + lenMax)
                             : string.Empty); // 将源中的对应字段替换空
                // 存储 lenSum
                var IncWeight = Math.Pow(1.4d, 3 + lenMax) - 3.6d; // 根据长度加成
                IncWeight *= 1d + 0.3d * Math.Max(0, 3 - Math.Abs(qp - spMax)); // 根据位置加成
                lenSum += IncWeight;
            }

            // 根据结果增加 qp
            qp += Math.Max(1, lenMax);
        }

        // 计算结果：重复字段量 × 源长度影响比例
        return lenSum / queryLength * (3d / Math.Pow(sourceLength + 15, 0.5d)) *
               (queryLength <= 2 ? 3 - queryLength : 1);
    }

    /// <summary>
    ///     获取多段文本加权后的相似度。
    /// </summary>
    private static double SearchSimilarityWeighted(List<SearchSource> source, string query)
    {
        var totalWeight = 0d;
        var sum = 0d;
        foreach (var Pair in source)
        {
            if (Pair.Aliases.Any())
                sum += Pair.Aliases.Max(a => SearchSimilarity(a, query)) * Pair.Weight;
            totalWeight += Pair.Weight;
        }

        return sum / totalWeight;
    }

    /// <summary>
    ///     用于搜索的项目。
    /// </summary>
    public class SearchEntry<T>
    {
        /// <summary>
        ///     是否完全匹配。
        /// </summary>
        public bool AbsoluteRight;

        /// <summary>
        ///     该项目对应的源数据。
        /// </summary>
        public T Item;

        /// <summary>
        ///     该项目用于搜索的文本源。
        ///     在搜索时，会对每个文本源单独加权，但单个文本源内的多个别名只取最高的一个的相似度。
        /// </summary>
        public List<SearchSource> SearchSource;

        /// <summary>
        ///     相似度。
        /// </summary>
        public double Similarity;
    }

    /// <summary>
    ///     单个用于搜索的文本源。
    /// </summary>
    public class SearchSource
    {
        public string[] Aliases;
        public double Weight;

        public SearchSource(string[] aliases, double weight = 1)
        {
            Aliases = aliases;
            Weight = weight;
        }

        public SearchSource(string text, double weight = 1)
        {
            Aliases = new[] { text };
            Weight = weight;
        }
    }

    /// <summary>
    ///     进行多段文本加权搜索，获取相似度较高的数项结果。
    /// </summary>
    /// <param name="MaxBlurCount">返回的最大模糊结果数。</param>
    /// <param name="MinBlurSimilarity">返回结果要求的最低相似度。</param>
    public static List<SearchEntry<T>> Search<T>(List<SearchEntry<T>> Entries, string Query, int MaxBlurCount = 5,
        double MinBlurSimilarity = 0.1d)
    {
        var ResultList = new List<SearchEntry<T>>();

        if (Entries is null || !Entries.Any()) return ResultList;

        // Preprocess query into parts
        var queryParts = Query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (queryParts.Length == 0)
        {
            ResultList.AddRange(Entries);
            return ResultList;
        }

        // Precompute query parts in lowercase for case-insensitive comparison
        var queryPartsLower = queryParts.Select(q => q.ToLower()).ToArray();

        // Process each entry to compute similarity and absolute match status
        foreach (var Entry in Entries)
        {
            Entry.Similarity = SearchSimilarityWeighted(Entry.SearchSource, Query);

            // Preprocess search source keys: remove spaces and convert to lowercase
            var processedSources = Entry.SearchSource.Select(s =>
            {
                for (var i = 0; i < s.Aliases.Length; i++)
                    s.Aliases[i] = s.Aliases[i].Replace(" ", "").ToLower();
                return s.Aliases;
            }).ToList();

            // Check if all query parts are matched exactly by at least one source
            var isAbsoluteRight = true;
            foreach (var qp in queryPartsLower)
            {
                var found = false;
                foreach (var ps in processedSources)
                    if (ps.Any(p => p.Contains(qp)))
                    {
                        found = true;
                        break;
                    }

                if (!found)
                {
                    isAbsoluteRight = false;
                    break;
                }
            }

            Entry.AbsoluteRight = isAbsoluteRight;
        }

        // Sort by absolute match (descending), then by similarity (descending)
        var sortedEntries = Entries.OrderByDescending(e => e.AbsoluteRight).ThenByDescending(e => e.Similarity)
            .ToList();

        // Build the final result list
        var blurCount = 0;
        foreach (var Entry in sortedEntries)
            if (Entry.AbsoluteRight)
            {
                ResultList.Add(Entry);
            }
            else
            {
                if (Entry.Similarity < MinBlurSimilarity || blurCount >= MaxBlurCount) break;
                ResultList.Add(Entry);
                blurCount += 1;
            }

        return ResultList;
    }

    #endregion

    #region 系统

    public static bool IsUtf8CodePage()
    {
        return Encoding.Default.CodePage == 65001;
    }

    /// <summary>
    ///     线程安全的 List。
    ///     通过在 For Each 循环中使用一个浅表副本规避多线程操作或移除自身导致的异常。
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
    ///     指示接取到这个异常的函数进行重试。
    /// </summary>
    public class RestartException : Exception
    {
    }

    /// <summary>
    ///     指示用户手动取消了操作，或用户已知晓操作被取消的原因。
    /// </summary>
    public class CancelledException : Exception
    {
    }

    /// <summary>
    ///     判断对象是否为某个泛型类型的实例。
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
    ///     获取一个全程序内不会重复的数字（伪 Uuid）。
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
    ///     将元素与 List 的混合体拆分为元素组。
    /// </summary>
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
    ///     数组去重。
    /// </summary>
    public static List<T> Distinct<T>(this ICollection<T> Arr, ComparisonBoolean<T> IsEqual)
    {
        var ResultArray = new List<T>();
        for (int i = 0, loopTo = Arr.Count - 1; i <= loopTo; i++)
        {
            for (int ii = i + 1, loopTo1 = Arr.Count - 1; ii <= loopTo1; ii++)
                if (IsEqual(Arr.ElementAtOrDefault(i), Arr.ElementAtOrDefault(ii)))
                    goto NextElement;
            ResultArray.Add(Arr.ElementAtOrDefault(i));
            NextElement: ;
        }

        return ResultArray;
    }

    /// <summary>
    ///     对集合的每个元素执行指定操作。
    /// </summary>
    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> Collection, Action<T> Action)
    {
        foreach (var Item in Collection)
            Action(Item);
        return Collection;
    }

    /// <summary>
    ///     用于储存 RaiseByMouse 的 EventArgs。
    /// </summary>
    public sealed class RouteEventArgs : EventArgs
    {
        public bool Handled = false;
        public bool RaiseByMouse;

        public RouteEventArgs(bool RaiseByMouse = false)
        {
            this.RaiseByMouse = RaiseByMouse;
        }
    }

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
    ///     确保在 UI 线程中执行代码。
    ///     如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
    ///     为防止线程互锁，请仅在开始加载动画、从 UI 获取输入时使用！
    /// </summary>
    public static Output RunInUiWait<Output>(Func<Output> Action)
    {
        if (RunInUi()) return Action();

        return System.Windows.Application.Current.Dispatcher.Invoke(Action);
    }

    /// <summary>
    ///     确保在 UI 线程中执行代码。
    ///     如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
    ///     为防止线程互锁，请仅在开始加载动画、从 UI 获取输入时使用！
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
    ///     确保在 UI 线程中执行代码，代码按触发顺序执行。
    ///     如果当前并非 UI 线程，也不阻断当前线程的执行。
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
    ///     确保在工作线程中执行代码。
    /// </summary>
    public static void RunInThread(Action Action)
    {
        if (RunInUi())
            RunInNewThread(Action, "Runtime Invoke " + GetUuid() + "#");
        else
            Action();
    }

    /// <summary>
    ///     使用优化的归并排序算法进行稳定排序。
    /// </summary>
    /// <param name="SortRule">传入两个对象，若第一个对象应该排在前面，则返回 True。</param>
    public static List<T> Sort<T>(this IList<T> List, ComparisonBoolean<T> SortRule)
    {
        // 创建原列表的副本以避免修改原始列表
        var tempList = new List<T>(List);
        if (tempList.Count <= 1)
            return tempList;

        // 使用归并排序核心算法
        MergeSort_Sort(ref tempList, 0, tempList.Count - 1, SortRule);
        return tempList;
    }

    private static void MergeSort_Sort<T>(ref List<T> array, int left, int right, ComparisonBoolean<T> comparator)
    {
        if (left >= right)
            return;

        var mid = (left + right) / 2;
        MergeSort_Sort(ref array, left, mid, comparator);
        MergeSort_Sort(ref array, mid + 1, right, comparator);
        MergeSort_Merge(ref array, left, mid, right, comparator);
    }

    private static void MergeSort_Merge<T>(ref List<T> array, int left, int mid, int right,
        ComparisonBoolean<T> comparator)
    {
        var leftArray = new List<T>();
        var rightArray = new List<T>();

        for (int i = left, loopTo = mid; i <= loopTo; i++)
            leftArray.Add(array[i]);

        for (int j = mid + 1, loopTo1 = right; j <= loopTo1; j++)
            rightArray.Add(array[j]);

        var leftPtr = 0;
        var rightPtr = 0;
        var current = left;

        while (leftPtr < leftArray.Count && rightPtr < rightArray.Count)
        {
            // 保持稳定性的关键比较逻辑：当相等时优先取左数组元素
            if (comparator(leftArray[leftPtr], rightArray[rightPtr]))
            {
                array[current] = leftArray[leftPtr];
                leftPtr += 1;
            }
            else
            {
                array[current] = rightArray[rightPtr];
                rightPtr += 1;
            }

            current += 1;
        }

        while (leftPtr < leftArray.Count)
        {
            array[current] = leftArray[leftPtr];
            leftPtr += 1;
            current += 1;
        }

        while (rightPtr < rightArray.Count)
        {
            array[current] = rightArray[rightPtr];
            rightPtr += 1;
            current += 1;
        }
    }

    public delegate bool ComparisonBoolean<T>(T Left, T Right);

    /// <summary>
    ///     返回列表的浅表副本。
    /// </summary>
    public static IList<T> Clone<T>(this IList<T> list)
    {
        return new List<T>(list);
    }

    /// <summary>
    ///     尝试从字典中获取某项，如果该项不存在，则返回默认值。
    /// </summary>
    public static TValue GetOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> Dict, TKey Key,
        TValue DefaultValue = default)
    {
        if (Dict.ContainsKey(Key)) return Dict[Key];

        return DefaultValue;
    }

    /// <summary>
    ///     将某项添加到以列表作为值的字典中。
    /// </summary>
    public static void AddToList<TKey, TValue>(this Dictionary<TKey, List<TValue>> Dict, TKey Key, TValue Value)
    {
        if (Dict.ContainsKey(Key))
            Dict[Key].Add(Value);
        else
            Dict.Add(Key, new List<TValue> { Value });
    }

    /// <summary>
    ///     获取程序启动参数。
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
    ///     打开网页。
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
    ///     打开 explorer。
    ///     若不以 \ 结尾，则将视作文件路径，打开并选中此文件。
    /// </summary>
    public static void OpenExplorer(string Location)
    {
        try
        {
            Location = ShortenPath(Location.Replace("/", @"\").Trim(' ', '"'));
            Log("[System] 正在打开资源管理器：" + Location);
            if (Location.EndsWithF(@"\"))
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
    ///     设置剪贴板。将在另一线程运行，且不会抛出异常。
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

    #endregion

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

    #region UI

    public static void SetLaunchFont(string FontName = null)
    {
        try
        {
            LocalizationFontService.ApplyLaunchFont(FontName, LocalizationService.CurrentLanguage);
        }
        catch (Exception ex)
        {
            Log(ex, "设置字体失败", LogLevel.Hint);
        }
    }

    // 边距改变
    /// <summary>
    ///     相对增减控件的左边距。
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
    ///     设置控件的左边距。（仅针对置左控件）
    /// </summary>
    public static void SetLeft(FrameworkElement control, double newValue)
    {
        DebugAssert(control.HorizontalAlignment == HorizontalAlignment.Left);
        control.Margin = new Thickness(newValue, control.Margin.Top, control.Margin.Right, control.Margin.Bottom);
    }

    /// <summary>
    ///     相对增减控件的上边距。
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
    }

    /// <summary>
    ///     设置控件的顶边距。（仅针对置上控件）
    /// </summary>
    public static void SetTop(FrameworkElement control, double newValue)
    {
        DebugAssert(control.VerticalAlignment == VerticalAlignment.Top);
        control.Margin = new Thickness(control.Margin.Left, newValue, control.Margin.Right, control.Margin.Bottom);
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

    /// <summary>
    ///     将 UI 内容固定为图片并进行 Clear。
    /// </summary>
    public static void ControlFreeze(Panel UI)
    {
        UI.Background = ControlBrush(UI);
        UI.Children.Clear();
    }

    /// <summary>
    ///     将 UI 内容固定为图片并进行 Clear。
    /// </summary>
    public static void ControlFreeze(Border UI)
    {
        UI.Background = ControlBrush(UI);
        UI.Child = null;
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

    private static readonly int UiThreadId = Thread.CurrentThread.ManagedThreadId;

    /// <summary>
    ///     当前线程是否为主线程。
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
        ///     不提示，只记录日志。
        /// </summary>
        Normal = 0,

        /// <summary>
        ///     只提示开发者。
        /// </summary>
        Developer = 1,

        /// <summary>
        ///     只提示开发者与调试模式用户。
        /// </summary>
        Debug = 2,

        /// <summary>
        ///     弹出提示所有用户。
        /// </summary>
        Hint = 3,

        /// <summary>
        ///     弹窗，不要求反馈。
        /// </summary>
        Msgbox = 4,

        /// <summary>
        ///     弹窗，要求反馈。
        /// </summary>
        Feedback = 5,

        /// <summary>
        ///     弹出 Windows 原生弹窗，要求反馈。在无法保证 WPF 窗口能正常运行时使用此级别。
        ///     在第二次触发后会直接结束程序。
        /// </summary>
        Critical = 6
    }

    private static bool IsCriticalErrorTriggered;

    /// <summary>
    ///     输出 Log。
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
                            Title, "反馈", Lang.Text("Common.Action.Cancel"), IsWarn: true) == 1)
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
                    FormMain.EndProgramForce(ProcessReturnValues.Exception);
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
    ///     输出错误信息。
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
                            Title, "反馈", Lang.Text("Common.Action.Cancel"), IsWarn: true) == 1)
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
                    FormMain.EndProgramForce(ProcessReturnValues.Exception);
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

    // 反馈
    public static void Feedback(bool ShowMsgbox = true, bool ForceOpenLog = false)
    {
        // On Error Resume Next
        FeedbackInfo();
        string currentDate;
        currentDate = DateTime.Now.ToString("yyyy-M-dd", CultureInfo.InvariantCulture);

        if (ForceOpenLog || (ShowMsgbox &&
                             ModMain.MyMsgBox(
                                 "若你在汇报一个 Bug，请点击 打开文件夹 按钮，并上传 Launch-" + currentDate + "-[一串数字].log 中包含错误信息的文件。" +
                                 "\r\n" + "游戏崩溃一般与启动器无关，请不要因为游戏崩溃而提交反馈。", "反馈提交提醒", Lang.Text("Common.Action.OpenFolder"), "不需要") ==
                             1)) OpenExplorer(ExePath + @"PCL\Log\");
        OpenWebsite("https://github.com/PCL-Community/PCL2-CE/issues/");
    }

    public static bool CanFeedback(bool ShowHint)
    {
        var stat = UpdateManager.GetVersionStatus();
        if (stat != UpdateEnums.VersionStatus.Latest)
        {
            if (ShowHint)
                if (ModMain.MyMsgBox(
                        stat == UpdateEnums.VersionStatus.NotLatest
                            ? $"你的 PCL 不是最新版，因此无法提交反馈。{"\r\n"}请在更新后，确认该问题在最新版中依然存在，然后再提交反馈。"
                            : $"你的 PCL 检查更新失败，因此无法提交反馈。{"\r\n"}请连接到互联网，在检查更新后，确认该问题在最新版中依然存在，然后再提交反馈。",
                        "无法提交反馈", stat == UpdateEnums.VersionStatus.NotLatest ? "更新" : "重新检查更新", Lang.Text("Common.Action.Cancel")) == 1)
                    ModMain.FrmMain.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupUpdate);

            return false;
        }

        return true;
    }

    /// <summary>
    ///     在日志中输出系统诊断信息。
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
        var Stack = new StackTrace();
        return Stack.GetFrames().Skip(1).Select(f => f.GetMethod())
            .Select(f => f.Name + "(" + f.GetParameters().Select(p => p.ToString()).ToList().Join(", ") + ") - " +
                         f.Module).ToList().Join("\r\n")
            .Replace("\r\n" + "\r\n", "\r\n");
    }

    #endregion
}

#region WPF

/// <summary>
///     对数据绑定进行加法运算，使用参数决定加数。
/// </summary>
public class AdditionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
            return 0;
        double before;
        if (!double.TryParse(value.ToString(), out before))
            return 0;
        var scale = 1d;
        if (parameter is not null)
            double.TryParse(parameter.ToString(), out scale);
        return before + scale;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
            return Binding.DoNothing;
        double before;
        if (!double.TryParse(value.ToString(), out before))
            return Binding.DoNothing;
        var scale = 1d;
        if (parameter is not null)
            double.TryParse(parameter.ToString(), out scale);
        if (scale == 0d)
            return Binding.DoNothing;
        return before - scale;
    }
}

/// <summary>
///     对数据绑定进行乘法运算，使用参数决定乘数。
/// </summary>
public class MultiplicationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
            return 0;
        double before;
        if (!double.TryParse(value.ToString(), out before))
            return 0;
        var scale = 1d;
        if (parameter is not null)
            double.TryParse(parameter.ToString(), out scale);
        return before * scale;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
            return Binding.DoNothing;
        double before;
        if (!double.TryParse(value.ToString(), out before))
            return Binding.DoNothing;
        var scale = 1d;
        if (parameter is not null)
            double.TryParse(parameter.ToString(), out scale);
        if (scale == 0d)
            return Binding.DoNothing;
        return before / scale;
    }
}

/// <summary>
///     将取反的 Boolean 绑定到 Visibility。
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
            return Visibility.Visible;
        bool boolValue;
        return bool.TryParse(value.ToString(), out boolValue)
            ? boolValue ? Visibility.Collapsed : Visibility.Visible
            : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
            return false;
        return value is Visibility
            ? Operators.ConditionalCompareObjectNotEqual(value, Visibility.Visible, false)
            : false;
    }
}

/// <summary>
///     将 Boolean 取反。
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
            return false;
        bool boolValue;
        return bool.TryParse(value.ToString(), out boolValue) ? !boolValue : false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return false;

        if (bool.TryParse(value.ToString(), out var result)) return !result;

        return false;
    }
}

#endregion
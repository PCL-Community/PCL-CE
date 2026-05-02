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
        /// <summary>
        ///     程序内嵌图片文件夹路径，以“/”结尾。
        /// </summary>
        public static readonly string PathImage = "pack://application:,,,/Plain Craft Launcher 2;component/Images/";

        /// <summary>
        ///     当前程序的语言。
        /// </summary>
        public static string Lang = "zh_CN";

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
        public static string UniqueAddress = ModSecret.SecretGetUniqueAddress();

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
            return ByteStream.GetReadableLength(FileSize);
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
            string StrFillNumRet = default;
            Num = Math.Round(Num, Length, MidpointRounding.AwayFromZero);
            StrFillNumRet = Num.ToString();
            if (!StrFillNumRet.Contains("."))
                return (StrFillNumRet + ".").PadRight(StrFillNumRet.Length + 1 + Length, '0');
            return StrFillNumRet.PadRight(StrFillNumRet.Split(".")[0].Length + 1 + Length, '0');
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

        #endregion

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

        #endregion

        // 获取当前的堆栈信息
        public static string GetStackTrace()
        {
            var Stack = new StackTrace();
            return Stack.GetFrames().Skip(1).Select(f => f.GetMethod())
                .Select(f => f.Name + "(" + f.GetParameters().Select(p => p.ToString()).ToList().Join(", ") + ") - " +
                             f.Module).ToList().Join("\r\n")
                .Replace("\r\n" + "\r\n", "\r\n");
        }
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
}

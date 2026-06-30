using System.Windows.Media;
using PCL.Core.Utils;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace PCL;

public static partial class ModBase
{
    #region 声明

    // 下列版本信息由更新器自动修改
    public static readonly string VersionBaseName = LauncherEnvironment.VersionBaseName;
    public static readonly string VersionStandardCode = LauncherEnvironment.VersionStandardCode;
    public static readonly string UpstreamVersion = LauncherEnvironment.UpstreamVersion;
    public static readonly string CommitHash = LauncherEnvironment.CommitHash;
    public static readonly string CommitHashShort = LauncherEnvironment.CommitHashShort;
    public static readonly int VersionCode = LauncherEnvironment.VersionCode;

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
    public static nint frmHandle
    {
        get => LauncherEnvironment.MainWindowHandle;
        set => LauncherEnvironment.MainWindowHandle = value;
    }

    /// <summary>
    ///     程序可执行文件所在目录，以“\”结尾。
    /// </summary>
    public static string exePath => LauncherPaths.ExecutableDirectoryWithSlash;

    /// <summary>
    ///     程序内嵌图片文件夹路径，以“/”结尾。
    /// </summary>
    public static string pathImage => LauncherPaths.ImageBaseUri;

    /// <summary>
    ///     当前程序的语言。
    /// </summary>
    public static string currentLang
    {
        get => LauncherEnvironment.CurrentLanguage;
        set => LauncherEnvironment.CurrentLanguage = value;
    }

    /// <summary>
    ///     设置对象。
    /// </summary>
    public static ModSetup setup
    {
        get => LauncherEnvironment.Setup;
        set => LauncherEnvironment.Setup = value;
    }

    /// <summary>
    ///     程序的打开计时。
    /// </summary>
    public static long applicationStartTick
    {
        get => LauncherRuntime.ApplicationStartTick;
        set => LauncherRuntime.ApplicationStartTick = value;
    }

    /// <summary>
    ///     程序打开时的时间。
    /// </summary>
    public static DateTime applicationOpenTime
    {
        get => LauncherRuntime.ApplicationOpenTime;
        set => LauncherRuntime.ApplicationOpenTime = value;
    }

    /// <summary>
    ///     程序是否已结束。
    /// </summary>
    public static bool isProgramEnded
    {
        get => LauncherRuntime.IsProgramEnded;
        set => LauncherRuntime.IsProgramEnded = value;
    }

    /// <summary>
    ///     程序的缓存文件夹路径，以 \ 结尾。
    /// </summary>
    public static string pathTemp
    {
        get => LauncherPaths.TempWithSlash;
        set => LauncherPaths.TempWithSlash = value;
    }

    /// <summary>
    ///     AppData 中的 PCL 文件夹路径，以 \ 结尾。
    /// </summary>
    public static string pathAppdata
    {
        get => LauncherPaths.LegacyAppDataWithSlash;
        set => LauncherPaths.LegacyAppDataWithSlash = value;
    }

    /// <summary>
    ///     AppData 中的 PCLCE 配置文件夹路径，以 \ 结尾。
    /// </summary>
    public static string pathAppdataConfig
    {
        get => LauncherPaths.SharedConfigWithSlash;
        set => LauncherPaths.SharedConfigWithSlash = value;
    }

    #endregion

    #region 自定义类

    /// <summary>
    ///     支持小数与常见类型隐式转换的颜色。
    /// </summary>
    public class MyColor
    {
        public double a = 255d;
        public double b;
        public double g;
        public double r;

        // 构造函数
        public MyColor()
        {
        }

        public MyColor(Color col)
        {
            a = col.A;
            r = col.R;
            g = col.G;
            b = col.B;
        }

        public MyColor(string hexString)
        {
            var stringColor = (Color)ColorConverter.ConvertFromString(hexString);
            a = stringColor.A;
            r = stringColor.R;
            g = stringColor.G;
            b = stringColor.B;
        }

        public MyColor(double newA, MyColor col)
        {
            a = newA;
            r = col.r;
            g = col.g;
            b = col.b;
        }

        public MyColor(double newR, double newG, double newB)
        {
            a = 255d;
            r = newR;
            g = newG;
            b = newB;
        }

        public MyColor(double newA, double newR, double newG, double newB)
        {
            a = newA;
            r = newR;
            g = newG;
            b = newB;
        }

        public MyColor(Brush brush)
        {
            var color = ((SolidColorBrush)brush).Color;
            a = color.A;
            r = color.R;
            g = color.G;
            b = color.B;
        }

        public MyColor(SolidColorBrush brush)
        {
            var color = brush.Color;
            a = color.A;
            r = color.R;
            g = color.G;
            b = color.B;
        }

        public MyColor(object obj)
        {
            switch (obj)
            {
                case null:
                    a = 255d;
                    r = 255d;
                    g = 255d;
                    b = 255d;
                    break;
                case SolidColorBrush brush:
                {
                    // 避免反复获取 Color 对象造成性能下降
                    var color = brush.Color;
                    a = color.A;
                    r = color.R;
                    g = color.G;
                    b = color.B;
                    break;
                }
                default:
                    a = Convert.ToDouble(((dynamic)obj).A);
                    r = Convert.ToDouble(((dynamic)obj).R);
                    g = Convert.ToDouble(((dynamic)obj).G);
                    b = Convert.ToDouble(((dynamic)obj).B);
                    break;
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
            return Color.FromArgb(
                MathByte(conv.a),
                MathByte(conv.r),
                MathByte(conv.g),
                MathByte(conv.b));
        }

        public static implicit operator System.Drawing.Color(MyColor conv)
        {
            return System.Drawing.Color.FromArgb(
                MathByte(conv.a),
                MathByte(conv.r),
                MathByte(conv.g),
                MathByte(conv.b));
        }

        public static implicit operator MyColor(SolidColorBrush bru)
        {
            return new MyColor(bru.Color);
        }

        public static implicit operator SolidColorBrush(MyColor conv)
        {
            return new SolidColorBrush(Color.FromArgb(
                MathByte(conv.a),
                MathByte(conv.r),
                MathByte(conv.g),
                MathByte(conv.b)));
        }

        public static implicit operator MyColor(Brush bru)
        {
            return new MyColor(bru);
        }

        public static implicit operator Brush(MyColor conv)
        {
            return new SolidColorBrush(Color.FromArgb(
                MathByte(conv.a),
                MathByte(conv.r),
                MathByte(conv.g),
                MathByte(conv.b)));
        }

        // 颜色运算
        public static MyColor operator +(MyColor a, MyColor b)
        {
            return new MyColor { a = a.a + b.a, b = a.b + b.b, g = a.g + b.g, r = a.r + b.r };
        }

        public static MyColor operator -(MyColor a, MyColor b)
        {
            return new MyColor { a = a.a - b.a, b = a.b - b.b, g = a.g - b.g, r = a.r - b.r };
        }

        public static MyColor operator *(MyColor a, double b)
        {
            return new MyColor { a = a.a * b, b = a.b * b, g = a.g * b, r = a.r * b };
        }

        public static MyColor operator /(MyColor a, double b)
        {
            return new MyColor { a = a.a / b, b = a.b / b, g = a.g / b, r = a.r / b };
        }

        public static bool operator ==(MyColor a, MyColor b)
        {
            if (a is null && b is null)
                return true;
            if (a is null || b is null)
                return false;
            return a.a == b.a && a.r == b.r && a.g == b.g && a.b == b.b;
        }

        public static bool operator !=(MyColor a, MyColor b)
        {
            if (a is null && b is null)
                return false;
            if (a is null || b is null)
                return true;
            return !(a.a == b.a && a.r == b.r && a.g == b.g && a.b == b.b);
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
                r = sL * 2.55d;
                g = r;
                b = r;
            }
            else
            {
                var h = sH / 360d;
                var s = sS / 100d;
                var l = sL / 100d;
                s = l < 0.5d ? s * l + l : s * (1.0d - l) + l;
                l = 2d * l - s;
                r = 255d * Hue(l, s, h + 1d / 3d);
                g = 255d * Hue(l, s, h);
                b = 255d * Hue(l, s, h - 1d / 3d);
            }

            a = 255d;
            return this;
        }

        public MyColor FromHSL2(double sH, double sS, double sL)
        {
            if (sS == 0d)
            {
                r = sL * 2.55d;
                g = r;
                b = r;
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

            a = 255d;
            return this;
        }

        public MyColor Alpha(double sA)
        {
            a = sA;
            return this;
        }

        public override string ToString()
        {
            return "(" + a + "," + r + "," + g + "," + b + ")";
        }

        public override bool Equals(object obj)
        {
            return obj is MyColor other && a == other.a && r == other.r && g == other.g && b == other.b;
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
    public static string RadixConvert(string input, int fromRadix, int toRadix)
    {
        return RadixUtils.Convert(input, fromRadix, toRadix);
    }

    /// <summary>
    ///     计算二阶贝塞尔曲线。
    /// </summary>
    public static double MathBezier(
        double x,
        double x1,
        double y1,
        double x2,
        double y2,
        double acc = 0.01d)
    {
        return InterpolationUtils.CubicBezierY(x, x1, y1, x2, y2, acc);
    }

    /// <summary>
    ///     将一个数字限制为 0~255 的 Byte 值。
    /// </summary>
    public static byte MathByte(double d)
    {
        return NumberUtils.ClampToByte(d);
    }

    /// <summary>
    ///     提供 MyColor 类型支持的 Math.Round。
    /// </summary>
    public static MyColor MathRound(MyColor col, int w = 0)
    {
        return new MyColor
        {
            a = Math.Round(col.a, w),
            r = Math.Round(col.r, w),
            g = Math.Round(col.g, w),
            b = Math.Round(col.b, w)
        };
    }

    /// <summary>
    ///     获取两数间的百分比。小数点精确到 6 位。
    /// </summary>
    /// <returns></returns>
    public static double MathPercent(double valueA, double valueB, double percent)
    {
        return NumberUtils.Lerp(valueA, valueB, percent);
    }

    /// <summary>
    ///     获取两颜色间的百分比，根据 RGB 计算。小数点精确到 6 位。
    /// </summary>
    public static MyColor MathPercent(MyColor valueA, MyColor valueB, double percent)
    {
        return MathRound(valueA * (1d - percent) + valueB * percent, 6); // 解决Double计算错误
    }

    /// <summary>
    ///     将数值限定在某个范围内。
    /// </summary>
    public static double MathClamp(double value, double min, double max)
    {
        return NumberUtils.Clamp(value, min, max);
    }

    /// <summary>
    ///     符号函数。
    /// </summary>
    public static int MathSgn(double value)
    {
        return NumberUtils.Sign(value);
    }

    #endregion
}
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace PCL;

public static partial class Common
{
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
    ///     支持小数与常见类型隐式转换的颜色。
    /// </summary>
    public class MyColor
    {
        public double a = 255d;
        public double b;
        public double g;
        public double r;

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

        public MyColor(
            double newA,
            double newR,
            double newG,
            double newB)
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
            return new SolidColorBrush(
                Color.FromArgb(
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
            return new SolidColorBrush(
                Color.FromArgb(
                    MathByte(conv.a),
                    MathByte(conv.r),
                    MathByte(conv.g),
                    MathByte(conv.b)));
        }

        public static MyColor operator +(MyColor a, MyColor b)
        {
            return new MyColor
            {
                a = a.a + b.a,
                b = a.b + b.b,
                g = a.g + b.g,
                r = a.r + b.r
            };
        }

        public static MyColor operator -(MyColor a, MyColor b)
        {
            return new MyColor
            {
                a = a.a - b.a,
                b = a.b - b.b,
                g = a.g - b.g,
                r = a.r - b.r
            };
        }

        public static MyColor operator *(MyColor a, double b)
        {
            return new MyColor
            {
                a = a.a * b,
                b = a.b * b,
                g = a.g * b,
                r = a.r * b
            };
        }

        public static MyColor operator /(MyColor a, double b)
        {
            return new MyColor
            {
                a = a.a / b,
                b = a.b / b,
                g = a.g / b,
                r = a.r / b
            };
        }

        public static bool operator ==(MyColor? a, MyColor? b)
        {
            if (a is null && b is null)
                return true;
            if (a is null || b is null)
                return false;
            return a.a == b.a && a.r == b.r && a.g == b.g && a.b == b.b;
        }

        public static bool operator !=(MyColor? a, MyColor? b)
        {
            if (a is null && b is null)
                return false;
            if (a is null || b is null)
                return true;
            return !(a.a == b.a && a.r == b.r && a.g == b.g && a.b == b.b);
        }

        public static double Hue(double v1, double v2, double vH)
        {
            if (vH < 0d)
                vH += 1d;
            if (vH > 1d)
                vH -= 1d;
            return vH switch
            {
                < 0.16667d => v1 + (v2 - v1) * 6d * vH,
                < 0.5d => v2,
                < 0.66667d => v1 + (v2 - v1) * (4d - vH * 6d),
                _ => v1
            };
        }

        public MyColor FromHsl(double sH, double sS, double sL)
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

        public MyColor FromHsl2(double sH, double sS, double sL)
        {
            if (sS == 0d)
            {
                r = sL * 2.55d;
                g = r;
                b = r;
            }
            else
            {
                sH = (sH + 3600000d) % 360d;
                var cent = new[]
                {
                    +0.1d,
                    -0.06d,
                    -0.3d,
                    -0.19d,
                    -0.15d,
                    -0.24d,
                    -0.32d,
                    -0.09d,
                    +0.18d,
                    +0.05d,
                    -0.12d,
                    -0.02d,
                    +0.1d,
                    -0.06d
                };
                var center = sH / 30.0d;
                var intCenter = (int)Math.Round(Math.Floor(center));
                center = 50d
                    - ((1d - center + intCenter) * cent[intCenter]
                        + (center - intCenter) * cent[intCenter + 1])
                    * sS;
                sL = (sL < center
                        ? sL / center
                        : 1d + (sL - center) / (100d - center))
                    * 50d;
                FromHsl(sH, sS, sL);
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

        public override bool Equals(object? obj)
        {
            return obj is MyColor other
                && a == other.a
                && r == other.r
                && g == other.g
                && b == other.b;
        }
    }

    /// <summary>
    ///     支持负数与浮点数的矩形。
    /// </summary>
    public class MyRect(
        double left,
        double top,
        double width,
        double height)
    {
        public double Width { get; set; } = width;
        public double Height { get; set; } = height;
        public double Left { get; set; } = left;
        public double Top { get; set; } = top;
    }

    /// <summary>
    ///     可以使用 Equals 和等号的 List。
    /// </summary>
    public class EqualableList<T> : List<T>
    {
        public override bool Equals(object? obj)
        {
            if (obj is not List<T> objList)
                return false;

            if (objList.Count != Count)
                return false;
            for (int i = 0, loopTo = objList.Count - 1; i <= loopTo; i++)
                if (!EqualityComparer<T>.Default.Equals(objList[i], this[i]))
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
}
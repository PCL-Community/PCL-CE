using System.Collections;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace PCL;

/// <summary>
///     模块加载状态。
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
///     支持负数与浮点数的矩形。
/// </summary>
public class MyRect
{
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

    public double Width { get; set; }
    public double Height { get; set; }
    public double Left { get; set; }
    public double Top { get; set; }
}

/// <summary>
///     支持小数与常见 WPF / Drawing 类型互转的颜色。
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

    public MyColor(Color color)
    {
        a = color.A;
        r = color.R;
        g = color.G;
        b = color.B;
    }

    public MyColor(string hexString)
    {
        var color = (Color)ColorConverter.ConvertFromString(hexString);
        a = color.A;
        r = color.R;
        g = color.G;
        b = color.B;
    }

    public MyColor(double newA, MyColor color)
    {
        a = newA;
        r = color.r;
        g = color.g;
        b = color.b;
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

    public MyColor(SolidColorBrush brush) : this(brush.Color)
    {
    }

    public MyColor(object? obj)
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
                var color = brush.Color;
                a = color.A;
                r = color.R;
                g = color.G;
                b = color.B;
                break;
            default:
                a = Convert.ToDouble(((dynamic)obj).A);
                r = Convert.ToDouble(((dynamic)obj).R);
                g = Convert.ToDouble(((dynamic)obj).G);
                b = Convert.ToDouble(((dynamic)obj).B);
                break;
        }
    }

    public static implicit operator MyColor(string value)
    {
        return new MyColor(value);
    }

    public static implicit operator MyColor(Color value)
    {
        return new MyColor(value);
    }

    public static implicit operator MyColor(SolidColorBrush value)
    {
        return new MyColor(value.Color);
    }

    public static implicit operator MyColor(Brush value)
    {
        return new MyColor(value);
    }

    public static implicit operator Color(MyColor value)
    {
        return Color.FromArgb(
            NumberUtils.ClampToByte(value.a),
            NumberUtils.ClampToByte(value.r),
            NumberUtils.ClampToByte(value.g),
            NumberUtils.ClampToByte(value.b));
    }

    public static implicit operator System.Drawing.Color(MyColor value)
    {
        return System.Drawing.Color.FromArgb(
            NumberUtils.ClampToByte(value.a),
            NumberUtils.ClampToByte(value.r),
            NumberUtils.ClampToByte(value.g),
            NumberUtils.ClampToByte(value.b));
    }

    public static implicit operator SolidColorBrush(MyColor value)
    {
        return new SolidColorBrush((Color)value);
    }

    public static implicit operator Brush(MyColor value)
    {
        return new SolidColorBrush((Color)value);
    }

    public static MyColor operator +(MyColor left, MyColor right)
    {
        return new MyColor
        {
            a = left.a + right.a,
            r = left.r + right.r,
            g = left.g + right.g,
            b = left.b + right.b
        };
    }

    public static MyColor operator -(MyColor left, MyColor right)
    {
        return new MyColor
        {
            a = left.a - right.a,
            r = left.r - right.r,
            g = left.g - right.g,
            b = left.b - right.b
        };
    }

    public static MyColor operator *(MyColor left, double right)
    {
        return new MyColor
        {
            a = left.a * right,
            r = left.r * right,
            g = left.g * right,
            b = left.b * right
        };
    }

    public static MyColor operator /(MyColor left, double right)
    {
        return new MyColor
        {
            a = left.a / right,
            r = left.r / right,
            g = left.g / right,
            b = left.b / right
        };
    }

    public static bool operator ==(MyColor? left, MyColor? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.a == right.a && left.r == right.r && left.g == right.g && left.b == right.b;
    }

    public static bool operator !=(MyColor? left, MyColor? right)
    {
        return !(left == right);
    }

    private static double Hue(double value1, double value2, double hue)
    {
        if (hue < 0d) hue += 1d;
        if (hue > 1d) hue -= 1d;
        if (hue < 0.16667d) return value1 + (value2 - value1) * 6d * hue;
        if (hue < 0.5d) return value2;
        if (hue < 0.66667d) return value1 + (value2 - value1) * (4d - hue * 6d);
        return value1;
    }

    public MyColor FromHSL(double sourceHue, double sourceSaturation, double sourceLightness)
    {
        if (sourceSaturation == 0d)
        {
            r = sourceLightness * 2.55d;
            g = r;
            b = r;
        }
        else
        {
            var hue = sourceHue / 360d;
            var saturation = sourceSaturation / 100d;
            var lightness = sourceLightness / 100d;
            saturation = lightness < 0.5d
                ? saturation * lightness + lightness
                : saturation * (1d - lightness) + lightness;
            lightness = 2d * lightness - saturation;
            r = 255d * Hue(lightness, saturation, hue + 1d / 3d);
            g = 255d * Hue(lightness, saturation, hue);
            b = 255d * Hue(lightness, saturation, hue - 1d / 3d);
        }

        a = 255d;
        return this;
    }

    public MyColor FromHSL2(double sourceHue, double sourceSaturation, double sourceLightness)
    {
        if (sourceSaturation == 0d)
        {
            r = sourceLightness * 2.55d;
            g = r;
            b = r;
        }
        else
        {
            sourceHue = (sourceHue + 3600000d) % 360d;
            var centers = new[]
            {
                +0.1d, -0.06d, -0.3d, -0.19d, -0.15d, -0.24d, -0.32d, -0.09d,
                +0.18d, +0.05d, -0.12d, -0.02d, +0.1d, -0.06d
            };
            var centerIndex = sourceHue / 30d;
            var lowerIndex = (int)Math.Round(Math.Floor(centerIndex));
            var visualCenter = 50d -
                               ((1d - centerIndex + lowerIndex) * centers[lowerIndex] +
                                (centerIndex - lowerIndex) * centers[lowerIndex + 1]) * sourceSaturation;
            sourceLightness = (sourceLightness < visualCenter
                ? sourceLightness / visualCenter
                : 1d + (sourceLightness - visualCenter) / (100d - visualCenter)) * 50d;
            FromHSL(sourceHue, sourceSaturation, sourceLightness);
        }

        a = 255d;
        return this;
    }

    public MyColor Alpha(double value)
    {
        a = value;
        return this;
    }

    public override string ToString()
    {
        return $"({a},{r},{g},{b})";
    }

    public override bool Equals(object? obj)
    {
        return obj is MyColor other && this == other;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(a, r, g, b);
    }
}

/// <summary>
///     可以使用 Equals 和等号的 List。
/// </summary>
public class EqualableList<T> : List<T>
{
    public override bool Equals(object? obj)
    {
        if (obj is not List<T> other || other.Count != Count)
            return false;

        for (var i = 0; i < other.Count; i++)
            if (!EqualityComparer<T>.Default.Equals(other[i], this[i]))
                return false;
        return true;
    }

    public static bool operator ==(EqualableList<T>? left, EqualableList<T>? right)
    {
        return EqualityComparer<EqualableList<T>>.Default.Equals(left, right);
    }

    public static bool operator !=(EqualableList<T>? left, EqualableList<T>? right)
    {
        return !(left == right);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in this)
            hash.Add(item);
        return hash.ToHashCode();
    }
}

/// <summary>
///     线程安全的 List。枚举时返回浅表快照。
/// </summary>
public class SafeList<T> : IEnumerable<T>, IDisposable, ICollection<T>
{
    private readonly List<T> _items;
    private readonly ReaderWriterLockSlim _lock = new();

    public SafeList()
    {
        _items = [];
    }

    public SafeList(IEnumerable<T> data)
    {
        _items = new List<T>(data);
    }

    public T this[int index]
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _items[index];
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        set
        {
            _lock.EnterWriteLock();
            try
            {
                _items[index] = value;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }

    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _items.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public bool IsReadOnly => false;

    public void Add(T item)
    {
        _lock.EnterWriteLock();
        try
        {
            _items.Add(item);
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
            return _items.Remove(item);
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
            _items.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool Contains(T item)
    {
        _lock.EnterReadLock();
        try
        {
            return _items.Contains(item);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        ToList().CopyTo(array, arrayIndex);
    }

    public void Dispose()
    {
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ToList().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void RemoveAt(int index)
    {
        _lock.EnterWriteLock();
        try
        {
            _items.RemoveAt(index);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public List<T> ToList()
    {
        _lock.EnterReadLock();
        try
        {
            return new List<T>(_items);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}

/// <summary>
///     文件校验规则。
/// </summary>
public class FileChecker
{
    public long actualSize = -1;
    public bool canUseExistsFile = true;
    public string? hash;
    public bool isJson;
    public long minSize = -1;

    public FileChecker(long minSize = -1, long actualSize = -1, string? hash = null,
        bool canUseExistsFile = true, bool isJson = false)
    {
        this.actualSize = actualSize;
        this.minSize = minSize;
        this.hash = hash;
        this.canUseExistsFile = canUseExistsFile;
        this.isJson = isJson;
    }

    public string Check(string localPath)
    {
        return LegacyFileFacade.CheckFile(localPath, minSize, actualSize, hash ?? string.Empty, isJson);
    }
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
public class CancelledException : Exception;

/// <summary>
///     用于储存 RaiseByMouse 的 EventArgs。
/// </summary>
public sealed class RouteEventArgs(bool raiseByMouse = false) : EventArgs
{
    public bool handled = false;
    public bool raiseByMouse = raiseByMouse;
}
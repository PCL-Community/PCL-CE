using System.Collections;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Media;
using Microsoft.VisualBasic.CompilerServices;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace PCL;

public static partial class ModBase
{
    public const string VersionBranchName = LauncherEnvironment.VersionBranchName;
    public static string PathImage => LauncherEnvironment.PathImage;
    public static ModSetup Setup => LauncherEnvironment.Setup;
    public static string UniqueAddress
    {
        get => LauncherEnvironment.UniqueAddress;
        set => LauncherEnvironment.UniqueAddress = value;
    }

    /// <summary>
    ///     支持小数与常见类型隐式转换的颜色。
    /// </summary>
    public class MyColor
    {
        public double A = 255d;
        public double B;
        public double G;
        public double R;

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

        public static implicit operator MyColor(string str) => new(str);
        public static implicit operator MyColor(Color col) => new(col);
        public static implicit operator Color(MyColor conv) => Color.FromArgb(LauncherBaseTypes.MathByte(conv.A), LauncherBaseTypes.MathByte(conv.R), LauncherBaseTypes.MathByte(conv.G), LauncherBaseTypes.MathByte(conv.B));
        public static implicit operator System.Drawing.Color(MyColor conv) => System.Drawing.Color.FromArgb(LauncherBaseTypes.MathByte(conv.A), LauncherBaseTypes.MathByte(conv.R), LauncherBaseTypes.MathByte(conv.G), LauncherBaseTypes.MathByte(conv.B));
        public static implicit operator MyColor(SolidColorBrush bru) => new(bru.Color);
        public static implicit operator SolidColorBrush(MyColor conv) => new(Color.FromArgb(LauncherBaseTypes.MathByte(conv.A), LauncherBaseTypes.MathByte(conv.R), LauncherBaseTypes.MathByte(conv.G), LauncherBaseTypes.MathByte(conv.B)));
        public static implicit operator MyColor(Brush bru) => new(bru);
        public static implicit operator Brush(MyColor conv) => new SolidColorBrush(Color.FromArgb(LauncherBaseTypes.MathByte(conv.A), LauncherBaseTypes.MathByte(conv.R), LauncherBaseTypes.MathByte(conv.G), LauncherBaseTypes.MathByte(conv.B)));

        public static MyColor operator +(MyColor a, MyColor b) => new() { A = a.A + b.A, B = a.B + b.B, G = a.G + b.G, R = a.R + b.R };
        public static MyColor operator -(MyColor a, MyColor b) => new() { A = a.A - b.A, B = a.B - b.B, G = a.G - b.G, R = a.R - b.R };
        public static MyColor operator *(MyColor a, double b) => new() { A = a.A * b, B = a.B * b, G = a.G * b, R = a.R * b };
        public static MyColor operator /(MyColor a, double b) => new() { A = a.A / b, B = a.B / b, G = a.G / b, R = a.R / b };

        public static bool operator ==(MyColor a, MyColor b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.A == b.A && a.R == b.R && a.G == b.G && a.B == b.B;
        }

        public static bool operator !=(MyColor a, MyColor b)
        {
            if (a == null && b == null) return false;
            if (a == null || b == null) return true;
            return !(a.A == b.A && a.R == b.R && a.G == b.G && a.B == b.B);
        }

        public override string ToString() => "(" + A + "," + R + "," + G + "," + B + ")";
        public override bool Equals(object obj) => Operators.ConditionalCompareObjectEqual(this, obj, false);
    }

    public static double MathClamp(double value, double min, double max) => LauncherText.MathClamp(value, min, max);

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
}

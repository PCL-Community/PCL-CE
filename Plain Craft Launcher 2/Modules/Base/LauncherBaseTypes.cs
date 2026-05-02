using System;
using System.Collections.Generic;
using Microsoft.VisualBasic.CompilerServices;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace PCL;

/// <summary>
/// Owns launcher custom value types and enums.
/// </summary>
public static class LauncherBaseTypes
{
    public static byte MathByte(double d)
    {
        if (d < 0d)
            d = 0d;
        if (d > 255d)
            d = 255d;
        return (byte)Math.Round(Math.Round(d));
    }
}

/// <summary>
/// 支持小数与常见类型隐式转换的颜色。
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

    public double Hue(double v1, double v2, double vH)
    {
        if (vH < 0d) vH += 1d;
        if (vH > 1d) vH -= 1d;
        if (vH < 0.16667d) return v1 + (v2 - v1) * 6d * vH;
        if (vH < 0.5d) return v2;
        if (vH < 0.66667d) return v1 + (v2 - v1) * (4d - vH * 6d);
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
            sH = (sH + 3600000d) % 360d;
            var cent = new[] { +0.1d, -0.06d, -0.3d, -0.19d, -0.15d, -0.24d, -0.32d, -0.09d, +0.18d, +0.05d, -0.12d, -0.02d, +0.1d, -0.06d };
            var center = sH / 30.0d;
            var intCenter = (int)Math.Round(Math.Floor(center));
            center = 50d - ((1d - center + intCenter) * cent[intCenter] + (center - intCenter) * cent[intCenter + 1]) * sS;
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

    public override string ToString() => "(" + A + "," + R + "," + G + "," + B + ")";
    public override bool Equals(object obj) => Operators.ConditionalCompareObjectEqual(this, obj, false);
}

/// <summary>
/// 支持负数与浮点数的矩形。
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

public enum LoadState
{
    Waiting,
    Loading,
    Finished,
    Failed,
    Aborted
}

public enum ProcessReturnValues
{
    Aborted = -1,
    Success = 0,
    Fail = 1,
    Exception = 2,
    Timeout = 3,
    Cancel = 4,
    TaskDone = 5
}

public class EqualableList<T> : List<T>
{
    public override bool Equals(object obj)
    {
        if (obj as List<T> is null) return false;
        var objList = (List<T>)obj;
        if (objList.Count != Count) return false;
        for (int i = 0, loopTo = objList.Count - 1; i <= loopTo; i++)
            if (!objList[i].Equals(this[i]))
                return false;
        return true;
    }

    public static bool operator ==(EqualableList<T> left, EqualableList<T> right) => EqualityComparer<EqualableList<T>>.Default.Equals(left, right);
    public static bool operator !=(EqualableList<T> left, EqualableList<T> right) => !(left == right);
}

public class SearchEntry<T>
{
    public bool AbsoluteRight;
    public T Item;
    public List<SearchSource> SearchSource;
    public double Similarity;
}

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
        Aliases = [text];
        Weight = weight;
    }
}

public sealed class RouteEventArgs(bool RaiseByMouse = false) : EventArgs
{
    public bool Handled = false;
    public bool RaiseByMouse = RaiseByMouse;
}

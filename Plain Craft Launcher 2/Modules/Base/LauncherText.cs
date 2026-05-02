using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using PCL.Core.IO;

namespace PCL;

/// <summary>
/// Owns regex, text, string, numeric formatting, culture, and collection text helpers.
/// </summary>
public static class LauncherText
{
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
        foreach (var Digit in Input.Reverse().Select(l => Digits.IndexOfF(l.ToString())))
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
    ///     检查字符串中的字符是否均为 ASCII 字符。
    /// </summary>
    public static bool IsASCII(this string Input)
    {
        return Input.All(c => Strings.AscW(c) < 128);
    }

    public static List<string> RegexSearch(this string str, string regex, RegexOptions options = RegexOptions.None)
    {
        try
        {
            var result = new List<string>();
            var matches = new Regex(regex, options).Matches(str);
            foreach (Match item in matches)
                result.Add(item.Value);
            return result;
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "正则匹配全部项出错");
            return new List<string>();
        }
    }

    public static List<string> RegexSearch(this string str, Regex regex)
    {
        try
        {
            var result = new List<string>();
            foreach (Match item in regex.Matches(str))
                result.Add(item.Value);
            return result;
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "正则匹配全部项出错");
            return new List<string>();
        }
    }

    public static string RegexSeek(this string str, string regex, RegexOptions options = RegexOptions.None)
    {
        try
        {
            var result = Regex.Match(str, regex, options).Value;
            return string.IsNullOrEmpty(result) ? null : result;
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "正则匹配第一项出错");
            return null;
        }
    }

    public static string RegexSeek(this string str, Regex regex, RegexOptions options = RegexOptions.None)
    {
        try
        {
            var result = regex.Match(str, (int)options).Value;
            return string.IsNullOrEmpty(result) ? null : result;
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "正则匹配第一项出错");
            return null;
        }
    }

    public static bool RegexCheck(this string str, string regex, RegexOptions options = RegexOptions.None)
    {
        try
        {
            return Regex.IsMatch(str, regex, options);
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "正则检查出错");
            return false;
        }
    }

    public static string RegexReplace(this string AllContents, string SearchRegex, string ReplaceTo,
        RegexOptions options = RegexOptions.None) => Regex.Replace(AllContents, SearchRegex, ReplaceTo, options);

    public static string RegexReplaceEach(this string AllContents, string SearchRegex, MatchEvaluator ReplaceTo,
        RegexOptions options = RegexOptions.None) => Regex.Replace(AllContents, SearchRegex, ReplaceTo, options);
}

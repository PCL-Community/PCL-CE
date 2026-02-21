using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using FileSystem = Microsoft.VisualBasic.FileIO.FileSystem;

namespace PCL;

public static class ModValidate
{
    /// <summary>
    ///     进行输入验证，并返回错误原因。
    ///     如果没有错误则返回空字符串。
    /// </summary>
    public static string Validate(string Text, IEnumerable<ValidateType> ValidateRules)
    {
        var Result = "";
        foreach (var ValidateRule in ValidateRules)
        {
            Result = ValidateRule.Validate(Text);
            if (Result is null)
                return "";
            if (!string.IsNullOrEmpty(Result))
                return Result;
        }

        return Result;
    }
}

/// <summary>
///     输入验证规则基类。要查看所有的输入验证规则，可在输入 Validate 后查看自动补全。
/// </summary>
public abstract class ValidateType
{
    /// <summary>
    ///     验证某字符串是否符合验证要求。若符合，返回空字符串；若不符合，返回错误原因；若需要中断检查并直接通过，返回 Nothing。
    /// </summary>
    public abstract string Validate(string Str);
}

/// <summary>
///     若为空则直接通过检查。
/// </summary>
public class ValidateNullable : ValidateType
{
    public override string Validate(string Str)
    {
        if (Str == null || string.IsNullOrEmpty(Str))
            return null;
        return "";
    }
}

/// <summary>
///     不能为 Nothing 或空字符串（不包括全空格检查）。
/// </summary>
public class ValidateNullOrEmpty : ValidateType
{
    public override string Validate(string Str)
    {
        if (Str == null || string.IsNullOrEmpty(Str))
            return "输入内容不能为空！";
        return "";
    }
}

/// <summary>
///     不能为 Nothing 或空字符串（包括全空格检查）。
/// </summary>
public class ValidateNullOrWhiteSpace : ValidateType
{
    public override string Validate(string Str)
    {
        if (Str == null || string.IsNullOrWhiteSpace(Str))
            return "输入内容不能为空！";
        return "";
    }
}

/// <summary>
///     必须满足正则表达式。
/// </summary>
public class ValidateRegex : ValidateType
{
    public ValidateRegex()
    {
    } // 用于 XAML 初始化

    public ValidateRegex(string Regex, string ErrorDescription = "正则检查失败！")
    {
        this.Regex = Regex;
        this.ErrorDescription = ErrorDescription;
    }

    public string Regex { get; set; }
    public string ErrorDescription { get; set; } = "正则检查失败！";

    public override string Validate(string Str)
    {
        if (!Str.RegexCheck(Regex))
            return ErrorDescription;
        return "";
    }
}

/// <summary>
///     必须是一个完整网址。
/// </summary>
public class ValidateHttp : ValidateType
{
    public ValidateHttp()
    {
    } // 用于 XAML 初始化

    public ValidateHttp(bool AllowsNullOrEmpty = false)
    {
        this.AllowsNullOrEmpty = AllowsNullOrEmpty;
    }

    public bool AllowsNullOrEmpty { get; set; }

    public override string Validate(string Str)
    {
        if (AllowsNullOrEmpty && string.IsNullOrEmpty(Str))
            return "";
        if (Str.EndsWithF("/"))
            Str = Str.Substring(0, Str.Length - 1);
        if (!Str.RegexCheck(@"^(http[s]?)\://"))
            return "输入的网址无效！";
        return "";
    }
}

/// <summary>
///     必须是一个完整网址或 UNC 路径。
/// </summary>
public class ValidateHttpOrUnc : ValidateType
{
    public ValidateHttpOrUnc()
    {
    } // 用于 XAML 初始化

    public ValidateHttpOrUnc(bool AllowsNullOrEmpty = false)
    {
        this.AllowsNullOrEmpty = AllowsNullOrEmpty;
    }

    public bool AllowsNullOrEmpty { get; set; }

    public override string Validate(string Str)
    {
        if (AllowsNullOrEmpty && string.IsNullOrEmpty(Str))
            return "";
        if (Str.EndsWithF("/") || Str.EndsWithF(@"\"))
            Str = Str.Substring(0, Str.Length - 1);
        if (!(Str.RegexCheck(@"^(http[s]?)\://") || Str.StartsWithF(@"\\")))
            return "输入的网址无效！";
        return "";
    }
}

/// <summary>
///     必须为整数。
/// </summary>
public class ValidateInteger : ValidateType
{
    public ValidateInteger()
    {
    } // 用于 XAML 初始化

    public ValidateInteger(int Min, int Max)
    {
        this.Min = Min;
        this.Max = Max;
    }

    public int Min { get; set; }
    public int Max { get; set; } = int.MaxValue;

    public override string Validate(string Str)
    {
        if (Str.Length > 9)
            return "请输入一个大小合理的数字！";
        var Valed = (int)Math.Round(ModBase.Val(Str));
        if ((Valed.ToString() ?? "") != (Str ?? ""))
            return "请输入一个整数！";
        if (ModBase.Val(Str) > Max)
            return "不可超过 " + Max + "！";
        if (ModBase.Val(Str) < Min)
            return "不可低于 " + Min + "！";
        return "";
    }
}

/// <summary>
///     长度限制。
/// </summary>
public class ValidateLength : ValidateType
{
    public ValidateLength()
    {
    } // 用于 XAML 初始化

    public ValidateLength(int Min, int Max = int.MaxValue)
    {
        this.Min = Min;
        this.Max = Max;
    }

    public int Min { get; set; }
    public int Max { get; set; } = int.MaxValue;

    public override string Validate(string Str)
    {
        if (Strings.Len(Str) != Max && Max == Min)
            return $"长度必须为 {Max} 个字符！";
        if (Strings.Len(Str) > Max)
            return $"长度最长为 {Max} 个字符！";
        if (Strings.Len(Str) < Min)
            return $"长度至少需 {Min} 个字符！";
        return "";
    }
}

/// <summary>
///     不能包含某些特定字符串。忽略大小写。
/// </summary>
public class ValidateExcept : ValidateType
{
    public ValidateExcept()
    {
        ErrorMessage = "输入内容不能包含 %";
    } // 用于 XAML 初始化

    public ValidateExcept(Collection<string> Excepts, string ErrorMessage = "输入内容不能包含 %")
    {
        this.Excepts = Excepts;
        this.ErrorMessage = ErrorMessage;
    }

    public ValidateExcept(IEnumerable Excepts, string ErrorMessage = "输入内容不能包含 %")
    {
        this.Excepts = new Collection<string>();
        this.ErrorMessage = ErrorMessage;
        foreach (string Data in Excepts)
            this.Excepts.Add(Data);
    }

    public Collection<string> Excepts { get; set; } = new();
    public string ErrorMessage { get; set; }

    public override string Validate(string Str)
    {
        foreach (var Ch in Excepts)
            if (Str.IndexOfF(Ch, true) >= 0)
            {
                if (ErrorMessage == null)
                    ErrorMessage = "";
                return ErrorMessage.Replace("%", Ch);
            }

        return "";
    }
}

/// <summary>
///     不能与某些特定字符串相同。
/// </summary>
public class ValidateExceptSame : ValidateType
{
    public ValidateExceptSame()
    {
    }

    public ValidateExceptSame(Collection<string> Excepts, string ErrorMessage = "输入内容不能为 %", bool IgnoreCase = false)
    {
        this.Excepts = Excepts;
        this.ErrorMessage = ErrorMessage;
        this.IgnoreCase = IgnoreCase;
    }

    public ValidateExceptSame(IEnumerable Excepts, string ErrorMessage = "输入内容不能为 %", bool IgnoreCase = false)
    {
        this.Excepts = new Collection<string>();
        foreach (string Data in Excepts)
            this.Excepts.Add(Data);
        this.ErrorMessage = ErrorMessage;
        this.IgnoreCase = IgnoreCase;
    }

    public Collection<string> Excepts { get; set; } = new();
    public string ErrorMessage { get; set; }
    public bool IgnoreCase { get; set; }

    public override string Validate(string Str)
    {
        if (Str is null)
            return ErrorMessage.Replace("%", "null");
        foreach (var Ch in Excepts)
            if (IgnoreCase)
            {
                if ((Str.ToLower() ?? "") == (Ch.ToLower() ?? ""))
                    return ErrorMessage.Replace("%", Ch);
            }
            else if (Str.Equals(Ch))
            {
                return ErrorMessage.Replace("%", Ch);
            }
        // 使用 = 不确定是否会忽略大小写

        return "";
    }
}

/// <summary>
///     对文件夹名的粗略的特化检测。
/// </summary>
public class ValidateFolderName : ValidateType
{
    private readonly bool IsIgnoreSameName;
    private readonly IEnumerable<DirectoryInfo> PathIgnore;

    public ValidateFolderName()
    {
    }

    public ValidateFolderName(string Path, bool UseMinecraftCharCheck = true, bool IgnoreCase = true,
        bool IgnoreSameName = false)
    {
        this.Path = Path;
        this.IgnoreCase = IgnoreCase;
        this.UseMinecraftCharCheck = UseMinecraftCharCheck;
        // On Error Resume Next
        try
        {
            PathIgnore = new DirectoryInfo(Path).EnumerateDirectories();
        }
        catch (DirectoryNotFoundException ex) // ignored
        {
        }

        IsIgnoreSameName = IgnoreSameName;
    }

    public string Path { get; set; }
    public bool UseMinecraftCharCheck { get; set; } = true;
    public bool IgnoreCase { get; set; } = true;

    public override string Validate(string Str)
    {
        try
        {
            // 检查是否为空
            var LengthCheck = new ValidateNullOrWhiteSpace().Validate(Str);
            if (!string.IsNullOrEmpty(LengthCheck))
                return LengthCheck;
            // 检查空格
            if (Str.StartsWithF(" "))
                return "文件夹名不能以空格开头！";
            if (Str.EndsWithF(" "))
                return "文件夹名不能以空格结尾！";
            // 检查长度
            LengthCheck = new ValidateLength(1, 100).Validate(Str);
            if (!string.IsNullOrEmpty(LengthCheck))
                return LengthCheck;
            // 检查尾部小数点
            if (Str.EndsWithF("."))
                return "文件夹名不能以小数点结尾！";
            // 检查特殊字符
            var CharactCheck =
                new ValidateExcept(System.IO.Path.GetInvalidFileNameChars() + (UseMinecraftCharCheck ? "!;" : ""),
                    "文件夹名不可包含 % 字符！").Validate(Str);
            if (!string.IsNullOrEmpty(CharactCheck))
                return CharactCheck;
            // 检查特殊字符串
            var InvalidStrCheck = new ValidateExceptSame(
                new[]
                {
                    "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6",
                    "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5",
                    "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³"
                }, "文件夹名不可为 %！", true).Validate(Str);
            if (!string.IsNullOrEmpty(InvalidStrCheck))
                return InvalidStrCheck;
            // 检查 NTFS 8.3 文件名（#4505）
            if (Str.RegexCheck(@".{2,}~\d"))
                return "文件夹名不能包含这一特殊格式！";
            // 检查文件夹重名
            var Arr = new List<string>();
            if (PathIgnore is not null)
                foreach (var Folder in PathIgnore)
                    Arr.Add(Folder.Name);
            if (!IsIgnoreSameName)
            {
                var SameNameCheck = new ValidateExceptSame(Arr, "不可与现有文件夹重名！", IgnoreCase).Validate(Str);
                if (!string.IsNullOrEmpty(SameNameCheck))
                    return SameNameCheck;
            }

            return "";
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "检查文件夹名出错");
            return "错误：" + ex.Message;
        }
    }
}

/// <summary>
///     对文件名的粗略的特化检测。
/// </summary>
public class ValidateFileName : ValidateType
{
    public ValidateFileName()
    {
    }

    public ValidateFileName(string Name, bool UseMinecraftCharCheck = true, bool IgnoreCase = true)
    {
        this.Name = Name;
        this.IgnoreCase = IgnoreCase;
        this.UseMinecraftCharCheck = UseMinecraftCharCheck;
    }

    public string Name { get; set; }
    public bool UseMinecraftCharCheck { get; set; } = true;
    public bool IgnoreCase { get; set; } = true;
    public string ParentFolder { get; set; } = null;
    public object RequireParentFolderExists { get; set; } = true;

    public override string Validate(string Str)
    {
        try
        {
            // 检查是否为空
            var LengthCheck = new ValidateNullOrWhiteSpace().Validate(Str);
            if (!string.IsNullOrEmpty(LengthCheck))
                return LengthCheck;
            // 检查空格
            if (Str.StartsWithF(" "))
                return "文件名不能以空格开头！";
            if (Str.EndsWithF(" "))
                return "文件名不能以空格结尾！";
            // 检查长度
            LengthCheck = new ValidateLength(1, 253).Validate(Str + (ParentFolder ?? ""));
            if (!string.IsNullOrEmpty(LengthCheck))
                return LengthCheck;
            // 检查尾部小数点
            if (Str.EndsWithF("."))
                return "文件名不能以小数点结尾！";
            // 检查特殊字符
            var CharactCheck = new ValidateExcept(Path.GetInvalidFileNameChars() + (UseMinecraftCharCheck ? "!;" : ""),
                "文件名不可包含 % 字符！").Validate(Str);
            if (!string.IsNullOrEmpty(CharactCheck))
                return CharactCheck;
            // 检查特殊字符串
            var InvalidStrCheck = new ValidateExceptSame(
                new[]
                {
                    "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6",
                    "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5",
                    "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³"
                }, "文件名不可为 %！", true).Validate(Str);
            if (!string.IsNullOrEmpty(InvalidStrCheck))
                return InvalidStrCheck;
            // 检查 NTFS 8.3 文件名（#4505）
            if (Str.RegexCheck(@".{2,}~\d"))
                return "文件名不能包含这一特殊格式！";
            // 检查文件重名
            if (ParentFolder is not null)
            {
                var DirInfo = new DirectoryInfo(ParentFolder);
                if (DirInfo.Exists)
                {
                    var SameNameCheck = new ValidateExceptSame(DirInfo.EnumerateFiles("*").Select(f => f.Name),
                        "不可与现有文件重名！", IgnoreCase).Validate(Str);
                    if (!string.IsNullOrEmpty(SameNameCheck))
                        return SameNameCheck;
                }
                else if (Conversions.ToBoolean(RequireParentFolderExists))
                {
                    return $"父文件夹不存在：{ParentFolder}";
                }
            }

            return "";
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "检查文件名出错");
            return "错误：" + ex.Message;
        }
    }
}

/// <summary>
///     要求输入一个可用的文件夹路径。
/// </summary>
public class ValidateFolderPath : ValidateType
{
    public ValidateFolderPath()
    {
    }

    public ValidateFolderPath(bool UseMinecraftCharCheck)
    {
        this.UseMinecraftCharCheck = UseMinecraftCharCheck;
    }

    public bool UseMinecraftCharCheck { get; set; } = true;

    public override string Validate(string Str)
    {
        // 去除尾部斜线，统一为 \
        Str = Str.Replace("/", @"\");
        if (!Str.TrimEnd(@"\").EndsWith(":"))
            Str = Str.TrimEnd('\\');
        // 检查是否为空
        var LengthCheck = new ValidateNullOrWhiteSpace().Validate(Str);
        if (!string.IsNullOrEmpty(LengthCheck))
            return LengthCheck;
        // 检查长度
        LengthCheck = new ValidateLength(1, 254).Validate(Str);
        if (!string.IsNullOrEmpty(LengthCheck))
            return LengthCheck;
        // 检查开头
        if (Str.StartsWithF(@"\\Mac\"))
            goto Fin;
        foreach (var Drive in FileSystem.Drives)
        {
            if ((Str.ToUpper() ?? "") == (Drive.Name ?? ""))
                return "";
            if (Str.StartsWithF(Drive.Name, true))
                goto Fin;
        }

        return "文件夹路径头存在错误！";
        Fin: ;

        // 对首层以外的路径检查
        for (int i = Str.StartsWithF(@"\\Mac\") ? 2 : 1, loopTo = Str.Split(@"\").Count() - 1; i <= loopTo; i++)
        {
            var SubStr = Str.Split(@"\")[i];
            // 检查是否为空
            var SubLengthCheck = new ValidateNullOrWhiteSpace().Validate(SubStr);
            if (!string.IsNullOrEmpty(SubLengthCheck))
                return "文件夹路径存在错误！";
            // 检查特殊字符
            var CharactCheck =
                new ValidateExcept(Path.GetInvalidFileNameChars() + (UseMinecraftCharCheck ? "!;" : ""), "路径中存在无效字符！")
                    .Validate(SubStr);
            if (!string.IsNullOrEmpty(CharactCheck))
                return CharactCheck;
            // 检查头部空格
            if (SubStr.StartsWithF(" "))
                return "文件夹名不能以空格开头！";
            if (SubStr.EndsWithF(" "))
                return "文件夹名不能以空格结尾！";
            // 检查尾部小数点
            if (SubStr.EndsWithF("."))
                return "文件夹名不能以小数点结尾！";
            // 检查特殊字符串
            var InvalidStrCheck = new ValidateExceptSame(
                new[]
                {
                    "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6",
                    "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5",
                    "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³"
                }, "文件夹名不可为 %！").Validate(SubStr);
            if (!string.IsNullOrEmpty(InvalidStrCheck))
                return InvalidStrCheck;
            // 检查 NTFS 8.3 文件名（#4505）
            if (Str.RegexCheck(@".{2,}~\d"))
                return "文件夹名不能包含这一特殊格式！";
        }

        return "";
    }
}
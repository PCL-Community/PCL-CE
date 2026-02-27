using System;
using System.IO;
using FluentValidation;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Utils.Validate;

public class FolderPathValidator : FileSystemValidator
{
    public bool UseMinecraftCharCheck { get; set; }

    public FolderPathValidator(bool useMinecraftCharCheck = true)
    {
        UseMinecraftCharCheck = useMinecraftCharCheck;
        
        RuleFor(x => x)
            .NotEmpty().WithMessage("输入内容不能为空！")
            .Must(x => !x.EndsWith(' ')).WithMessage("文件夹名不能以空格结尾！")
            .Must(x => !x.EndsWith('.')).WithMessage("文件夹名不能以小数点结尾！");

        RuleForEach(x => GetSubPaths(x))
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("文件夹路径存在错误！")
            .Must(x => !x.StartsWith(' ')).WithMessage("文件夹名不能以空格开头！")
            .Must(x => !x.EndsWith(' ')).WithMessage("文件夹名不能以空格结尾！")
            .Must(x => !x.EndsWith('.')).WithMessage("文件夹名不能以小数点结尾！")
            .Custom((fileName, context) => 
            {
                var invalidChar = CheckInvalidStrings(fileName, UseMinecraftCharCheck ? ["!;"] : []);
                if (invalidChar != null)
                {
                    context.AddFailure($"文件夹名不可包含 {invalidChar} 字符！");
                }
            })
            .Custom((fileName, context) => 
            {
                var reservedWord = CheckReservedWord(fileName, []);
                if (reservedWord != null)
                {
                    context.AddFailure($"文件夹名不可为 {reservedWord}！");
                }
            })
            .Must(x => !x.IsMatch(RegexPatterns.Ntfs83FileName)).WithMessage("文件夹名不能包含这一特殊格式！")
            .OverridePropertyName("PathSegments");
    }

    private static string[] GetSubPaths(string path)
    {
        var fullPath = new DirectoryInfo(path).FullName;
        return fullPath[Path.GetPathRoot(fullPath)!.Length..]
            .TrimEnd(Path.DirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
    }
}
using System;
using System.IO;
using System.Linq;
using FluentValidation;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Utils.Validate;

public class FolderNameValidator : FileSystemValidator
{
    public bool UseMinecraftCharCheck { get; set; }
    public bool IgnoreCase { get; set; }
    public bool IgnoreSameNameInParentFolder { get; set; }
    public string? ParentFolder { get; set; }
    
    public FolderNameValidator(string? parentFolder = null, bool useMinecraftCharCheck = true, bool ignoreCase = true,
        bool ignoreSameNameInParentFolder = true)
    {
        UseMinecraftCharCheck = useMinecraftCharCheck;
        IgnoreCase = ignoreCase;
        IgnoreSameNameInParentFolder = ignoreSameNameInParentFolder;
        ParentFolder = parentFolder;
        
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("输入内容不能为空！")
            .Must(x => !x.StartsWith(' ')).WithMessage("文件名不能以空格开头！")
            .Must(x => !x.EndsWith(' ')).WithMessage("文件名不能以空格结尾！")
            .Must(x => !x.EndsWith('.')).WithMessage("文件名不能以小数点结尾！")
            .Custom((fileName, context) => 
            {
                var invalidChar = CheckInvalidStrings(fileName, UseMinecraftCharCheck ? ["!;"] : []);
                if (invalidChar != null)
                {
                    context.AddFailure($"文件名不可包含 {invalidChar} 字符！");
                }
            })
            .Custom((fileName, context) => 
            {
                var reservedWord = CheckReservedWord(fileName, []);
                if (reservedWord != null)
                {
                    context.AddFailure($"文件名不可为 {reservedWord}！");
                }
            })
            .Must(x => !x.IsMatch(RegexPatterns.Ntfs83FileName)).WithMessage("文件名不能包含这一特殊格式！")
            .Must(x =>
            {
                if (ParentFolder is null) return true;
                
                var dirInfo = new DirectoryInfo(ParentFolder);
                if (!dirInfo.Exists) return true;
                if (IgnoreSameNameInParentFolder) return true;
                    
                return !dirInfo.EnumerateFiles().Select(f => f.Name).Contains(x,
                    IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

            }).WithMessage("不可与现有文件夹重名！");
    }
}
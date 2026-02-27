using System;
using System.IO;
using System.Linq;
using FluentValidation;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Utils.Validate;

public class FileNameValidator : FileSystemValidator
{
    public bool UseMinecraftCharCheck { get; set; }
    public bool IgnoreCase { get; set; }
    public string? ParentFolder { get; set; }
    public bool RequireParentFolderExists { get; set; }
    
    private bool? _isParentFolderExists;

    public FileNameValidator(string? parentFolder = null, bool ignoreCase = true, bool useMinecraftCharCheck = true,
        bool requireParentFolderExists = true)
    {
        ParentFolder = parentFolder;
        IgnoreCase = ignoreCase;
        UseMinecraftCharCheck = useMinecraftCharCheck;
        RequireParentFolderExists = requireParentFolderExists;
        
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
                if (dirInfo.Exists)
                {
                    return !dirInfo.EnumerateFiles().Select(f => f.Name).Contains(x,
                        IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
                }

                _isParentFolderExists = false;
                return !RequireParentFolderExists;

            }).WithMessage(_isParentFolderExists is not null ? $"父文件夹不存在：{ParentFolder}" : "不可与现有文件重名！");
    }
}
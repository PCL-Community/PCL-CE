using System;
using System.IO;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using PCL.Core.App.Localization;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Utils.Validate;

public class FileNameValidator(
    string? parentFolder = null,
    bool ignoreCase = true,
    bool useMinecraftCharCheck = true,
    bool requireParentFolderExists = true)
    : FileSystemValidator
{
    public bool UseMinecraftCharCheck { get; set; } = useMinecraftCharCheck;
    public bool IgnoreCase { get; set; } = ignoreCase;
    public string? ParentFolder { get; set; } = parentFolder;
    public bool RequireParentFolderExists { get; set; } = requireParentFolderExists;

    private bool? _isParentFolderExists;

    public FileNameValidator() : this(null)
    {
    }

    private void _BuildRules()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage(Lang.Text("Validation.Input.Required"))
            .Must(x => !x.StartsWith(' ')).WithMessage(Lang.Text("Validation.File.StartsWithSpace"))
            .Must(x => !x.EndsWith(' ')).WithMessage(Lang.Text("Validation.File.EndsWithSpace"))
            .Must(x => !x.EndsWith('.')).WithMessage(Lang.Text("Validation.File.EndsWithDot"))
            .Custom((fileName, context) => 
            {
                var invalidChar = CheckInvalidStrings(fileName, UseMinecraftCharCheck ? ["!;"] : []);
                if (invalidChar is not null)
                {
                    context.AddFailure(Lang.Text("Validation.File.InvalidCharacter", invalidChar));
                }
            })
            .Custom((fileName, context) => 
            {
                var reservedWord = CheckReservedWord(fileName, []);
                if (reservedWord is not null)
                {
                    context.AddFailure(Lang.Text("Validation.File.ReservedName", reservedWord));
                }
            })
            .Must(x => !x.IsMatch(RegexPatterns.Ntfs83FileName))
            .WithMessage(Lang.Text("Validation.File.SpecialFormat"))
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

            }).WithMessage(_isParentFolderExists is not null
                ? Lang.Text("Validation.File.ParentMissing", ParentFolder)
                : Lang.Text("Validation.File.Duplicate"));
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        _BuildRules();
        return base.PreValidate(context, result);
    }
}

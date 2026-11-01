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
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage(_ => Lang.Text("Common.Validate.Empty"))
            .Must(x => !x.StartsWith(' ')).WithMessage(_ => Lang.Text("Common.Validate.FileName.StartsWithSpace"))
            .Must(x => !x.EndsWith(' ')).WithMessage(_ => Lang.Text("Common.Validate.FileName.EndsWithSpace"))
            .Must(x => !x.EndsWith('.')).WithMessage(_ => Lang.Text("Common.Validate.FileName.EndsWithDot"))
            .Custom((fileName, context) =>
            {
                var invalidChar = CheckInvalidStrings(fileName, UseMinecraftCharCheck ? ["!;"] : []);
                if (invalidChar is not null)
                {
                    context.AddFailure(Lang.Text("Common.Validate.FileName.InvalidChar", invalidChar));
                }
            })
            .Custom((fileName, context) =>
            {
                var reservedWord = CheckReservedWord(fileName, []);
                if (reservedWord is not null)
                {
                    context.AddFailure(Lang.Text("Common.Validate.FileName.ReservedWord", reservedWord));
                }
            })
            .Must(x => !x.IsMatch(RegexPatterns.Ntfs83FileName))
            .WithMessage(_ => Lang.Text("Common.Validate.FileName.SpecialFormat"))
            .Must(x =>
            {
                _isParentFolderExists = null;
                if (ParentFolder is null) return true;

                var dirInfo = new DirectoryInfo(ParentFolder);
                if (dirInfo.Exists)
                {
                    return !dirInfo.EnumerateFiles().Select(f => f.Name).Contains(x,
                        IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
                }

                _isParentFolderExists = false;
                return !RequireParentFolderExists;

            }).WithMessage(_ => _isParentFolderExists is not null
                ? Lang.Text("Common.Validate.FileName.ParentFolderMissing", ParentFolder)
                : Lang.Text("Common.Validate.FileName.Duplicate"));
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        _BuildRules();
        return base.PreValidate(context, result);
    }
}
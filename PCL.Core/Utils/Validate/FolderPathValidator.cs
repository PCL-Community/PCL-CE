using System;
using System.IO;
using FluentValidation;
using FluentValidation.Results;
using PCL.Core.App.Localization;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Utils.Validate;

public class FolderPathValidator(bool useMinecraftCharCheck) : FileSystemValidator
{
    public bool UseMinecraftCharCheck { get; set; } = useMinecraftCharCheck;

    public FolderPathValidator() : this(true)
    {
    }

    private void _BuildRules()
    {
        RuleFor(x => x)
            .NotEmpty().WithMessage(Lang.Text("Validation.Input.Required"))
            .Must(x => !x.EndsWith(' ')).WithMessage(Lang.Text("Validation.Folder.EndsWithSpace"))
            .Must(x => !x.EndsWith('.')).WithMessage(Lang.Text("Validation.Folder.EndsWithDot"));

        RuleForEach(x => _GetSubPaths(x))
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage(Lang.Text("Validation.Folder.PathInvalid"))
            .Must(x => !x.StartsWith(' ')).WithMessage(Lang.Text("Validation.Folder.StartsWithSpace"))
            .Must(x => !x.EndsWith(' ')).WithMessage(Lang.Text("Validation.Folder.EndsWithSpace"))
            .Must(x => !x.EndsWith('.')).WithMessage(Lang.Text("Validation.Folder.EndsWithDot"))
            .Custom((fileName, context) => 
            {
                var invalidChar = CheckInvalidStrings(fileName, UseMinecraftCharCheck ? ["!;"] : []);
                if (invalidChar is not null)
                {
                    context.AddFailure(Lang.Text("Validation.Folder.InvalidCharacter", invalidChar));
                }
            })
            .Custom((fileName, context) => 
            {
                var reservedWord = CheckReservedWord(fileName, []);
                if (reservedWord is not null)
                {
                    context.AddFailure(Lang.Text("Validation.Folder.ReservedName", reservedWord));
                }
            })
            .Must(x => !x.IsMatch(RegexPatterns.Ntfs83FileName))
            .WithMessage(Lang.Text("Validation.Folder.SpecialFormat"))
            .OverridePropertyName("PathSegments");
    }
    
    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        _BuildRules();
        return base.PreValidate(context, result);
    }
    
    private static string[] _GetSubPaths(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }
        
        var fullPath = new DirectoryInfo(path).FullName;
        return fullPath[Path.GetPathRoot(fullPath)!.Length..]
            .TrimEnd(Path.DirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
    }
}

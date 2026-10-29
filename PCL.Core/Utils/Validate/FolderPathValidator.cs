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
            .NotEmpty().WithMessage(_ => Lang.Text("Common.Validate.Empty"))
            .Must(x => !x.EndsWith(' ')).WithMessage(_ => Lang.Text("Common.Validate.FolderName.EndsWithSpace"))
            .Must(x => !x.EndsWith('.')).WithMessage(_ => Lang.Text("Common.Validate.FolderName.EndsWithDot"));

        RuleForEach(x => _GetSubPaths(x))
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage(_ => Lang.Text("Common.Validate.FolderPath.Malformed"))
            .Must(x => !x.StartsWith(' ')).WithMessage(_ => Lang.Text("Common.Validate.FolderName.StartsWithSpace"))
            .Must(x => !x.EndsWith(' ')).WithMessage(_ => Lang.Text("Common.Validate.FolderName.EndsWithSpace"))
            .Must(x => !x.EndsWith('.')).WithMessage(_ => Lang.Text("Common.Validate.FolderName.EndsWithDot"))
            .Custom((fileName, context) =>
            {
                var invalidChar = CheckInvalidStrings(fileName, UseMinecraftCharCheck ? ["!;"] : []);
                if (invalidChar is not null)
                {
                    context.AddFailure(Lang.Text("Common.Validate.FolderName.InvalidChar", invalidChar));
                }
            })
            .Custom((fileName, context) =>
            {
                var reservedWord = CheckReservedWord(fileName, []);
                if (reservedWord is not null)
                {
                    context.AddFailure(Lang.Text("Common.Validate.FolderName.ReservedWord", reservedWord));
                }
            })
            .Must(x => !x.IsMatch(RegexPatterns.Ntfs83FileName))
            .WithMessage(_ => Lang.Text("Common.Validate.FolderName.SpecialFormat"))
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
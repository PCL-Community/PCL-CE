using FluentValidation;
using PCL.Core.App.Localization;

namespace PCL.Core.Utils.Validate;

public class NullOrWhiteSpaceValidator : AbstractValidator<string>
{
    public NullOrWhiteSpaceValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage(_ => Lang.Text("Common.Validate.Empty"));
    }
}
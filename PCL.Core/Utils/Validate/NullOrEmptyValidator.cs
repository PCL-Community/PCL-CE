using FluentValidation;

using PCL.Core.App.Localization;

namespace PCL.Core.Utils.Validate;

public class NullOrEmptyValidator : AbstractValidator<string>
{
    public NullOrEmptyValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x)).WithMessage(Lang.Text("Validation.Input.Required"));
    }
}

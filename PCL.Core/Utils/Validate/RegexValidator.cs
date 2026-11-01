using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using PCL.Core.App.Localization;

namespace PCL.Core.Utils.Validate;

public class RegexValidator(string pattern = "", string? errorMessage = null) : AbstractValidator<string>
{
    public RegexValidator() : this(string.Empty)
    {
    }

    public string Pattern { get; set; } = pattern;

    public string? ErrorMessage { get; set; } = errorMessage;
    public string ErrorKey { get; set; } = "";

    private void _BuildRules()
    {
        RuleFor(x => x)
            .Must(x => Regex.IsMatch(x, Pattern))
            .WithMessage(_ => string.IsNullOrEmpty(ErrorKey)
                ? ErrorMessage ?? Lang.Text("Common.Validate.RegexFailed")
                : Lang.Text(ErrorKey));
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        _BuildRules();
        return base.PreValidate(context, result);
    }
}
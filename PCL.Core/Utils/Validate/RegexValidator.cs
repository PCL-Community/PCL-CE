using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;

namespace PCL.Core.Utils.Validate;

public class RegexValidator(string pattern = "", string errorMessage = "正则检查失败！") : AbstractValidator<string>
{
    public string Pattern { get; set; } = pattern;
    public string ErrorMessage { get; set; } = errorMessage;

    public RegexValidator() : this(string.Empty) {}

    private void BuildRules()
    {
        RuleFor(x => x)
            .Must(x => Regex.IsMatch(x, Pattern)).WithMessage(ErrorMessage);
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        BuildRules();
        return base.PreValidate(context, result);
    }
}
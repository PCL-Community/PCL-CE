using System.Text.RegularExpressions;
using FluentValidation;

namespace PCL.Core.Utils.Validate;

public class RegexValidator : AbstractValidator<string>
{
    public string Pattern { get; set; }
    public string ErrorMessage { get; set; }
    
    public RegexValidator(string pattern, string errorMessage = "正则检查失败！")
    {
        Pattern = pattern;
        ErrorMessage = errorMessage;
        RuleFor(x => x)
            .Must(x => Regex.IsMatch(x, Pattern)).WithMessage(ErrorMessage);
    }
}
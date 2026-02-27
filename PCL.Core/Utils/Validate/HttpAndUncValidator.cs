using FluentValidation;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Utils.Validate;

public class HttpAndUncValidator : AbstractValidator<string>
{
    public bool AllowsNullOrEmpty { get; set; }
    
    public HttpAndUncValidator(bool allowNullOrEmpty = false)
    {
        AllowsNullOrEmpty = allowNullOrEmpty;

        RuleFor(x => x)
            .Must(x =>
            {
                if (AllowsNullOrEmpty && string.IsNullOrEmpty(x))
                {
                    return true;
                }

                return x.IsMatch(RegexPatterns.HttpUri) || x.IsMatch(RegexPatterns.UncPath);
            }).WithMessage("输入的网址无效！");
    }
}
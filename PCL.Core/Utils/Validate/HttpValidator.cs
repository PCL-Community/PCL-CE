using FluentValidation;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Utils.Validate;

public class HttpValidator : AbstractValidator<string>
{
    public bool AllowsNullOrEmpty { get; set; }
    
    public HttpValidator(bool allowNullOrEmpty = false)
    {
        AllowsNullOrEmpty = allowNullOrEmpty;

        RuleFor(x => x)
            .Must(x =>
            {
                if (AllowsNullOrEmpty && string.IsNullOrEmpty(x))
                {
                    return true;
                }

                return x.IsMatch(RegexPatterns.HttpUri);
            }).WithMessage("输入的网址无效！");

    }
}
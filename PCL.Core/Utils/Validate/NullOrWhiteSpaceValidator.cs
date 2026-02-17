using FluentValidation;

namespace PCL.Core.Utils.Validate;

public class NullOrWhiteSpaceValidator : AbstractValidator<string>
{
    public NullOrWhiteSpaceValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("输入内容不能为空！");
    }
}
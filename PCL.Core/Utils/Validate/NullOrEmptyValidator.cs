using FluentValidation;

namespace PCL.Core.Utils.Validate;

public class NullOrEmptyValidator : AbstractValidator<string>
{
    public NullOrEmptyValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x)).WithMessage("输入内容不能为空！");
    }
}
using FluentValidation;
using FluentValidation.Results;

namespace PCL.Core.Utils.Validate;

public class StringLengthValidator(int min = 0, int max = int.MaxValue) : AbstractValidator<string>
{
    public int Min { get; set; } = min;
    public int Max { get; set; } = max;

    public StringLengthValidator() : this(0)
    {
    }

    private void BuildRules()
    {
        RuleFor(x => x)
            .Must(x => x.Length != Max || Max == Min).WithMessage($"长度必须为 {Max} 个字符！")
            .Must(x => x.Length >= Min).WithMessage($"长度至少为 {Min} 个字符！")
            .Must(x => x.Length <= Max).WithMessage($"长度最长为 {Max} 个字符！");
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        BuildRules();
        return base.PreValidate(context, result);
    }
}
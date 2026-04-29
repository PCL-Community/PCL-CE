using FluentValidation;
using FluentValidation.Results;

namespace PCL.Core.Utils.Validate;

public class IntValidator(int max = int.MaxValue, int min = int.MinValue) : AbstractValidator<string>
{
    public int Max { get; set; } = max;
    public int Min { get; set; } = min;

    public IntValidator() : this(int.MaxValue)
    {
    }

    private void BuildRules()
    {
        RuleFor(x => x)
            .Must(x => x.Length < 9).WithMessage("请输入一个大小合理的数字！")
            .Must(x => int.TryParse(x, out _)).WithMessage("请输入一个整数！")
            .Must(x => int.TryParse(x, out var value) && value <= Max).WithMessage($"不可超过 {Max}！")
            .Must(x => int.TryParse(x, out var value) && value >= Min).WithMessage($"不可低于 {Min}！");
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        BuildRules();
        return base.PreValidate(context, result);
    }
}
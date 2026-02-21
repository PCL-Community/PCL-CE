using FluentValidation;

namespace PCL.Core.Utils.Validate;

public class StringLengthValidator : AbstractValidator<string>
{
    public int Min { get; set; }
    public int Max { get; set; }
    
    public StringLengthValidator(int min = 0, int max = int.MaxValue)
    {
        Min = min;
        Max = max;

        RuleFor(x => x)
            .Must(x => x.Length != Max || Max == Min).WithMessage($"长度必须为 {Max} 个字符！")
            .Must(x => x.Length >= Min).WithMessage($"长度至少为 {Min} 个字符！")
            .Must(x => x.Length <= Max).WithMessage($"长度最长为 {Max} 个字符！");
    }
}
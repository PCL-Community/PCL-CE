using FluentValidation;
using FluentValidation.Results;
using PCL.Core.App.Localization;

namespace PCL.Core.Utils.Validate;

public class IntValidator(int max = int.MaxValue, int min = int.MinValue) : AbstractValidator<string>
{
    public int Max { get; set; } = max;
    public int Min { get; set; } = min;

    public IntValidator() : this(int.MaxValue)
    {
    }

    private void _BuildRules()
    {
        RuleFor(x => x)
            .Must(x => x.Length < 9).WithMessage(_ => Lang.Text("Common.Validate.Int.Unreasonable"))
            .Must(x => int.TryParse(x, out _)).WithMessage(_ => Lang.Text("Common.Validate.Int.Invalid"))
            .Must(x => int.TryParse(x, out var value) && value <= Max)
            .WithMessage(_ => Lang.Text("Common.Validate.Int.Max", Max))
            .Must(x => int.TryParse(x, out var value) && value >= Min)
            .WithMessage(_ => Lang.Text("Common.Validate.Int.Min", Min));
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        _BuildRules();
        return base.PreValidate(context, result);
    }
}
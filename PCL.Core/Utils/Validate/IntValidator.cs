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
            .Must(x => x.Length < 9).WithMessage(Lang.Text("Validation.Integer.Reasonable"))
            .Must(x => int.TryParse(x, out _)).WithMessage(Lang.Text("Validation.Integer.Required"))
            .Must(x => int.TryParse(x, out var value) && value <= Max)
            .WithMessage(Lang.Text("Validation.Integer.Maximum", Max))
            .Must(x => int.TryParse(x, out var value) && value >= Min)
            .WithMessage(Lang.Text("Validation.Integer.Minimum", Min));
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        _BuildRules();
        return base.PreValidate(context, result);
    }
}

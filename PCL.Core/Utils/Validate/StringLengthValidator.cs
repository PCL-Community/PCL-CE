using FluentValidation;
using FluentValidation.Results;

using PCL.Core.App.Localization;

namespace PCL.Core.Utils.Validate;

public class StringLengthValidator(int min = 0, int max = int.MaxValue) : AbstractValidator<string>
{
    public int Min { get; set; } = min;
    public int Max { get; set; } = max;

    public StringLengthValidator() : this(0)
    {
    }

    private void _BuildRules()
    {
        RuleFor(x => x)
            .Must(x => x.Length != Max || Max == Min).WithMessage(Lang.Text("Validation.Length.Exact", Max))
            .Must(x => x.Length >= Min).WithMessage(Lang.Text("Validation.Length.Minimum", Min))
            .Must(x => x.Length <= Max).WithMessage(Lang.Text("Validation.Length.Maximum", Max));
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        _BuildRules();
        return base.PreValidate(context, result);
    }
}

using System.Collections.Generic;
using FluentValidation;
using FluentValidation.Results;

using PCL.Core.App.Localization;

namespace PCL.Core.Utils.Validate;

public class BlacklistValidator(List<string> contains) : AbstractValidator<string>
{
    public List<string> Blacklist { get; set; } = contains;

    public BlacklistValidator() : this([])
    {
    }

    private void _BuildRules()
    {
        RuleFor(x => x)
            .Custom((input, context) =>
            {
                foreach (var items in Blacklist)
                {
                    if (input.Contains(items))
                    {
                        context.AddFailure(Lang.Text("Validation.Input.ForbiddenContent", items));
                    }
                }
            });
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        _BuildRules();
        return base.PreValidate(context, result);
    }
}

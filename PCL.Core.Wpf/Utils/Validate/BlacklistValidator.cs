using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using PCL.Core.App.Localization;

namespace PCL.Core.Utils.Validate;

public class BlacklistValidator(List<string> contains) : AbstractValidator<string>
{
    public BlacklistValidator() : this([])
    {
    }

    public List<string> Blacklist { get; set; } = contains;

    private void _BuildRules()
    {
        RuleFor(x => x)
            .Custom((input, context) =>
            {
                foreach (var items in Blacklist.Where(input.Contains))
                    context.AddFailure(Lang.Text("Common.Validate.Blacklisted", items));
            });
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        _BuildRules();
        return base.PreValidate(context, result);
    }
}
using System.Collections.Generic;
using FluentValidation;
using FluentValidation.Results;

namespace PCL.Core.Utils.Validate;

public class BlacklistValidator(List<string> contains) : AbstractValidator<string>
{
    public List<string> Blacklist { get; set; } = contains;

    public BlacklistValidator() : this([])
    {
    }

    private void BuildRules()
    {
        RuleFor(x => x)
            .Custom((input, context) =>
            {
                foreach (var items in Blacklist)
                {
                    if (input.Contains(items))
                    {
                        context.AddFailure($"输入内容不能包含 {items}！");
                    }
                }
            });
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        BuildRules();
        return base.PreValidate(context, result);
    }
}
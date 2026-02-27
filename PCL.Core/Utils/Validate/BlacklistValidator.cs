using System.Collections.Generic;
using FluentValidation;

namespace PCL.Core.Utils.Validate;

public class BlacklistValidator : AbstractValidator<string>
{
    public List<string> Blacklist { get; set; }
    
    public BlacklistValidator(List<string>? contains = null)
    {
        Blacklist = contains ?? [];
        
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
}
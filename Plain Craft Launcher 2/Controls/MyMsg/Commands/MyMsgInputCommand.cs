using System.Windows.Input;
using FluentValidation;

namespace PCL.Controls.MyMsg.Commands;

public class MyMsgInputCommand: ICommand
{
    public List<IValidator<string>> ValidateRules { get; } = [];
    public event Action<string>? Error;

    public event Action? Success;

    public event EventHandler? CanExecuteChanged;

    public void Execute(object? parameter)
    {
        var userInput = parameter?.ToString()!;
        foreach (var rule in ValidateRules)
        {
            var result = rule.Validate(userInput);
            if(result.IsValid) continue;
            Error?.Invoke(string.Join(Environment.NewLine, result.Errors.Select(e => e.ErrorMessage)));
            return;
        }
        Success?.Invoke();
    }
    
    public bool CanExecute(object? parameter) => true;

}
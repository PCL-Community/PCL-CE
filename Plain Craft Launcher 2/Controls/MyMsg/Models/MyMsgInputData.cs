using System.Windows.Input;
using FluentValidation;
using PCL.Controls.MyMsg.Commands;

namespace PCL.Controls.MyMsg.Models;

public class MyMsgInputData
{
    
    public string? UserInput { get; set; }
    
    public string? Description { get; set; }
    
    public bool NoError { get; set; }

    public ICommand Command;

    public MyMsgInputData(string description)
    {
        Description = description;
        var command = new MyMsgInputCommand();
        command.Error += _ => NoError = false;
        command.Success += () => NoError = true;
        Command = command;
    }

    public MyMsgInputData(List<IValidator<string>> rules)
    {
        var command = new MyMsgInputCommand();
        command.Error += _ => NoError = false;
        command.Success += () => NoError = true;
        rules.ForEach(v => command.ValidateRules.Add(v));
        Command = command;
    }

    public MyMsgInputData(string description, List<IValidator<string>> rules)
    {
        Description = description;
        var command = new MyMsgInputCommand();
        command.Error += _ => NoError = false;
        command.Success += () => NoError = true;
        rules.ForEach(v => command.ValidateRules.Add(v));
        Command = command;
    }
}
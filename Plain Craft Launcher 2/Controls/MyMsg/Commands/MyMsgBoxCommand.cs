using System.Windows.Input;
using PCL.Controls.MyMsg.Models;
using PCL.Core.Utils.Exts;

namespace PCL.Controls.MyMsg.Commands;

public class MyMsgBoxCommand: ICommand
{
    public MyMsgBoxCommand(ButtonContext[] contexts)
    {
        contexts.ForEachIndexed((value, index) => OperateMap[index] = value);
    }

    public event Action? Exited;

    public event Action<int>? Clicked;

    public event EventHandler? CanExecuteChanged;
    
    private Dictionary<int, ButtonContext> OperateMap { get; set; } = [];

    public string GetButtonTextById(int id) =>
        OperateMap.FirstOrDefault(key => key.Key == id).Value.ButtonName;

    public bool CanExecute(object? parameter) =>
        parameter is not null && OperateMap.ContainsKey((int)parameter);

    public void Execute(object? parameter)
    {
        
        if(parameter is null 
           || !OperateMap.TryGetValue((int)parameter, out var op)) return;
        
        Clicked?.Invoke((int)parameter);
        
        op.Operation.Invoke(parameter);
        if(op.ExitWhenClick) Exited?.Invoke();
    }
}
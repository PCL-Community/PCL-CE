using System.Windows.Input;

namespace PCL.Controls.MyMsg.Commands;

public class MyMsgInputButtonCommand: ICommand
{
    public event Action? Exited;
    
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => Exited?.Invoke();
}
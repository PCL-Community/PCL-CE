using System.Windows.Input;
using PCL.Controls.MyMsg.Commands;
using PCL.Controls.MyMsg.Models;

namespace PCL.Controls.MyMsg.ViewModels;

public class MyMsgInputViewModel: ViewModelBase
{
    public string ErrorDescription
    {
        get;
        set
        {
            if (field == value) return;
            SetProperty(value, ref field);
        }
    } = "";
    
    public ICommand ButtonCommand { get; set; }
    public ICommand ValidateCommand { get; set; }

    public MyMsgInputViewModel(MyMsgInputData data, ICommand buttonCommand)
    {
        ((MyMsgInputCommand)data.Command).Error += value => ErrorDescription = value;
        ButtonCommand = buttonCommand;
        ValidateCommand = data.Command;
    }
}
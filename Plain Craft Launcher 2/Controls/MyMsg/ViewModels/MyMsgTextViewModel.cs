using System.Windows;
using System.Windows.Input;
using PCL.Controls.MyMsg.Commands;
using PCL.Controls.MyMsg.Models;

namespace PCL.Controls.MyMsg.ViewModels;

public class MyMsgTextViewModel: ViewModelBase
{
    public string Title
    {
        get;
        set
        {
            if(field == value) return;
            SetProperty(value, ref field);
        }
    }

    public string Message
    {
        get;
        set
        {
            if(field == value) return;
            SetProperty(value, ref field);
        }
    }

    public Visibility Button1Visibility
    {
        get;
        set
        {
            if (field == value) return;
            SetProperty(value, ref field);
        }
    }
    
    
    public Visibility Button2Visibility
    {
        get;
        set
        {
            if (field == value) return;
            SetProperty(value, ref field);
        }
    }
    
    
    public Visibility Button3Visibility
    {
        get;
        set
        {
            if (field == value) return;
            SetProperty(value, ref field);
        }
    }

    public string Button1Text
    {
        get;
        set
        {
            if(value == field) return;
            SetProperty(value, ref field);
        }
    }

    public string Button2Text
    {
        get;
        set
        {
            if(value == field) return;
            SetProperty(value, ref field);
        }
    }
    
    public string Button3Text
    {
        get;
        set
        {
            if(value == field) return;
            SetProperty(value, ref field);
        }
    }

    public ICommand Context { get; set; }

    public MyMsgTextViewModel(MyMsgTextData data)
    {
        var cmd = (MyMsgBoxCommand)data.Command;
        Context = data.Command;
        Title = data.Title;
        Message = data.Message;
        var hasButton1 = Context.CanExecute(0);
        var hasButton2 = Context.CanExecute(1);
        var hasButton3 = Context.CanExecute(2);
        if (!hasButton1) throw new InvalidOperationException("必须提供至少一个按钮");
        Button1Visibility = hasButton1 ? Visibility.Visible : Visibility.Collapsed;
        Button2Visibility = hasButton2 ? Visibility.Visible : Visibility.Collapsed;
        Button3Visibility = hasButton3 ? Visibility.Visible : Visibility.Collapsed;
        Button1Text = cmd.GetButtonTextById(0);
        Button2Text = cmd.GetButtonTextById(1);
        Button3Text = cmd.GetButtonTextById(2);
    }
}
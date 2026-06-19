using System.Windows.Input;
using PCL.Controls.MyMsg.Commands;

// 非常感谢龙猫送来的工作量，非常感谢！！！

namespace PCL.Controls.MyMsg.Models;

public class MyMsgTextData
{
    /// <summary>
    /// 退出事件
    /// </summary>
    public event Action? Exited;
    /// <summary>
    /// 窗口消息
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = "提示";
    
    public MyMsgTextData(string message) : this(message,
        new ButtonContext
        {
            ButtonName = "确定", ExitWhenClick = true, Operation = static _ => { }
        },
        new ButtonContext
        {
            ButtonName = "取消", ExitWhenClick = true, Operation = static _ => { }
        }
    ){}

    public MyMsgTextData(string message, params ButtonContext[] buttons)
    {
        Message = message;
        var cmd =new MyMsgBoxCommand(buttons);
        cmd.Exited += () => Exited?.Invoke();
        cmd.Clicked += value => Result = value;
        Command = cmd;
    }
    
    /// <summary>
    /// 执行器
    /// </summary>
    public ICommand Command { get; set; }
    
    public int Result { get; set; }
}
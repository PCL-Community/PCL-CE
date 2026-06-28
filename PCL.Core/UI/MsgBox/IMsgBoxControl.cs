using System;
using System.Threading.Tasks;

namespace PCL.Core.UI.MsgBox;

/// <summary>
/// MsgBox UI Control interface
/// Used by MsgBoxActor<br/>
/// MVVM Control will implement this interface in the future
/// </summary>
public interface IMsgBoxControl
{
    MsgBoxRequest Request { get; }
    event EventHandler<MsgBoxResponse>? Completed;
    void InvokeShowAnimation();
    Task InvokeCloseAnimationAsync(MsgBoxResponse response);
}
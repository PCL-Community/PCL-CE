namespace PCL.UI.Controls.Common;

/// <summary>
///     单选控件（MyRadioButton/MyRadioBox/MyCheckBox 等）共用的选中事件约定。
/// </summary>
public interface IMyRadio
{
    delegate void ChangedEventHandler(object sender, RouteEventArgs e);

    delegate void CheckEventHandler(object sender, RouteEventArgs e);

    event CheckEventHandler Check;
    event ChangedEventHandler Changed;
}
using System.Windows;
using PCL.Core.UI;

namespace PCL;

public static class DialogButtonBuilder
{
    public static MyButton Build(DialogButton button, bool isWarn)
    {
        var btn = new MyButton
        {
            Text = button.Text,
            ColorType = button.IsPrimary
                ? (isWarn ? MyButton.ColorState.Red : MyButton.ColorState.Highlight)
                : MyButton.ColorState.Normal,
            Visibility = string.IsNullOrEmpty(button.Text) ? Visibility.Collapsed : Visibility.Visible,
            IsEnabled = true,
        };
        btn.ApplyTemplate();
        btn.TextPadding = new Thickness(7);
        btn.Padding = new Thickness(5, 0, 5, 0);
        btn.Margin = new Thickness(12, 0, 0, 0);
        btn.Name += ModBase.GetUuid();
        return btn;
    }
}

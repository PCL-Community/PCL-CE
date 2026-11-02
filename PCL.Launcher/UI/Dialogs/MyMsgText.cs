using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.UI.Dialogs;

namespace PCL.UI.Dialogs;

/// <summary>纯文本弹窗：正文可选中复制，超出高度时滚动显示。</summary>
public sealed class MyMsgText : MyMsgDialogBase
{
    public MyMsgText(DialogViewModel dialog) : base(dialog)
    {
        var caption = new SelectableTextBlock
        {
            Text = dialog.TextContent,
            TextWrapping = TextWrapping.Wrap,
            FontSize = DialogTheme.BodyFontSize,
            Padding = new Thickness(1d),
            VerticalAlignment = VerticalAlignment.Top
        };
        DialogTheme.BindBrush(caption, TextBlock.ForegroundProperty, "PageBrushText");
        SetBody(caption);
    }
}
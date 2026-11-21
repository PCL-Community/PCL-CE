using Avalonia;
using PCL.Core.UI.Dialogs;

namespace PCL.UI.Dialogs;

/// <summary>Markdown 弹窗：标题、段落、粗体、斜体、行内代码、链接、多级列表、图片与表格按控件树渲染。</summary>
public sealed class MyMsgMarkdown : MyMsgDialogBase
{
    public MyMsgMarkdown(DialogViewModel dialog) : base(dialog)
    {
        SetBodyPadding(new Thickness(0d, 0d, 7d, 0d));
        SetBody(MyMsgMarkdownRenderer.Render(dialog.TextContent));
    }
}
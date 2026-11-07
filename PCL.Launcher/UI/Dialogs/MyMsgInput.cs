using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentValidation;
using PCL.Core.UI.Dialogs;
using PCL.UI.Controls;

namespace PCL.UI.Dialogs;

/// <summary>输入弹窗：正文可选，输入框带提示与校验；校验不通过时主按钮不关闭弹窗。</summary>
public sealed class MyMsgInput : MyMsgDialogBase
{
    private readonly MyTextBox _input;

    public MyMsgInput(DialogViewModel dialog) : base(dialog)
    {
        _input = new MyTextBox
        {
            Text = dialog.InputDefaultValue,
            HintText = dialog.HintText ?? string.Empty,
            MaxLength = 1000,
            Margin = new Thickness(7d, 0d, 7d, 12d),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ValidateRules = new Collection<IValidator<string>>([.. dialog.ValidateRules])
        };

        if (string.IsNullOrEmpty(dialog.TextContent))
        {
            HideBody();
        }
        else
        {
            var caption = new TextBlock
            {
                Text = dialog.TextContent,
                TextWrapping = TextWrapping.Wrap,
                FontSize = DialogTheme.BodyFontSize,
                LineHeight = DialogTheme.BodyLineHeight
            };
            DialogTheme.BindBrush(caption, TextBlock.ForegroundProperty, "ColorBrush1");
            SetBody(caption);
        }

        SetFooter(_input);
    }

    /// <inheritdoc />
    public override bool FocusInitial()
    {
        if (!_input.Focus()) return base.FocusInitial();
        _input.CaretIndex = (_input.Text ?? string.Empty).Length;
        return true;
    }

    /// <summary>主按钮先执行校验；校验失败时保留弹窗并展示失败状态。</summary>
    protected override void HandleButton(int number)
    {
        if (number != 1)
        {
            InvokeActionOrClose(number);
            return;
        }

        _input.Validate();
        if (Dialog.IsClosed || !_input.IsValidated) return;

        Dialog.InputText = _input.Text ?? string.Empty;
        Dialog.CloseByButton(1);
    }
}
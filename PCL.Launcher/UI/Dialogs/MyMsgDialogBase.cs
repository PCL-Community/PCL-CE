using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.UI.Controls;

namespace PCL.UI.Dialogs;

/// <summary>
///     弹窗卡片：统一的标题、分隔线、正文滚动区、页脚与三按钮行。
///     子类负责填入正文/页脚，并决定各按钮的确认语义。
/// </summary>
public abstract class MyMsgDialogBase : UserControl
{
    private const double BodyMaxHeight = 400d;

    private readonly ContentControl _footerHost = new()
    {
        VerticalAlignment = VerticalAlignment.Top,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Top
    };

    private readonly MyScrollViewer _bodyScroll;
    private readonly MyButton[] _buttons;

    protected MyMsgDialogBase(DialogViewModel dialog)
    {
        Dialog = dialog;
        Focusable = true;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;

        var titleBrushKey = dialog.IsWarn ? "PageBrushError" : "PageBrushText";

        LabTitle = new TextBlock
        {
            Text = dialog.Title,
            FontSize = DialogTheme.TitleFontSize,
            Margin = new Thickness(7d, -1d, 70d, 9d),
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.None,
            VerticalAlignment = VerticalAlignment.Top
        };
        DialogTheme.BindBrush(LabTitle, TextBlock.ForegroundProperty, titleBrushKey);

        var shapeLine = new Rectangle
        {
            Height = 2d,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        DialogTheme.BindBrush(shapeLine, Shape.FillProperty, titleBrushKey);

        _bodyScroll = new MyScrollViewer
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = BodyMaxHeight,
            Margin = new Thickness(0d, 0d, 0d, 17d),
            Padding = new Thickness(7d, 0d, 15d, 0d),
            DeltaMult = 0.7d
        };

        var panBtn = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(150d, 0d, 8d, 0d)
        };

        _buttons = new MyButton[DialogButtons.Capacity];
        for (var index = 0; index < _buttons.Length; index++)
        {
            var button = new MyButton
            {
                Margin = new Thickness(12d, 0d, 0d, 0d),
                TextPadding = new Thickness(7d, 0d),
                Padding = new Thickness(5d, 0d)
            };
            var number = index + 1;
            button.Click += (_, _) => OnButtonClicked(number);
            _buttons[index] = button;
            panBtn.Children.Add(button);
        }

        for (var number = 1; number <= DialogButtons.Capacity; number++)
            SetButtonText(number, dialog.ButtonText(number));

        var main = new Grid
        {
            Margin = new Thickness(22d, 22d, 22d, 23d),
            VerticalAlignment = VerticalAlignment.Top,
            RowDefinitions = new RowDefinitions("Auto,2,13,*,Auto,Auto")
        };
        Grid.SetRow(LabTitle, 0);
        Grid.SetRow(shapeLine, 1);
        Grid.SetRow(_bodyScroll, 3);
        Grid.SetRow(_footerHost, 4);
        Grid.SetRow(panBtn, 5);
        main.Children.Add(LabTitle);
        main.Children.Add(shapeLine);
        main.Children.Add(_bodyScroll);
        main.Children.Add(_footerHost);
        main.Children.Add(panBtn);

        var card = new Border
        {
            CornerRadius = new CornerRadius(DialogTheme.CardCornerRadius),
            BoxShadow = BoxShadows.Parse("0 2 20 0 #66000000"),
            Child = main
        };
        DialogTheme.BindBrush(card, Border.BackgroundProperty, "PageBrushCard");

        var root = new Grid
        {
            Margin = new Thickness(25d),
            MinWidth = 400d,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        root.Children.Add(card);
        Content = root;

        AddHandler(KeyDownEvent, OnDialogKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>当前弹窗状态。</summary>
    protected DialogViewModel Dialog { get; }

    /// <summary>标题行。</summary>
    protected TextBlock LabTitle { get; }

    /// <summary>取第 <paramref name="number" /> 个按钮（从 1 开始）。</summary>
    protected MyButton Button(int number)
    {
        return _buttons[number - 1];
    }

    /// <summary>写入正文控件；正文超出高度时在滚动区内显示。</summary>
    protected void SetBody(Control body)
    {
        _bodyScroll.Content = body;
        _bodyScroll.IsVisible = true;
    }

    /// <summary>隐藏正文区，仅保留标题、页脚与按钮。</summary>
    protected void HideBody()
    {
        _bodyScroll.IsVisible = false;
    }

    /// <summary>调整正文内边距。</summary>
    protected void SetBodyPadding(Thickness padding)
    {
        _bodyScroll.Padding = padding;
    }

    /// <summary>写入页脚控件；页脚固定在滚动区下方，不随正文滚动。</summary>
    protected void SetFooter(Control footer)
    {
        _footerHost.Content = footer;
        _bodyScroll.Margin = new Thickness(0d, 0d, 0d, 7d);
    }

    /// <summary>写入按钮文本；空文本的按钮不显示。</summary>
    protected void SetButtonText(int number, string text)
    {
        var button = _buttons[number - 1];
        button.Text = text;
        button.IsVisible = !string.IsNullOrEmpty(text);
        ApplyButtonColors();
    }

    /// <summary>危险操作时主按钮为红色；存在次要按钮时主按钮高亮。</summary>
    private void ApplyButtonColors()
    {
        _buttons[0].ColorType = Dialog.IsWarn
            ? MyButton.ColorState.Red
            : _buttons[1].IsVisible || _buttons[2].IsVisible
                ? MyButton.ColorState.Highlight
                : MyButton.ColorState.Normal;
    }

    /// <summary>把焦点放到弹窗内的起始控件；返回是否成功取得焦点。</summary>
    public virtual bool FocusInitial()
    {
        if (_buttons[0].IsVisible && _buttons[0].Focus()) return true;
        return Focus();
    }

    private void OnButtonClicked(int number)
    {
        if (Dialog.IsClosed) return;
        HandleButton(number);
    }

    /// <summary>处理按钮点击；<paramref name="number" /> 从 1 开始。</summary>
    protected virtual void HandleButton(int number)
    {
        InvokeActionOrClose(number);
    }

    /// <summary>按钮自带动作时执行动作，否则按状态收尾。</summary>
    protected void InvokeActionOrClose(int number)
    {
        var action = Dialog.ButtonAction(number);
        if (action is null)
        {
            Dialog.CloseByButton(number);
            return;
        }

        try
        {
            action();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI.Dialogs", "弹窗按钮动作执行失败");
        }
    }

    /// <summary>以取消结果关闭弹窗。</summary>
    protected void CloseAsCancelled()
    {
        Dialog.Close(DialogResult.Cancel);
    }

    private void OnDialogKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None) return;

        // 下拉列表展开时把按键交给下拉框自己处理（回车选中、Esc 收起）
        if (e.Source is ComboBox { IsDropDownOpen: true }
            || (e.Source is Visual source
                && source.FindAncestorOfType<ComboBox>() is { IsDropDownOpen: true }))
            return;

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (!Dialog.IsClosed) HandleButton(1);
            return;
        }

        if (e.Key != Key.Escape) return;
        e.Handled = true;
        if (Dialog.IsClosed) return;
        HandleButton(_buttons[2].IsVisible ? 3 : _buttons[1].IsVisible ? 2 : 1);
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using PCL.Core.App.Localization;
using SvgIconControl = PCL.UI.Controls.SvgIcon.SvgIcon;

namespace PCL.UI.Controls;

/// <summary>
///     搜索框：卡片容器 + 输入框 + 搜索图标 + 清空/搜索按钮。
///     保留 <see cref="HintText" />/<see cref="Text" />、清空按钮联动、可选搜索按钮、
///     <see cref="TextChanged" />/<see cref="Search" /> 事件与加载后自动聚焦。
///     清空按钮的显隐与按下回车时的自定义事件按输入状态触发。
/// </summary>
public class MySearchBox : MyCard
{
    public delegate void SearchEventHandler(object sender, EventArgs e);

    public delegate void TextChangedEventHandler(object sender, EventArgs e);

    public static readonly StyledProperty<string> HintTextProperty =
        AvaloniaProperty.Register<MySearchBox, string>(
            nameof(HintText),
            string.Empty);

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MySearchBox, string>(
            nameof(Text),
            string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    private readonly SvgIconControl _iconSearch;
    private readonly StackPanel _rightPanel;
    private bool _isSyncingText;

    public MySearchBox()
    {
        using var controlScope = PCL.Animations.SuspendControl();

        Height = 40d;

        TextBox = new MyTextBox
        {
            HasBackground = false,
            BorderThickness = new Thickness(0d),
            Padding = new Thickness(28.5d, 0d, 40d, 0d),
            MaxLength = 50,
            FontSize = 13.5d,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        TextBox.TextChanged += TextBox_TextChanged;

        _iconSearch = new SvgIconControl
        {
            Icon = "lucide/search",
            Width = 14d,
            Height = 14d,
            Margin = new Thickness(14d, 0d, 0d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };

        BtnClear = new MyIconButton
        {
            Width = 24d,
            Height = 24d,
            LogoScale = 0.75d,
            SvgIcon = "lucide/x",
            Theme = MyIconButton.Themes.Black,
            Opacity = 0d,
            IsHitTestVisible = false,
            Focusable = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        BtnClear.Click += BtnClear_Click;

        BtnSearch = new MyButton
        {
            Width = 60d,
            Height = 32d,
            Margin = new Thickness(0d, 4d, 4d, 4d),
            Text = Lang.Text("Common.Action.Search"),
            ColorType = MyButton.ColorState.Highlight,
            IsVisible = false,
            Focusable = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        BtnSearch.Click += BtnSearch_Click;

        _rightPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 10d, 0d),
            Spacing = 4d
        };
        _rightPanel.Children.Add(BtnClear);
        _rightPanel.Children.Add(BtnSearch);

        var layout = new Grid();
        layout.Children.Add(TextBox);
        layout.Children.Add(_iconSearch);
        layout.Children.Add(_rightPanel);
        Children.Add(layout);

        Loaded += (_, _) => TextBox.Focus();
    }

    private string ClearButtonKey => InputAnimation.Key("MySearchBox ClearBtn", uuid);

    // 属性

    public string HintText
    {
        get => GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool SearchButtonVisibility
    {
        get => IsSearchButtonVisible;
        set => IsSearchButtonVisible = value;
    }

    public bool IsSearchButtonVisible
    {
        get => BtnSearch.IsVisible;
        set
        {
            BtnSearch.IsVisible = value;
            _rightPanel.Margin = new Thickness(0d, 0d, value ? 4d : 10d, 0d);
        }
    }

    /// <summary>
    ///     内部输入框。
    /// </summary>
    public MyTextBox TextBox { get; }

    /// <summary>
    ///     清空按钮。
    /// </summary>
    public MyIconButton BtnClear { get; }

    /// <summary>
    ///     搜索按钮。
    /// </summary>
    public MyButton BtnSearch { get; }

    public event TextChangedEventHandler? TextChanged;

    public event SearchEventHandler? Search;

    // 交互

    private void TextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateClearButtonState();

        if (!_isSyncingText)
        {
            _isSyncingText = true;
            try
            {
                SetCurrentValue(TextProperty, TextBox.Text ?? string.Empty);
            }
            finally
            {
                _isSyncingText = false;
            }
        }

        TextChanged?.Invoke(TextBox, EventArgs.Empty);
    }

    private void BtnClear_Click(object? sender, EventArgs e)
    {
        TextBox.Text = string.Empty;
        TextBox.Focus();
    }

    private void BtnSearch_Click(object? sender, PointerReleasedEventArgs? e)
    {
        Search?.Invoke(BtnSearch, EventArgs.Empty);
    }

    private void UpdateClearButtonState()
    {
        var hasText = !string.IsNullOrEmpty(TextBox.Text);
        BtnClear.IsHitTestVisible = hasText;
        InputAnimation.Opacity(BtnClear, hasText ? 1d : 0d, 90, ClearButtonKey);
    }

    /// <summary>
    ///     回车时触发自定义事件。
    /// </summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            CustomEventService.Raise(this);
        base.OnKeyUp(e);
    }

    // 属性变更处理

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == HintTextProperty)
        {
            TextBox.HintText = change.NewValue as string ?? string.Empty;
        }
        else if (change.Property == TextProperty)
        {
            if (_isSyncingText)
                return;
            var value = change.NewValue as string ?? string.Empty;
            if ((TextBox.Text ?? string.Empty) == value)
                return;
            _isSyncingText = true;
            try
            {
                TextBox.Text = value;
            }
            finally
            {
                _isSyncingText = false;
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        ApplyIconBrush();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        InputAnimation.Stop(ClearButtonKey);
        base.OnDetachedFromVisualTree(e);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        ApplyIconBrush();
    }

    private void ApplyIconBrush()
    {
        _iconSearch.IconBrush = InputTheme.Text(this);
    }
}
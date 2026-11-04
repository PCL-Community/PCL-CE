using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using PCL.Core.Logging;

namespace PCL.UI.Controls;

/// <summary>
///     PCL 纯文字按钮：文字颜色随悬停/按下/禁用状态过渡，文本变更时淡出淡入。
///     常态基线由 <c>UI/Styles/Controls/MyTextButton.axaml</c> 提供。
/// </summary>
public partial class MyTextButton : Label
{
    public delegate void ClickEventHandler(object? sender, EventArgs e);

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyTextButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MyTextButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MyTextButton, object?>(nameof(CommandParameter));

    private static int _uuidSeed;

    /// <summary>动画键使用的唯一编号。</summary>
    public readonly int Uuid = Interlocked.Increment(ref _uuidSeed);

    private bool _isCommandRunning;

    public bool isMouseDown;

    public MyTextButton()
    {
        InitializeComponent();

        LabText.Text = Text;

        PointerPressed += _OnPointerPressed;
        PointerReleased += _OnPointerReleased;
        PointerEntered += (_, _) => RefreshColor(true);
        PointerExited += _OnPointerExited;

        Loaded += (_, _) => RefreshColor(false);
        ActualThemeVariantChanged += (_, _) => RefreshColor(false);
        Unloaded += (_, _) =>
        {
            PCL.Animations.AniStop(ColorKey);
            PCL.Animations.AniStop(TextKey);
        };
    }

    private string ColorKey => ButtonAnimation.Name("MyTextButton Color", Uuid);

    private string TextKey => ButtonAnimation.Name("MyTextButton Text", Uuid);

    public InlineCollection Inlines => LabText.Inlines!;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public event ClickEventHandler? Click;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
            _UpdateText(change.NewValue as string ?? string.Empty);
        else if (change.Property == IsEnabledProperty)
            RefreshColor(false);
    }

    /// <summary>
    ///     文本切换：先淡出，替换文本后再淡入；动画关闭时直接替换。
    /// </summary>
    private void _UpdateText(string text)
    {
        if (!ButtonAnimation.CanAnimate(this))
        {
            LabText.Text = text;
            return;
        }

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaOpacity(LabText, -LabText.Opacity, 50),
                PCL.Animations.AaCode(() => LabText.Text = text, after: true),
                PCL.Animations.AaOpacity(LabText, 1d, 170)
            },
            TextKey);
    }

    /// <summary>
    ///     按悬停/按下/禁用状态刷新文字颜色。
    ///     颜色落在内部 TextBlock 上，与 Styles/Controls/MyTextButton.axaml 的基线一致。
    /// </summary>
    private void RefreshColor(bool animate)
    {
        if (!IsLoaded)
            return;

        var (key, time) = !IsEnabled
            ? ("ColorBrush1", 200)
            : isMouseDown
                ? ("ColorBrush4", 30)
                : IsPointerOver
                    ? ("ColorBrush3", 100)
                    : ("ColorBrush1", 200);

        ButtonAnimation.ApplyColor(
            LabText,
            TextBlock.ForegroundProperty,
            key,
            null,
            time,
            animate,
            ColorKey);
    }

    private void _OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        isMouseDown = true;
        Classes.Set("pressed", true);
        RefreshColor(true);
        e.Handled = true;
    }

    private void _OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || !isMouseDown)
            return;

        isMouseDown = false;
        Classes.Set("pressed", false);
        RefreshColor(true);

        LogWrapper.Debug("[Control] 按下文本按钮：" + Text);
        Click?.Invoke(this, EventArgs.Empty);
        CustomEventService.Raise(this);
        ExecuteCommand();
        e.Handled = true;
    }

    private void _OnPointerExited(object? sender, PointerEventArgs e)
    {
        isMouseDown = false;
        Classes.Set("pressed", false);
        RefreshColor(true);
    }

    private void ExecuteCommand()
    {
        var command = Command;
        if (_isCommandRunning || command is null || !command.CanExecute(CommandParameter))
            return;
        _isCommandRunning = true;
        try
        {
            command.Execute(CommandParameter);
        }
        finally
        {
            _isCommandRunning = false;
        }
    }
}
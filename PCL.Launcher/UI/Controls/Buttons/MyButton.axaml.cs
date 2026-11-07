using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using PCL.Core.Logging;

namespace PCL.UI.Controls;

/// <summary>
///     PCL 标准按钮：外层 <see cref="Border" /> 负责命中测试与焦点，内层 <c>PanFore</c> 承载边框、底色与缩放。
///     颜色取自核心 <c>ColorBrush*</c> 动态资源；动画关闭时直接落到终值。
/// </summary>
public partial class MyButton : Border
{
    public delegate void ClickEventHandler(object? sender, PointerReleasedEventArgs e);

    public enum ColorState
    {
        Normal = 0,
        Highlight = 1,
        Red = 2
    }

    /// <summary>悬停/离开时的配色渐变时长（毫秒）。</summary>
    private const int AnimationColorIn = 100;

    private const int AnimationColorOut = 200;

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<Thickness> TextPaddingProperty =
        AvaloniaProperty.Register<MyButton, Thickness>(nameof(TextPadding));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MyButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MyButton, object?>(nameof(CommandParameter));

    /// <summary>
    ///     按钮内容的内边距，作用于内层 <c>PanFore</c>；外层不参与布局内缩。
    /// </summary>
    public new static readonly StyledProperty<Thickness> PaddingProperty =
        AvaloniaProperty.Register<MyButton, Thickness>(nameof(Padding));

    private static int _uuidSeed;

    /// <summary>动画键使用的唯一编号。</summary>
    public readonly int Uuid = Interlocked.Increment(ref _uuidSeed);

    private ColorState _colorType = ColorState.Normal;
    private bool _isCommandRunning;
    private bool _isMouseDown;

    public MyButton()
    {
        using var controlScope = PCL.Animations.SuspendControl();

        InitializeComponent();

        PanFore.Padding = Padding;
        LabText.Text = Text;
        LabText.Padding = TextPadding;
        LabText.Bind(
            TextBlock.ForegroundProperty,
            new Binding(nameof(BorderBrush))
            {
                Source = PanFore
            });
        _ApplyColorClasses();

        PointerPressed += _OnPointerPressed;
        PointerReleased += _OnPointerReleased;
        PointerEntered += (_, _) => RefreshColor(true);
        PointerExited += _OnPointerExited;

        Loaded += (_, _) => RefreshColor(false);
        ActualThemeVariantChanged += (_, _) => RefreshColor(false);
        Unloaded += (_, _) => _StopAnimations();
    }

    public InlineCollection Inlines => LabText.Inlines!;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>文本控件的内边距（<c>LabText.Padding</c>）。</summary>
    public Thickness TextPadding
    {
        get => GetValue(TextPaddingProperty);
        set => SetValue(TextPaddingProperty, value);
    }

    /// <summary>按钮内容的内边距（<c>PanFore.Padding</c>）。</summary>
    public new Thickness Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>配色方案：普通 / 高亮 / 红色。</summary>
    public ColorState ColorType
    {
        get => _colorType;
        set
        {
            if (_colorType == value)
                return;
            _colorType = value;
            _ApplyColorClasses();
            RefreshColor(true);
        }
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

    /// <summary>内层 <c>PanFore</c> 的渲染变换。</summary>
    public ITransform? RealRenderTransform
    {
        get => PanFore.RenderTransform;
        set => PanFore.RenderTransform = value;
    }

    private string ScaleKey => ButtonAnimation.Name("MyButton Scale", Uuid);

    private string BackgroundKey => ButtonAnimation.Name("MyButton Background", Uuid);

    private string BorderKey => ButtonAnimation.Name("MyButton Border", Uuid);

    public event ClickEventHandler? Click;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
            LabText.Text = change.NewValue as string ?? string.Empty;
        else if (change.Property == TextPaddingProperty)
            LabText.Padding = change.GetNewValue<Thickness>();
        else if (change.Property == PaddingProperty)
            PanFore.Padding = change.GetNewValue<Thickness>();
        else if (change.Property == IsEnabledProperty)
            RefreshColor(false);
    }

    private void _ApplyColorClasses()
    {
        Classes.Set("color-normal", _colorType == ColorState.Normal);
        Classes.Set("color-highlight", _colorType == ColorState.Highlight);
        Classes.Set("color-red", _colorType == ColorState.Red);
    }

    private (string Background, string Border) GetColorKeys()
    {
        if (!IsEnabled)
            return ("ColorBrushHalfWhite", "ColorBrushGray4");

        if (_colorType == ColorState.Red)
            return (IsPointerOver ? "ColorBrushRedBack" : "ColorBrushHalfWhite",
                IsPointerOver ? "ColorBrushRedLight" : "ColorBrushRedDark");

        if (_colorType == ColorState.Highlight)
            return (IsPointerOver ? "ColorBrush7" : "ColorBrushHalfWhite",
                IsPointerOver ? "ColorBrush3" : "ColorBrush2");

        return IsPointerOver
            ? ("ColorBrush7", "ColorBrush3")
            : ("ColorBrushHalfWhite", "ColorBrush1");
    }

    /// <summary>按当前悬停/按下/禁用状态刷新配色；<paramref name="animate" /> 为 false 时直接赋终值。</summary>
    private void RefreshColor(bool animate)
    {
        if (!IsLoaded)
            return;

        try
        {
            var (background, border) = GetColorKeys();
            var time = IsPointerOver ? AnimationColorIn : AnimationColorOut;

            ButtonAnimation.ApplyColor(
                PanFore,
                BackgroundProperty,
                background,
                null,
                _isMouseDown ? 30 : time,
                animate,
                BackgroundKey);
            ButtonAnimation.ApplyColor(
                PanFore,
                BorderBrushProperty,
                border,
                null,
                time,
                animate,
                BorderKey);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "MyButton", "刷新按钮颜色出错");
        }
    }

    private void _AnimateScale(
        double target,
        int time,
        int delay = 0,
        PCL.Animations.AniEase? ease = null)
    {
        ButtonAnimation.AnimateScale(PanFore, target, time, ScaleKey, delay, ease);
    }

    private void _StopAnimations()
    {
        PCL.Animations.AniStop(BackgroundKey);
        PCL.Animations.AniStop(BorderKey);
        PCL.Animations.AniStop(ScaleKey);
    }

    private void _OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isMouseDown = true;
        Focus();
        Classes.Set("pressed", true);
        RefreshColor(true);

        if (ButtonAnimation.CanAnimate(this))
            PCL.Animations.AniStart(
                new List<PCL.Animations.AniData>
                {
                    PCL.Animations.AaScaleTransform(
                        PanFore,
                        0.955d - ButtonAnimation.CurrentScale(PanFore),
                        80,
                        ease: new PCL.Animations.AniEaseOutFluent(
                            PCL.Animations.AniEasePower.ExtraStrong)),
                    PCL.Animations.AaScaleTransform(
                        PanFore,
                        -0.01d,
                        700,
                        ease: new PCL.Animations.AniEaseOutFluent())
                },
                ScaleKey);
        else
            ButtonAnimation.SetScale(PanFore, 0.955d);
    }

    private void _OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || !_isMouseDown)
            return;

        _isMouseDown = false;
        Classes.Set("pressed", false);
        _AnimateScale(1d, 300, 10, new PCL.Animations.AniEaseOutFluent());
        RefreshColor(true);

        LogWrapper.Debug("[Control] 按下按钮：" + Text);
        Click?.Invoke(this, e);
        CustomEventService.Raise(this);
        ExecuteCommand();
    }

    private void _OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isMouseDown = false;
        Classes.Set("pressed", false);
        _AnimateScale(
            1d,
            800,
            ease: new PCL.Animations.AniEaseOutFluent(PCL.Animations.AniEasePower.Strong));
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
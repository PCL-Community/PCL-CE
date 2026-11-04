using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using FluentValidation;
using PCL.UI.Controls.Behaviors;

namespace PCL.UI.Controls;

/// <summary>
///     带验证的输入文本框。
///     保留 <see cref="HintText" />（映射原生 PlaceholderText）、
///     <see cref="ValidateRules" />/<see cref="ValidateResult" />/<see cref="IsValidated" /> 验证体系、
///     <see cref="ValidateChanged" />/<see cref="ValidatedTextChanged" /> 事件与
///     <see cref="HasBackground" />/<see cref="CornerRadius" /> 视觉开关。
///     验证失败时在输入框下方展开错误文本层（模板中的 PART_Error）；
///     边框、底色与文字颜色经 <c>PCL.Animations</c> 过渡。
/// </summary>
public class MyTextBox : TextBox
{
    public delegate void ValidateChangedEventHandler(object sender, EventArgs e);

    private const int AnimationTimeOfFocus = 10;
    private const int AnimationTimeOfMouseIn = 100;
    private const int AnimationTimeOfMouseOut = 100;
    private const int AnimationTimeOfError = 200;
    private const int AnimationTimeOfDisabled = 200;
    private const int AnimationTimeOfValidate = 150;
    private const double ErrorLayerHeight = 21d;

    public static readonly StyledProperty<string> ValidateResultProperty =
        AvaloniaProperty.Register<MyTextBox, string>(
            nameof(ValidateResult),
            string.Empty);

    public static readonly DirectProperty<MyTextBox, bool> IsValidatedProperty =
        AvaloniaProperty.RegisterDirect<MyTextBox, bool>(
            nameof(IsValidated),
            textBox => textBox.IsValidated);

    public static readonly StyledProperty<string> HintTextProperty =
        AvaloniaProperty.Register<MyTextBox, string>(
            nameof(HintText),
            string.Empty);

    // 基础

    public int Uuid = InputTheme.NewUuid();

    private TextBlock? _errorText;
    private bool _isErrorVisible;
    private bool _isTextChangedFromUser;
    private bool _isValidated = true;
    private ValidateState _shownValidateResult = ValidateState.NotInited;
    private Collection<IValidator<string>> _validateRules = [];

    /// <summary>
    ///     额外控件初始化事件表，用于同步 TextChanged。
    /// </summary>
    public List<EventHandler<TextChangedEventArgs>> changedEventList = [];

    static MyTextBox()
    {
        ClipboardInterceptor.Enable();
    }

    public MyTextBox()
    {
        using (PCL.Animations.SuspendControl())
        {
            BorderThickness = new Thickness(1d);
            FontSize = 13d;
            MinHeight = 28d;
            CornerRadius = new CornerRadius(3d);
            VerticalContentAlignment = VerticalAlignment.Center;
            SelectionBrush = InputTheme.Accent(this);
        }

        TextChanged += OnTextChanged;

        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        GotFocus += (_, _) => RefreshColor();
        LostFocus += (_, _) => RefreshColor();
        Loaded += (_, _) =>
        {
            UpdateHintText();
            Validate();
            RefreshColor(false);
            RefreshTextColor(false);
        };
        Unloaded += (_, _) => StopAnimations();
    }

    private string BorderColorKey => InputAnimation.Key("MyTextBox BorderColor", Uuid);
    private string BackgroundColorKey => InputAnimation.Key("MyTextBox BackgroundColor", Uuid);
    private string TextColorKey => InputAnimation.Key("MyTextBox TextColor", Uuid);
    private string ErrorLayerKey => InputAnimation.Key("MyTextBox ErrorLayer", Uuid);

    // 自定义属性

    /// <summary>
    ///     是否绘制背景；为 <c>false</c> 时背景透明（用于内嵌输入）。
    /// </summary>
    public bool HasBackground { get; set; } = true;

    /// <summary>
    ///     验证失败时是否展示错误文本。
    /// </summary>
    public bool ShowValidateResult { get; set; } = true;

    /// <summary>
    ///     输入验证结果。若为空字符串则无错误，否则为第一个错误原因。
    /// </summary>
    public string ValidateResult
    {
        get => GetValue(ValidateResultProperty);
        set => SetValue(ValidateResultProperty, value);
    }

    /// <summary>
    ///     是否通过了输入验证。
    /// </summary>
    public bool IsValidated => _isValidated;

    /// <summary>
    ///     输入验证的规则。
    /// </summary>
    public Collection<IValidator<string>> ValidateRules
    {
        get => _validateRules;
        set
        {
            _validateRules = value ?? [];
            Validate();
        }
    }

    public string HintText
    {
        get => GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    /// <summary>
    ///     输入验证状态改变。
    /// </summary>
    public event ValidateChangedEventHandler? ValidateChanged;

    /// <summary>
    ///     通过验证的文本变化事件。
    /// </summary>
    public event EventHandler<TextChangedEventArgs>? ValidatedTextChanged
    {
        add
        {
            if (value is not null)
                changedEventList.Add(value);
        }
        remove
        {
            if (value is not null)
                changedEventList.Remove(value);
        }
    }

    // 模板

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // 错误文本由 ControlTheme 的 PART_Error 承载：与输入区一同参与模板测量，
        // 高度在 0 与 21 之间切换，布局随模板自动更新。
        _errorText = e.NameScope.Find<TextBlock>("PART_Error");
        _isErrorVisible = false;
        UpdateErrorLayer(false);
    }

    // 输入验证

    /// <summary>
    ///     进行输入验证。
    /// </summary>
    public void Validate()
    {
        var stringResult = GetValidateResult();
        ValidateResult = stringResult;

        var newState = IsValidated
            ? ValidateState.Success
            : _isTextChangedFromUser
                ? ValidateState.FailedAndShowDetail
                : ValidateState.FailedButTextNotChanged;

        var stateChanged = _shownValidateResult != newState;
        _shownValidateResult = newState;

        if (stateChanged)
            UpdateErrorLayer(true);

        RefreshColor();
        RefreshTextColor();

        if (stateChanged)
            ValidateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     强制显示结果为正常，类似尚未输入过文本的状态。不影响实际的检查结果。
    /// </summary>
    public void ForceShowAsSuccess()
    {
        _isTextChangedFromUser = false;
        _shownValidateResult = ValidateState.Success;
        UpdateErrorLayer(true);
        RefreshColor();
        RefreshTextColor();
        ValidateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateHintText();

        // 改变输入记录
        _isTextChangedFromUser = IsLoaded;

        // 进行输入验证
        Validate();
        if (!IsValidated)
            return;

        // 改变文本
        foreach (var handler in changedEventList.ToArray())
            handler(this, e);
    }

    private void UpdateHintText()
    {
        PlaceholderText = HintText;
    }

    private string GetValidateResult()
    {
        var stringResult = string.Empty;

        // 执行输入验证
        foreach (var rule in ValidateRules)
        {
            var result = rule.Validate(Text ?? string.Empty);
            stringResult = result.IsValid ? "" : result.Errors[0].ErrorMessage;
        }

        return stringResult;
    }

    private void UpdateErrorLayer(bool animate)
    {
        var shouldShow = ShowValidateResult && !IsValidated && _isTextChangedFromUser;
        Classes.Set("invalid", shouldShow);

        if (_errorText is null)
            return;

        if (shouldShow)
            ShowErrorLayer(animate);
        else
            HideErrorLayer(animate);
    }

    private void ShowErrorLayer(bool animate)
    {
        if (_errorText is not { } errorText)
            return;

        if (_isErrorVisible && Math.Abs(errorText.Height - ErrorLayerHeight) < 0.01d)
            return;

        _isErrorVisible = true;
        errorText.IsVisible = true;

        if (!animate || !InputAnimation.CanAnimate(this))
        {
            PCL.Animations.AniStop(ErrorLayerKey);
            errorText.Opacity = 1d;
            errorText.Height = ErrorLayerHeight;
            return;
        }

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaOpacity(
                    errorText,
                    1d - errorText.Opacity,
                    AnimationTimeOfValidate),
                PCL.Animations.AaHeight(
                    errorText,
                    ErrorLayerHeight - errorText.Height,
                    AnimationTimeOfValidate,
                    ease: new PCL.Animations.AniEaseOutFluent())
            },
            ErrorLayerKey);
    }

    private void HideErrorLayer(bool animate)
    {
        if (_errorText is not { } errorText)
            return;

        if (!_isErrorVisible)
        {
            if (!animate || !InputAnimation.CanAnimate(this))
            {
                PCL.Animations.AniStop(ErrorLayerKey);
                errorText.IsVisible = false;
                errorText.Opacity = 0d;
                errorText.Height = 0d;
            }

            return;
        }

        _isErrorVisible = false;

        if (!animate || !InputAnimation.CanAnimate(this))
        {
            PCL.Animations.AniStop(ErrorLayerKey);
            errorText.IsVisible = false;
            errorText.Opacity = 0d;
            errorText.Height = 0d;
            return;
        }

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaOpacity(errorText, -errorText.Opacity, AnimationTimeOfValidate),
                PCL.Animations.AaHeight(
                    errorText,
                    -errorText.Height,
                    AnimationTimeOfValidate,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                PCL.Animations.AaCode(() => errorText.IsVisible = false, after: true)
            },
            ErrorLayerKey);
    }

    // 颜色

    private void RefreshColor(bool animate = true)
    {
        try
        {
            IBrush borderFallback;
            IBrush backgroundFallback;
            string borderKey;
            string? backgroundKey = null;
            Color? backgroundLiteral = null;
            var transparentBackground = false;
            int time;

            if (!IsEnabled)
            {
                borderKey = InputTheme.DisabledBorderKey;
                borderFallback = InputTheme.TextSecondary(this);
                backgroundKey = InputTheme.DisabledBackgroundKey;
                backgroundFallback = InputTheme.DisabledBackground(this);
                time = AnimationTimeOfDisabled;
            }
            else if (IsValidated || !_isTextChangedFromUser)
            {
                if (IsFocused)
                {
                    borderKey = InputTheme.AccentKey;
                    borderFallback = InputTheme.Accent(this);
                    backgroundKey = InputTheme.AccentLightKey;
                    backgroundFallback = InputTheme.AccentLight(this);
                    time = AnimationTimeOfFocus;
                }
                else if (IsPointerOver)
                {
                    borderKey = InputTheme.AccentHoverKey;
                    borderFallback = InputTheme.AccentHover(this);
                    backgroundKey = InputTheme.AccentLightKey;
                    backgroundFallback = InputTheme.AccentLight(this);
                    time = AnimationTimeOfMouseIn;
                }
                else
                {
                    borderKey = InputTheme.IdleBorderKey;
                    borderFallback = InputTheme.IdleBorder(this);
                    backgroundKey = InputTheme.IdleBackgroundKey;
                    backgroundFallback = InputTheme.IdleBackground(this);
                    time = AnimationTimeOfMouseOut;
                }
            }
            else
            {
                borderKey = InputTheme.ErrorKey;
                borderFallback = InputTheme.Error(this);
                backgroundFallback = InputTheme.ErrorBackground(this);
                backgroundLiteral = ToColor(backgroundFallback);
                time = AnimationTimeOfError;
            }

            if (!HasBackground)
            {
                backgroundKey = null;
                transparentBackground = true;
                time = AnimationTimeOfError;
            }

            InputAnimation.Color(
                this,
                BorderBrushProperty,
                borderKey,
                borderFallback,
                time,
                BorderColorKey,
                animate);

            if (transparentBackground)
                InputAnimation.ColorLiteral(
                    this,
                    BackgroundProperty,
                    Colors.Transparent,
                    time,
                    BackgroundColorKey,
                    animate);
            else if (backgroundLiteral is { } literal)
                InputAnimation.ColorLiteral(
                    this,
                    BackgroundProperty,
                    literal,
                    time,
                    BackgroundColorKey,
                    animate);
            else if (backgroundKey is not null)
                InputAnimation.Color(
                    this,
                    BackgroundProperty,
                    backgroundKey,
                    backgroundFallback,
                    time,
                    BackgroundColorKey,
                    animate);

            SelectionBrush = InputTheme.Accent(this);
        }
        catch (Exception ex)
        {
            InputTheme.LogDebug(ex, "文本框颜色改变出错");
        }
    }

    private static Color ToColor(IBrush? brush)
    {
        return brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
    }

    private void RefreshTextColor(bool animate = true)
    {
        var key = IsEnabled ? InputTheme.TextKey : InputTheme.TextSecondaryKey;
        var fallback = IsEnabled ? InputTheme.Text(this) : InputTheme.TextSecondary(this);
        InputAnimation.Color(
            this,
            ForegroundProperty,
            key,
            fallback,
            AnimationTimeOfDisabled,
            TextColorKey,
            animate && !string.IsNullOrEmpty(Text));
    }

    private void StopAnimations()
    {
        InputAnimation.Stop(BorderColorKey, BackgroundColorKey, TextColorKey, ErrorLayerKey);
    }

    /// <summary>
    ///     在按下回车时触发自定义事件。
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

        if (change.Property == ValidateResultProperty)
        {
            SetAndRaise(
                IsValidatedProperty,
                ref _isValidated,
                string.IsNullOrEmpty(change.NewValue as string));
            RefreshTextColor();
        }
        else if (change.Property == HintTextProperty)
        {
            UpdateHintText();
        }
        else if (change.Property == IsEnabledProperty)
        {
            RefreshColor();
            RefreshTextColor();
        }
    }

    // 主题

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        RefreshColor(false);
        RefreshTextColor(false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        StopAnimations();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        RefreshColor(false);
        RefreshTextColor(false);
    }

    private enum ValidateState
    {
        NotInited,
        Success,
        FailedButTextNotChanged,
        FailedAndShowDetail,
        FailedAndHideDetail,
        NotLoaded
    }
}
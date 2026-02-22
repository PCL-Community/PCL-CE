using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL;

public class MyTextBox : TextBox
{
    public delegate void ValidateChangedEventHandler(object sender, EventArgs e);

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register("CornerRadius",
        typeof(CornerRadius), typeof(MyTextBox), new PropertyMetadata(new CornerRadius(3d)));

    public static readonly DependencyProperty ValidateResultProperty = DependencyProperty.Register("ValidateResult",
        typeof(string), typeof(MyTextBox),
        new PropertyMetadata("",
            (d, e) => d.SetValue(IsValidatedPropertyKey,
                string.IsNullOrEmpty(Conversions.ToString(e.NewValue)))));

    private static readonly DependencyPropertyKey IsValidatedPropertyKey =
        DependencyProperty.RegisterReadOnly("IsValidated", typeof(bool), typeof(MyTextBox), new PropertyMetadata(true));

    public static readonly DependencyProperty IsValidatedProperty = IsValidatedPropertyKey.DependencyProperty;

    public static readonly DependencyProperty HintTextProperty = DependencyProperty.Register("HintText", typeof(string),
        typeof(MyTextBox), new PropertyMetadata("", (t, e) =>
        {
            if (((dynamic)t).labHint is not null) ((dynamic)t).labHint.Text = string.IsNullOrEmpty(((dynamic)t).Text) ? ((dynamic)t).HintText : "";
        }));

    private TextBlock _labHint;

    // 额外控件初始化

    private TextBlock _labWrong;
    private Collection<ValidateType> _ValidateRules = new();
    public List<RoutedEventHandler> ChangedEventList = new();

    // 提示文本

    /// <summary>
    ///     是否已经由用户输入过文本，若尚未输入过，则不显示输入检查的失败。
    /// </summary>
    private bool IsTextChanged;

    private ValidateState ShownValidateResult = ValidateState.NotInited;

    // 事件

    public int Uuid = ModBase.GetUuid();

    public MyTextBox()
    {
        Loaded += (_, __) => Validate();
        TextChanged += (a, b) => MyTextBox_TextChanged((dynamic)a, b);
        IsEnabledChanged += (_, __) => RefreshColor();
        MouseEnter += (_, __) => RefreshColor();
        MouseLeave += (_, __) => RefreshColor();
        GotFocus += (_, __) => RefreshColor();
        LostFocus += (_, __) => RefreshColor();
        IsEnabledChanged += (_, __) => RefreshTextColor();
    }

    // 自定义属性

    public bool HasBackground { get; set; } = true;
    public bool ShowValidateResult { get; set; } = true;

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set
        {
            if (value == null) return;
            SetValue(CornerRadiusProperty, value);
        }
    }

    private TextBlock labWrong
    {
        get
        {
            if (Template is null)
                return null;
            if (_labWrong is null)
                _labWrong = (TextBlock)Template.FindName("labWrong", this);
            return _labWrong;
        }
    }

    private TextBlock labHint
    {
        get
        {
            if (Template is null)
                return null;
            if (_labHint is null)
                _labHint = (TextBlock)Template.FindName("labHint", this);
            return _labHint;
        }
    }

    // 输入验证

    /// <summary>
    ///     输入验证结果。若为空字符串则无错误，否则为第一个错误原因。
    /// </summary>
    public string ValidateResult
    {
        get => Conversions.ToString(GetValue(ValidateResultProperty));
        set => SetValue(ValidateResultProperty, value);
    }

    /// <summary>
    ///     是否通过了输入验证。
    /// </summary>
    public bool IsValidated => Conversions.ToBoolean(GetValue(IsValidatedProperty));

    /// <summary>
    ///     输入验证的规则。
    /// </summary>
    public Collection<ValidateType> ValidateRules
    {
        get => _ValidateRules;
        set
        {
            _ValidateRules = value;
            Validate();
        }
    }

    public string HintText
    {
        get => Conversions.ToString(GetValue(HintTextProperty));
        set => SetValue(HintTextProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (string.IsNullOrEmpty(HintText) || !string.IsNullOrEmpty(labHint.Text))
            return;
        labHint.Text = string.IsNullOrEmpty(Text) ? HintText : "";
    }

    public static event ValidateChangedEventHandler ValidateChanged;

    public event RoutedEventHandler ValidatedTextChanged
    {
        add => ChangedEventList.Add(value);
        remove => ChangedEventList.Remove(value);
    }

    private void OnValidatedTextChanged(object sender, TextChangedEventArgs e)
    {
        foreach (var handler in ChangedEventList)
            if (!(handler == null))
                handler.Invoke(sender, e);
    }

    /// <summary>
    ///     进行输入验证。
    /// </summary>
    public void Validate()
    {
        // 执行输入验证
        ValidateResult = ModValidate.Validate(Text, ValidateRules);
        // 根据结果改变样式
        if (ShownValidateResult != (IsValidated ? ValidateState.Success : ValidateState.FailedAndShowDetail))
        {
            if (IsLoaded && labWrong is not null)
                ChangeValidateResult(IsValidated, true);
            else
                ModBase.RunInNewThread(() =>
                {
                    Thread.Sleep(30);
                    ModBase.RunInUi(() => ChangeValidateResult(IsValidated, false));
                }, "DelayedValidate Change");
        }

        // 更新错误信息
        if (ShowValidateResult && !IsValidated)
        {
            if (IsLoaded && labWrong is not null)
                labWrong.Text = ValidateResult;
            else
                ModBase.RunInNewThread(() =>
                {
                    var IsFinished = false;
                    while (!IsFinished)
                    {
                        Thread.Sleep(20);
                        ModBase.RunInUiWait(() =>
                        {
                            if (labWrong is not null)
                            {
                                labWrong.Text = ValidateResult;
                                IsFinished = true;
                            }

                            if (!IsLoaded)
                                IsFinished = true;
                        });
                    }
                }, "DelayedValidate Text");
        }
    }

    /// <summary>
    ///     强制显示结果为正常，类似尚未输入过文本的状态。不影响实际的检查结果。
    /// </summary>
    public void ForceShowAsSuccess()
    {
        IsTextChanged = false;
        ChangeValidateResult(IsValidated, true);
    }

    private void ChangeValidateResult(bool IsSuccessful, bool IsLoaded)
    {
        if (IsLoaded && ModAnimation.AniControlEnabled == 0 && labWrong is not null)
        {
            if (IsSuccessful || !IsTextChanged)
            {
                // 变为正确
                ShownValidateResult = IsSuccessful ? ValidateState.Success : ValidateState.FailedButTextNotChanged;
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaOpacity(labWrong, -labWrong.Opacity, 150),
                        ModAnimation.AaHeight(labWrong, -labWrong.Height, 150,
                            Ease: new ModAnimation.AniEaseOutFluent()),
                        ModAnimation.AaCode(() => labWrong.Visibility = Visibility.Collapsed, After: true)
                    }, "MyTextBox Validate " + Uuid);
            }
            else if (ShowValidateResult)
            {
                // 变为错误
                ShownValidateResult = ValidateState.FailedAndShowDetail;
                labWrong.Visibility = Visibility.Visible;
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaOpacity(labWrong, 1d - labWrong.Opacity, 150),
                        ModAnimation.AaHeight(labWrong, 21d - labWrong.Height, 150,
                            Ease: new ModAnimation.AniEaseOutFluent())
                    }, "MyTextBox Validate " + Uuid);
            }
            else
            {
                // 变为错误，但不显示文本
                ShownValidateResult = ValidateState.FailedAndHideDetail;
            }
        }
        else
        {
            ShownValidateResult = ValidateState.NotLoaded;
        }

        RefreshColor();
        ValidateChanged?.Invoke(this, new EventArgs());
    }

    private void MyTextBox_TextChanged(MyTextBox sender, TextChangedEventArgs e)
    {
        try
        {
            // 改变提示文本
            if (labHint is not null)
                labHint.Text = string.IsNullOrEmpty(Text) ? HintText : "";
            // 改变输入记录
            IsTextChanged = IsLoaded;
            // 进行输入验证
            Validate();
            if (!IsValidated)
                return;
            // 改变文本
            OnValidatedTextChanged(sender, e);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "进行输入验证时出错", ModBase.LogLevel.Critical);
        }
    }

    // 颜色

    private void RefreshColor()
    {
        try
        {
            // 不对 ComboBox 从属进行动画
            if (TemplatedParent is not null && TemplatedParent is MyComboBox)
                return;
            // 判断当前颜色
            string ForeColorName;
            string BackColorName;
            int AnimationTime;
            if (IsEnabled)
            {
                if (IsValidated || !IsTextChanged)
                {
                    if (IsFocused)
                    {
                        ForeColorName = "ColorBrush3";
                        BackColorName = "ColorBrush7";
                        AnimationTime = 10;
                    }
                    else if (IsMouseOver)
                    {
                        ForeColorName = "ColorBrush4";
                        BackColorName = "ColorBrush7";
                        AnimationTime = 100;
                    }
                    else // 未选中
                    {
                        ForeColorName = "ColorBrushBg0";
                        BackColorName = "ColorBrushHalfWhite";
                        AnimationTime = 100;
                    }
                }
                else
                {
                    ForeColorName = "ColorBrushRedLight";
                    BackColorName = "ColorBrushRedBack";
                    AnimationTime = 200;
                }
            }
            else
            {
                ForeColorName = "ColorBrushGray5";
                BackColorName = "ColorBrushGray6";
                AnimationTime = 200;
            }

            if (!HasBackground)
                BackColorName = "ColorBrushTransparent";
            // 触发颜色动画
            if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
            {
                // 有动画
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaColor(this, BorderBrushProperty, ForeColorName, AnimationTime),
                        ModAnimation.AaColor(this, BackgroundProperty, BackColorName, AnimationTime)
                    }, "MyTextBox Color " + Uuid);
            }
            else
            {
                // 无动画
                ModAnimation.AniStop("MyTextBox Color " + Uuid);
                SetResourceReference(BorderBrushProperty, ForeColorName);
                SetResourceReference(BackgroundProperty, BackColorName);
            }
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "文本框颜色改变出错");
        }
    }

    private void RefreshTextColor()
    {
        var NewColor = IsEnabled ? ModSecret.ColorGray1 : ModSecret.ColorGray4;
        if (((SolidColorBrush)Foreground).Color.R == NewColor.R)
            return;
        if (IsLoaded && ModAnimation.AniControlEnabled == 0 && !string.IsNullOrEmpty(Text))
        {
            // 有动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(this, ForegroundProperty, IsEnabled ? "ColorBrushGray1" : "ColorBrushGray4",
                        200)
                }, "MyTextBox TextColor " + Uuid);
        }
        else
        {
            // 无动画
            ModAnimation.AniStop("MyTextBox TextColor " + Uuid);
            Foreground = NewColor;
        }
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
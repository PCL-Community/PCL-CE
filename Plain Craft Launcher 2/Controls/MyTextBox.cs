using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FluentValidation;

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
                string.IsNullOrEmpty((string)e.NewValue))));

    private static readonly DependencyPropertyKey IsValidatedPropertyKey =
        DependencyProperty.RegisterReadOnly("IsValidated", typeof(bool), typeof(MyTextBox), new PropertyMetadata(true));

    public static readonly DependencyProperty IsValidatedProperty = IsValidatedPropertyKey.DependencyProperty;

    public static readonly DependencyProperty HintTextProperty = DependencyProperty.Register("HintText", typeof(string),
        typeof(MyTextBox), new PropertyMetadata("", (t, e) =>
        {
            var textBox = (MyTextBox)t;
            textBox.UpdateHintText();
        }));

    // 额外控件初始化

    public List<RoutedEventHandler> changedEventList = new();

    // 提示文本

    /// <summary>
    ///     是否已经由用户输入过文本，若尚未输入过，则不显示输入检查的失败。
    /// </summary>
    private bool isTextChanged;

    private ValidateState shownValidateResult = ValidateState.NotInited;

    // 事件

    public int Uuid = ModBase.GetUuid();

    public MyTextBox()
    {
        Loaded += (_, _) => Validate();
        TextChanged += (a, b) => MyTextBox_TextChanged((MyTextBox)a, b);
        IsEnabledChanged += (_, _) => RefreshColor();
        MouseEnter += (_, _) => RefreshColor();
        MouseLeave += (_, _) => RefreshColor();
        GotFocus += (_, _) => RefreshColor();
        LostFocus += (_, _) => RefreshColor();
        IsEnabledChanged += (_, _) => RefreshTextColor();
    }

    // 自定义属性

    public bool HasBackground { get; set; } = true;
    public bool ShowValidateResult { get; set; } = true;

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    private TextBlock labWrong
    {
        get
        {
            if (Template is null) return null;
            if (field is null) field = (TextBlock)Template.FindName("labWrong", this);
            return field;
        }
    }

    private TextBlock labHint
    {
        get
        {
            if (Template is null) return null;
            if (field is null) field = (TextBlock)Template.FindName("labHint", this);
            return field;
        }
    }

    // 输入验证

    /// <summary>
    ///     输入验证结果。若为空字符串则无错误，否则为第一个错误原因。
    /// </summary>
    public string ValidateResult
    {
        get => (string)GetValue(ValidateResultProperty);
        set => SetValue(ValidateResultProperty, value);
    }

    /// <summary>
    ///     是否通过了输入验证。
    /// </summary>
    public bool IsValidated => (bool)GetValue(IsValidatedProperty);

    /// <summary>
    ///     输入验证的规则。
    /// </summary>
    public Collection<IValidator<string>> ValidateRules
    {
        get => field;
        set
        {
            field = value;
            Validate();
        }
    } = new();

    public string HintText
    {
        get => (string)GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (string.IsNullOrEmpty(HintText) || !string.IsNullOrEmpty(labHint.Text))
            return;
        UpdateHintText();
    }

    public event ValidateChangedEventHandler? ValidateChanged;

    public event RoutedEventHandler ValidatedTextChanged
    {
        add => changedEventList.Add(value);
        remove => changedEventList.Remove(value);
    }

    private void OnValidatedTextChanged(object sender, TextChangedEventArgs e)
    {
        foreach (var handler in changedEventList)
            if (handler is not null)
                handler.Invoke(sender, e);
    }

    /// <summary>
    ///     进行输入验证。
    /// </summary>
    public void Validate()
    {
        var stringResult = GetValidateResult();

        ValidateResult = stringResult;
        // 根据结果改变样式
        if (shownValidateResult != (IsValidated ? ValidateState.Success : ValidateState.FailedAndShowDetail))
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
                    var isFinished = false;
                    while (!isFinished)
                    {
                        Thread.Sleep(20);
                        ModBase.RunInUiWait(() =>
                        {
                            if (labWrong is not null)
                            {
                                labWrong.Text = ValidateResult;
                                isFinished = true;
                            }

                            if (!IsLoaded)
                                isFinished = true;
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
        isTextChanged = false;
        ChangeValidateResult(IsValidated, true);
    }

    private void ChangeValidateResult(bool isSuccessful, bool isLoaded)
    {
        if (isLoaded && ModAnimation.AniControlEnabled == 0 && labWrong is not null)
        {
            if (isSuccessful || !isTextChanged)
            {
                // 变为正确
                shownValidateResult = isSuccessful ? ValidateState.Success : ValidateState.FailedButTextNotChanged;
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaOpacity(labWrong, -labWrong.Opacity, 150),
                        ModAnimation.AaHeight(labWrong, -labWrong.Height, 150,
                            ease: new ModAnimation.AniEaseOutFluent()),
                        ModAnimation.AaCode(() => labWrong.Visibility = Visibility.Collapsed, after: true)
                    }, "MyTextBox Validate " + Uuid);
            }
            else if (ShowValidateResult)
            {
                // 变为错误
                shownValidateResult = ValidateState.FailedAndShowDetail;
                labWrong.Visibility = Visibility.Visible;
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaOpacity(labWrong, 1d - labWrong.Opacity, 150),
                        ModAnimation.AaHeight(labWrong, 21d - labWrong.Height, 150,
                            ease: new ModAnimation.AniEaseOutFluent())
                    }, "MyTextBox Validate " + Uuid);
            }
            else
            {
                // 变为错误，但不显示文本
                shownValidateResult = ValidateState.FailedAndHideDetail;
            }
        }
        else
        {
            shownValidateResult = ValidateState.NotLoaded;
        }

        RefreshColor();
        ValidateChanged?.Invoke(this, new EventArgs());
    }

    private void MyTextBox_TextChanged(MyTextBox sender, TextChangedEventArgs e)
    {
        try
        {
            UpdateHintText();
            // 改变输入记录
            isTextChanged = IsLoaded;
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

    private void UpdateHintText()
    {
        if (labHint is null)
            return;
        labHint.Text = string.IsNullOrEmpty(Text) ? HintText : "";
    }

    private string GetValidateResult()
    {
        var stringResult = string.Empty;
        // 执行输入验证
        foreach (var rule in ValidateRules)
        {
            var result = rule.Validate(Text);
            stringResult = result.IsValid ? "" : result.Errors[0].ErrorMessage;
        }

        return stringResult;
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
            string foreColorName;
            string backColorName;
            int animationTime;
            if (IsEnabled)
            {
                if (IsValidated || !isTextChanged)
                {
                    if (IsFocused)
                    {
                        foreColorName = "ColorBrush3";
                        backColorName = "ColorBrush7";
                        animationTime = 10;
                    }
                    else if (IsMouseOver)
                    {
                        foreColorName = "ColorBrush4";
                        backColorName = "ColorBrush7";
                        animationTime = 100;
                    }
                    else // 未选中
                    {
                        foreColorName = "ColorBrushBg0";
                        backColorName = "ColorBrushHalfWhite";
                        animationTime = 100;
                    }
                }
                else
                {
                    foreColorName = "ColorBrushRedLight";
                    backColorName = "ColorBrushRedBack";
                    animationTime = 200;
                }
            }
            else
            {
                foreColorName = "ColorBrushGray5";
                backColorName = "ColorBrushGray6";
                animationTime = 200;
            }

            if (!HasBackground)
                backColorName = "ColorBrushTransparent";
            // 触发颜色动画
            if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
            {
                // 有动画
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaColor(this, BorderBrushProperty, foreColorName, animationTime),
                        ModAnimation.AaColor(this, BackgroundProperty, backColorName, animationTime)
                    }, "MyTextBox Color " + Uuid);
            }
            else
            {
                // 无动画
                ModAnimation.AniStop("MyTextBox Color " + Uuid);
                SetResourceReference(BorderBrushProperty, foreColorName);
                SetResourceReference(BackgroundProperty, backColorName);
            }
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "文本框颜色改变出错");
        }
    }

    private void RefreshTextColor()
    {
        var newColor = IsEnabled ? ThemeManager.colorGray1 : ThemeManager.colorGray4;
        if (((SolidColorBrush)Foreground).Color.R == newColor.r)
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
            Foreground = newColor;
        }
    }

    // 在按下回车时触发自定义事件
    private void MyTextBox_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ModMain.RaiseCustomEvent(this);
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

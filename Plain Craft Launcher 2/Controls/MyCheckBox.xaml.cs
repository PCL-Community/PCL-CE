using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace PCL;

[ContentProperty("Inlines")]
public partial class MyCheckBox
{
    public delegate void ChangeEventHandler(object sender, bool user);

    public delegate void PreviewChangeEventHandler(object sender, ModBase.RouteEventArgs e);

    // 在使用 XAML 设置 Checked 属性时，不会触发 Checked_Set 方法，所以需要在这里手动触发 UI 改变
    public static readonly DependencyProperty CheckedProperty = DependencyProperty.Register("Checked", typeof(bool?),
        typeof(MyCheckBox), new PropertyMetadata(false, (d, e) =>
        {
            var obj = (MyCheckBox)d;
            if (!obj.IsLoaded) obj.SyncUI();
        }));

    /// <summary>
    ///     是否为三态复选框。
    /// </summary>
    public static readonly DependencyProperty IsThreeStateProperty =
        DependencyProperty.Register("IsThreeState", typeof(bool), typeof(MyCheckBox), new PropertyMetadata(false));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyCheckBox), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyCheckBox)sender).LabText.Text = (string)e.NewValue;
        }));

    // 点击事件

    private bool mouseDowned;

    // 基础

    public int Uuid = ModBase.GetUuid();

    public MyCheckBox()
    {
        InitializeComponent();

        MouseLeftButtonUp += (_, _) => Checkbox_MouseUp();
        MouseLeftButtonDown += (_, _) => Checkbox_MouseDown();
        MouseLeave += (_, _) => Checkbox_MouseLeave();
        IsEnabledChanged += (_, _) => Checkbox_IsEnabledChanged();
        MouseEnter += (_, _) => Checkbox_MouseEnterStyle();
        MouseLeave += (_, _) => Checkbox_MouseLeaveStyle();
    }

    // 自定义属性
    public bool? Checked
    {
        get => (bool?)GetValue(CheckedProperty);
        set => SetChecked(value, false);
    }

    public bool IsThreeState
    {
        get => (bool)GetValue(IsThreeStateProperty);
        set => SetValue(IsThreeStateProperty, value);
    } // 是否为三态复选框

    public InlineCollection Inlines => LabText.Inlines;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    } // 内容

    /// <summary>
    ///     复选框勾选状态改变。
    /// </summary>
    /// <param name="user">是否为用户手动改变的勾选状态。</param>
    public event ChangeEventHandler? Change;

    public event PreviewChangeEventHandler? PreviewChange;

    /// <summary>
    ///     手动设置 Checked 属性。
    /// </summary>
    /// <param name="value">新的 Checked 属性。</param>
    /// <param name="user">是否由用户引发。</param>
    public void SetChecked(bool? value, bool user)
    {
        try
        {
            if (Checked.HasValue && value.HasValue && Checked.Value == value.Value)
                return;

            // Preview 事件
            if (value.HasValue && value.Value && user)
            {
                var e = new ModBase.RouteEventArgs(user);
                PreviewChange?.Invoke(this, e);
                if (e.handled)
                {
                    mouseDowned = true;
                    Checkbox_MouseLeave();
                    mouseDowned = false;
                    return;
                }
            }

            // 判断真实勾选状态
            var isChecked = GetFinalState(value, IsThreeState);

            SetValue(CheckedProperty, isChecked);
            if (IsLoaded)
                Change?.Invoke(this, user);

            // 更改显示
            SyncUI();
            ModMain.RaiseCustomEvent(this);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "设置 Checked 失败");
        }
    }

    private void SyncUI()
    {
        var isChecked = GetFinalState(Checked, IsThreeState);

        var checkScale = isChecked == true ? 1d : 0d;
        ((ScaleTransform)ShapeCheck.RenderTransform).ScaleX = checkScale;
        ((ScaleTransform)ShapeCheck.RenderTransform).ScaleY = checkScale;

        var indeterminateScale = isChecked is null ? 1d : 0d;
        ((ScaleTransform)ShapeIndeterminate.RenderTransform).ScaleX = indeterminateScale;
        ((ScaleTransform)ShapeIndeterminate.RenderTransform).ScaleY = indeterminateScale;

        ShapeBorder.SetResourceReference(Border.BorderBrushProperty,
            !IsEnabled
                ? "ColorBrushGray4"
                : IsMouseOver
                    ? "ColorBrush3"
                    : isChecked == true
                        ? "ColorBrush2"
                        : "ColorBrush1");
    }

    private void Checkbox_MouseUp()
    {
        if (!mouseDowned)
            return;
        ModBase.Log("[Control] 按下复选框（" + !Checked + "）：" + Text);
        mouseDowned = false;
        if (IsThreeState)
        {
            switch (Checked)
            {
                case true:
                    SetChecked(null, true);
                    break;
                case false:
                    SetChecked(true, true);
                    break;
                case null:
                    SetChecked(false, true);
                    break;
            }

            ShapeBorder.SetResourceReference(Border.BackgroundProperty, "ColorBrushHalfWhite");
            return;
        }

        SetChecked(!Checked, true);
        ShapeBorder.SetResourceReference(Border.BackgroundProperty, "ColorBrushHalfWhite");
    }

    private void Checkbox_MouseDown()
    {
        mouseDowned = true;
        Focus();
        ShapeBorder.SetResourceReference(Border.BackgroundProperty, "ColorBrushBg1");
    }

    private void Checkbox_MouseLeave()
    {
        if (!mouseDowned)
            return;
        mouseDowned = false;
        ShapeBorder.SetResourceReference(Border.BackgroundProperty, "ColorBrushHalfWhite");
    }

    private void Checkbox_IsEnabledChanged()
    {
        LabText.SetResourceReference(TextBlock.ForegroundProperty, IsEnabled ? "ColorBrush1" : "ColorBrushGray4");
        ShapeBorder.SetResourceReference(Border.BorderBrushProperty,
            IsEnabled ? Checked == true ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4");
    }

    private void Checkbox_MouseEnterStyle()
    {
        if (!IsEnabled)
            return;
        LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3");
        ShapeBorder.SetResourceReference(Border.BorderBrushProperty, "ColorBrush3");
    }

    private void Checkbox_MouseLeaveStyle()
    {
        if (!IsEnabled)
            return; // MouseLeave 比 IsEnabledChanged 后执行，所以如果自定义事件修改了 IsEnabled，将导致显示错误
        LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");
        ShapeBorder.SetResourceReference(Border.BorderBrushProperty, Checked == true ? "ColorBrush2" : "ColorBrush1");
    }

    private static bool? GetFinalState(bool? value, bool isThreeState)
    {
        return isThreeState ? value : value == true;
    }
}

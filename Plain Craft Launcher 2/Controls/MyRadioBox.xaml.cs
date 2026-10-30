using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Shapes;

using PCL.Core.App.Localization;
namespace PCL;

[ContentProperty("Inlines")]
public partial class MyRadioBox : IMyRadio
{
    public delegate void PreviewChangeEventHandler(object sender, ModBase.RouteEventArgs e);

    public delegate void PreviewCheckEventHandler(object sender, ModBase.RouteEventArgs e);

    // 在使用 XAML 设置 Checked 属性时，不会触发 Checked_Set 方法，所以需要在这里手动触发 UI 改变
    public static readonly DependencyProperty CheckedProperty = DependencyProperty.Register("Checked", typeof(bool),
        typeof(MyRadioBox), new PropertyMetadata(false, (dRaw, e) =>
        {
            var d = (MyRadioBox)dRaw;
            if (!d.IsLoaded) d.SyncUI();
        }));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyRadioBox), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyRadioBox)sender).LabText.Text = (string)e.NewValue;
        }));

    // 点击事件

    private bool mouseDowned;

    // 基础

    public int Uuid = ModBase.GetUuid();

    public MyRadioBox()
    {
        InitializeComponent();
        MouseLeftButtonUp += (_, _) => Radiobox_MouseUp();
        MouseLeftButtonDown += (_, _) => Radiobox_MouseDown();
        MouseLeave += (_, _) => Radiobox_MouseLeave();
        IsEnabledChanged += (_, _) => Radiobox_IsEnabledChanged();
        MouseEnter += (_, _) => Radiobox_MouseEnterStyle();
        MouseLeave += (_, _) => Radiobox_MouseLeaveStyle();
    }

    // 自定义属性
    public bool Checked
    {
        get => (bool)GetValue(CheckedProperty);
        set => SetChecked(value, false);
    }

    public InlineCollection Inlines => LabText.Inlines;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    } // 内容

    public event IMyRadio.CheckEventHandler? Check;
    public event IMyRadio.ChangedEventHandler? Changed;
    public event PreviewCheckEventHandler? PreviewCheck;
    public event PreviewChangeEventHandler? PreviewChange;

    /// <summary>
    ///     手动设置 Checked 属性。
    /// </summary>
    /// <param name="value">新的 Checked 属性。</param>
    /// <param name="user">是否由用户引发。</param>
    public void SetChecked(bool value, bool user)
    {
        try
        {
            // Preview 事件
            if (value && user)
            {
                var e = new ModBase.RouteEventArgs(user);
                PreviewCheck?.Invoke(this, e);
                if (e.handled)
                {
                    Radiobox_MouseLeave();
                    return;
                }
            }

            // 自定义属性基础
            var isChanged = false;
            if (IsLoaded && value != Checked)
                PreviewChange?.Invoke(this, new ModBase.RouteEventArgs(user));
            if (value != Checked)
            {
                SetValue(CheckedProperty, value);
                isChanged = true;
            }

            // 保证只有一个单选框选中
            if (Parent is null)
                return;
            var radioboxList = new List<MyRadioBox>();
            var checkedCount = 0;
            foreach (var Control in ((Panel)Parent).Children) // 收集控件列表与选中个数
                if (Control is MyRadioBox radioBox)
                {
                    radioboxList.Add(radioBox);
                    if (radioBox.Checked)
                        checkedCount += 1;
                }

            switch (checkedCount) // 判断选中情况
            {
                case 0:
                {
                    // 没有任何单选框被选中，选择第一个
                    radioboxList[0].Checked = true;
                    break;
                }
                case var @case when @case > 1:
                {
                    // 选中项目多于 1 个
                    if (Checked)
                    {
                        // 如果本控件选中，则取消其他所有控件的选中
                        foreach (var Control in radioboxList)
                            if (Control.Checked && !Control.Equals(this))
                                Control.Checked = false;
                    }
                    else
                    {
                        // 如果本控件未选中，则只保留第一个选中的控件
                        var firstChecked = false;
                        foreach (var Control in radioboxList)
                            if (Control.Checked)
                            {
                                if (firstChecked)
                                    Control.Checked = false; // 修改 Checked 会自动触发 Change 事件，所以不用额外触发
                                else
                                    firstChecked = true;
                            }
                    }

                    break;
                }
            }

            // 触发事件
            if (isChanged)
            {
                if (Checked)
                    Check?.Invoke(this, new ModBase.RouteEventArgs(user));
                Changed?.Invoke(this, new ModBase.RouteEventArgs(user));
                ModMain.RaiseCustomEvent(this);
            }

            // 更改显示
            SyncUI();
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "单选框勾选改变错误",
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.Error.OperationFailed"));
        }
    }

    private void SyncUI()
    {
        if (Checked)
        {
            ShapeDot.Width = 9d;
            ShapeDot.Height = 9d;
            ShapeDot.Opacity = 1d;
            ShapeDot.Margin = new Thickness(5.5d, 0d, 0d, 0d);
        }
        else
        {
            ShapeDot.Width = 0d;
            ShapeDot.Height = 0d;
            ShapeDot.Opacity = 0d;
            ShapeDot.Margin = new Thickness(10d, 0d, 0d, 0d);
        }

        ShapeBorder.SetResourceReference(Shape.StrokeProperty,
            !IsEnabled
                ? "ColorBrushGray4"
                : IsMouseOver
                    ? "ColorBrush3"
                    : Checked
                        ? "ColorBrush2"
                        : "ColorBrush1");
    }

    private void Radiobox_MouseUp()
    {
        if (!mouseDowned)
            return;
        ModBase.Log("[Control] 按下单选框：" + Text);
        SetChecked(true, true);
        mouseDowned = false;
        ShapeBorder.SetResourceReference(Shape.FillProperty, "ColorBrushHalfWhite");
    }

    private void Radiobox_MouseDown()
    {
        mouseDowned = true;
        Focus();
        ShapeBorder.SetResourceReference(Shape.FillProperty, "ColorBrushBg1");
    }

    private void Radiobox_MouseLeave()
    {
        if (!mouseDowned)
            return;
        mouseDowned = false;
        ShapeBorder.SetResourceReference(Shape.FillProperty, "ColorBrushHalfWhite");
    }

    private void Radiobox_IsEnabledChanged()
    {
        LabText.SetResourceReference(TextBlock.ForegroundProperty, IsEnabled ? "ColorBrush1" : "ColorBrushGray4");
        ShapeBorder.SetResourceReference(Shape.StrokeProperty,
            IsEnabled ? Checked ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4");
    }

    private void Radiobox_MouseEnterStyle()
    {
        if (!IsEnabled)
            return;
        ShapeBorder.SetResourceReference(Shape.StrokeProperty, "ColorBrush3");
        LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3");
    }

    private void Radiobox_MouseLeaveStyle()
    {
        if (!IsEnabled)
            return; // MouseLeave 比 IsEnabledChanged 后执行，所以如果自定义事件修改了 IsEnabled，将导致显示错误
        ShapeBorder.SetResourceReference(Shape.StrokeProperty, Checked ? "ColorBrush2" : "ColorBrush1");
        LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");
    }
}

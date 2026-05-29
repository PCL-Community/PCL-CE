using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PCL;

public class MyComboBox : ComboBox
{
    public delegate void TextChangedEventHandler(object sender, TextChangedEventArgs e);

    public static readonly DependencyProperty HintTextProperty = DependencyProperty.Register("HintText", typeof(string),
        typeof(MyComboBox), new PropertyMetadata("", (d, e) =>
        {
            var c = (MyComboBox)d;
            if (c.TextBox is not null)
                c.TextBox.HintText = (string)e.NewValue;
        }));

    private string _Text;

    // 鼠标按下接口
    private bool IsMouseDown;

    // 修复 WPF Bug：下拉框文本修改后，依然误认为还选择着此前的选项，导致再次点击该选项时内容不变
    private bool IsTextChanging;
    private double RealWidth; // 由于下拉框 Popup 宽度与 Width 一致，故不能为 NaN（Auto）
    private MyTextBox TextBox;

    // 基础
    public int Uuid = ModBase.GetUuid();

    public MyComboBox()
    {
        _Text = SelectedItem?.ToString() ?? "";
        PreviewMouseLeftButtonDown += MyComboBox_PreviewMouseLeftButtonDown;
        PreviewMouseLeftButtonUp += MyComboBox_PreviewMouseLeftButtonUp;
        MouseLeave += MyComboBox_PreviewMouseLeftButtonUp;
        IsEnabledChanged += (_, _) => RefreshColor();
        MouseEnter += (_, _) => RefreshColor();
        MouseLeave += (_, _) => RefreshColor();
        PreviewMouseLeftButtonDown += (_, _) => RefreshColor();
        PreviewMouseLeftButtonUp += (_, _) => RefreshColor();
        GotKeyboardFocus += (_, _) => RefreshColor();
        DropDownOpened += MyComboBox_DropDownOpened;
        DropDownClosed += MyComboBox_DropDownClosed;
        TextChanged += MyComboBox_TextChanged;
    }

    public string HintText
    {
        get => (string)GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    public new string Text
    {
        get
        {
            if (IsEditable)
            {
                if (TextBox is null) return _Text ?? "";
                return TextBox.Text ?? "";
            }

            return (SelectedItem ?? "").ToString();
        }
        set
        {
            if (IsEditable)
            {
                if (TextBox is null)
                    _Text = value;
                else
                    TextBox.Text = value;
            }
            else
            {
                throw new NotSupportedException("该 ComboBox 不支持修改文本。");
            }
        }
    }

    public bool DropDownWidthSync { get; set; } = true;

    public ContentPresenter ContentPresenter => (ContentPresenter)Template.FindName("PART_Content", this);
    public event TextChangedEventHandler? TextChanged;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (!IsEditable)
            return;
        try
        {
            TextBox = (MyTextBox)Template.FindName("PART_EditableTextBox", this);
            TextBox.AddHandler(LostFocusEvent, new RoutedEventHandler((_, _) => RefreshColor()));
            TextBox.ChangedEventList.Add((sender, e) => TextChanged?.Invoke(sender, (TextChangedEventArgs)e));
            TextBox.Tag = Tag; // 有时需要用文本框的 Tag 来写入设置
            if (string.IsNullOrEmpty(Text))
                TextBox.Text = _Text;
            else
                TextChanged?.Invoke(this, null);
            if (HintText.Length > 0)
                TextBox.HintText = HintText;
            TextBox.SetResourceReference(TextBoxBase.CaretBrushProperty, "ColorBrushGray1");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化可编辑文本框失败（" + (Name ?? "") + "）", ModBase.LogLevel.Feedback);
        }
    }

    private void MyComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        IsMouseDown = true;
    }

    private void MyComboBox_PreviewMouseLeftButtonUp(object sender, EventArgs e)
    {
        IsMouseDown = false;
    }

    // 指向动画
    public void RefreshColor()
    {
        // 判断当前颜色
        string ForeColorName;
        string BackColorName;
        int Time;
        if (IsEnabled)
        {
            if (IsMouseDown || IsDropDownOpen ||
                (IsEditable && ((MyTextBox)Template.FindName("PART_EditableTextBox", this)).IsFocused))
            {
                ForeColorName = "ColorBrush3";
                BackColorName = "ColorBrush7";
                Time = 10;
            }
            else if (IsMouseOver)
            {
                ForeColorName = "ColorBrush4";
                BackColorName = "ColorBrush7";
                Time = 100;
            }
            else
            {
                ForeColorName = "ColorBrushBg0";
                BackColorName = "ColorBrushHalfWhite";
                Time = 100;
            }
        }
        else
        {
            ForeColorName = "ColorBrushGray5";
            BackColorName = "ColorBrushGray6";
            Time = 200;
        }

        // 触发颜色动画
        if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
        {
            // 有动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(this, ForegroundProperty, ForeColorName, Time),
                    ModAnimation.AaColor(this, BackgroundProperty, BackColorName, Time)
                }, "MyComboBox Color " + Uuid);
        }
        else
        {
            // 无动画
            ModAnimation.AniStop("MyComboBox Color " + Uuid);
            SetResourceReference(ForegroundProperty, ForeColorName);
            SetResourceReference(BackgroundProperty, BackColorName);
        }
    }

    private void MyComboBox_DropDownOpened(object sender, EventArgs e)
    {
        RealWidth = Width;
        if (DropDownWidthSync)
            Width = ActualWidth;
        try
        {
            var popup = (Grid)Template.FindName("PanPopup", this);
            popup.Opacity = ModMain.FrmMain.Opacity;
            if (!DropDownWidthSync)
                popup.MinWidth = ActualWidth;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "设置下拉框属性失败", ModBase.LogLevel.Feedback);
        }
    }

    private void MyComboBox_DropDownClosed(object sender, EventArgs e)
    {
        Width = RealWidth;
    }

    private void MyComboBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsTextChanging || !IsEditable)
            return;
        if (SelectedItem is null || Text == SelectedItem.ToString()) return;
        {
            var RawText = Text;
            var RawSelectionStart = TextBox.SelectionStart;
            IsTextChanging = true;
            SelectedItem = null;
            Text = RawText;
            TextBox.SelectionStart = RawSelectionStart;
            IsTextChanging = false;
        }
    }

    // 用于 ItemsSource 的自定义容器
    protected override DependencyObject GetContainerForItemOverride()
    {
        return new MyComboBoxItem();
    }
    
    private void MyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && ModAnimation.AniControlEnabled == 0) ModMain.RaiseCustomEvent(this);
    }
    
    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is MyComboBoxItem || base.IsItemItsOwnContainerOverride(item);
    }
}
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;

using PCL.Core.App;
using PCL.Core.UI.Theme;
using System.Windows.Controls;

namespace PCL;

[ContentProperty("Inlines")]
public partial class MyHint
{
    // 配色
    public enum Themes
    {
        Blue = 0,
        Red = 1,
        Yellow = 2
    }

    public static readonly DependencyProperty IsWarnProperty = DependencyProperty.Register("IsWarn", typeof(bool),
        typeof(MyHint),
        new PropertyMetadata(true,
            (d, e) =>
            {
                var f = (MyHint)d;
                f.Theme = e.NewValue != null ? Themes.Red : Themes.Blue;
            }));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyHint), new PropertyMetadata("", (d, e) =>
        {
            var f = (MyHint)d;
            f.LabText.Text = (string)e.NewValue;
        }));

    private Themes _ColorType = Themes.Red;

    // 触发点击事件
    private bool IsMouseDown;
    public int Uuid = ModBase.GetUuid();

    public MyHint()
    {
        InitializeComponent();
        UpdateUI();
        Loaded += (_, _) => UpdateUI();
        Loaded += MyHint_Loaded;
        MouseLeftButtonUp += MyHint_MouseUp;
        MouseLeftButtonDown += MyHint_MouseDown;
        MouseLeave += (_, _) => MyHint_MouseLeave();
        Unloaded += (_, _) => Dispose();
    }

    // 边框
    public bool HasBorder
    {
        get => BorderThickness.Top > 0d;
        set
        {
            if (value)
                BorderThickness = new Thickness(3d, ModBase.GetWPFSize(1d), ModBase.GetWPFSize(1d),
                    ModBase.GetWPFSize(1d));
            else
                BorderThickness = new Thickness(3d, 0d, 0d, 0d);
        }
    }

    public Themes Theme
    {
        get => _ColorType;
        set
        {
            _ColorType = value;
            UpdateUI();
        }
    }

    [Obsolete("IsWarn 已过时。请换用 Theme 属性。")]
    public bool IsWarn
    {
        get => Theme == Themes.Red;
        set => Theme = value ? Themes.Red : Themes.Blue;
    }

    // 文本
    public InlineCollection Inlines => LabText.Inlines;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // 关闭按钮
    public bool CanClose
    {
        get => BtnClose.Visibility == Visibility.Visible;
        set => BtnClose.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public string RelativeSetup { get; set; } = "";

    private void UpdateUI()
    {
        var hue = default(double);
        switch (Theme)
        {
            case Themes.Blue:
            {
                hue = 210d;
                break;
            }
            case Themes.Red:
            {
                hue = 355d;
                break;
            }
            case Themes.Yellow:
            {
                hue = 40d;
                break;
            }
        }

        var s = ThemeService.CurrentTone;
        Background = new ModBase.MyColor().FromHSL2(hue, 90, s.L7 * 100);
        BorderBrush = new ModBase.MyColor().FromHSL2(hue, 90, s.L2 * 100);
        LabText.Foreground = new ModBase.MyColor().FromHSL2(hue, 90, s.L2 * 100);
        BtnClose.Foreground = new ModBase.MyColor().FromHSL2(hue, 90, s.L2 * 100);
    }

    private void MyHint_Loaded(object sender, RoutedEventArgs e)
    {
        ThemeService.ColorModeChanged += (v, theme) => _ThemeChanged(v, theme);
        if (CanClose && ModBase.Setup.Get(RelativeSetup) != null)
            Visibility = Visibility.Collapsed;
    }

    private void BtnClose_Click(object sender, EventArgs e)
    {
        ModBase.Setup.Set(RelativeSetup, true);
        ModAnimation.AniDispose(this, false);
    }

    private void MyHint_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsMouseDown)
            return;
        IsMouseDown = false;
        ModBase.Log("[Control] 按下提示条" + (string.IsNullOrEmpty(Name) ? "" : "：" + Name));
        e.Handled = true;
        ModMain.RaiseCustomEvent(this);
    }

    private void MyHint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        IsMouseDown = true;
    }

    private void MyHint_MouseLeave()
    {
        IsMouseDown = false;
    }

    private void _ThemeChanged(bool isDarkMode, ColorTheme theme)
    {
        UpdateUI();
    }

    private void Dispose()
    {
        ThemeService.ColorModeChanged -= _ThemeChanged;
    }

    // Private Sub SetStyle()
    // If Type = HintType.Note Then
    // If IsWarn Then
    // BorderBrush = New MyColor("#CCFF4444")
    // Gradient1.Color = New MyColor(CType(If(IsDarkMode, "#BBFF8888", "#BBFFBBBB"), String))
    // Gradient2.Color = New MyColor(CType(If(IsDarkMode, "#BBFF6666", "#BBFF8888"), String))
    // Path.Fill = New MyColor("#BF0000")
    // LabText.Foreground = New MyColor("#BF0000")
    // BtnClose.Foreground = New MyColor("#BF0000")
    // Path.Data = (New GeometryConverter).ConvertFromString("F1 M 58.5832,55.4172L 17.4169,55.4171C 15.5619,53.5621 15.5619,50.5546 17.4168,48.6996L 35.201,15.8402C 37.056,13.9852 40.0635,13.9852 41.9185,15.8402L 58.5832,48.6997C 60.4382,50.5546 60.4382,53.5622 58.5832,55.4172 Z M 34.0417,25.7292L 36.0208,41.9584L 39.9791,41.9583L 41.9583,25.7292L 34.0417,25.7292 Z M 38,44.3333C 36.2511,44.3333 34.8333,45.7511 34.8333,47.5C 34.8333,49.2489 36.2511,50.6667 38,50.6667C 39.7489,50.6667 41.1666,49.2489 41.1666,47.5C 41.1666,45.7511 39.7489,44.3333 38,44.3333 Z ")
    // Return
    // Else
    // BorderBrush = New MyColor("#CC4D76FF")
    // Gradient1.Color = New MyColor("#BBB0D0FF")
    // Gradient2.Color = New MyColor("#BB9EBAFF")
    // Path.Fill = New MyColor("#0062BF")
    // LabText.Foreground = New MyColor("#0062BF")
    // BtnClose.Foreground = New MyColor("#0062BF")
    // Path.Data = (New GeometryConverter).ConvertFromString("F1M38,19C48.4934,19 57,27.5066 57,38 57,48.4934 48.4934,57 38,57 27.5066,57 19,48.4934 19,38 19,27.5066 27.5066,19 38,19z M33.25,33.25L33.25,36.4167 36.4166,36.4167 36.4166,47.5 33.25,47.5 33.25,50.6667 44.3333,50.6667 44.3333,47.5 41.1666,47.5 41.1666,36.4167 41.1666,33.25 33.25,33.25z M38.7917,25.3333C37.48,25.3333 36.4167,26.3967 36.4167,27.7083 36.4167,29.02 37.48,30.0833 38.7917,30.0833 40.1033,30.0833 41.1667,29.02 41.1667,27.7083 41.1667,26.3967 40.1033,25.3333 38.7917,25.3333z")
    // Return
    // End If
    // End If

    // Select Case Type
    // Case HintType.Warning
    // BorderBrush = New MyColor("#CCE69900")
    // Gradient1.Color = New MyColor("#BBFFF4CE")
    // Gradient2.Color = New MyColor("#BBFFF5CE")
    // Path.Fill = New MyColor("#957500")
    // LabText.Foreground = New MyColor("#957500")
    // BtnClose.Foreground = New MyColor("#957500")
    // Path.Data = (New GeometryConverter).ConvertFromString("F1 M 58.5832,55.4172L 17.4169,55.4171C 15.5619,53.5621 15.5619,50.5546 17.4168,48.6996L 35.201,15.8402C 37.056,13.9852 40.0635,13.9852 41.9185,15.8402L 58.5832,48.6997C 60.4382,50.5546 60.4382,53.5622 58.5832,55.4172 Z M 34.0417,25.7292L 36.0208,41.9584L 39.9791,41.9583L 41.9583,25.7292L 34.0417,25.7292 Z M 38,44.3333C 36.2511,44.3333 34.8333,45.7511 34.8333,47.5C 34.8333,49.2489 36.2511,50.6667 38,50.6667C 39.7489,50.6667 41.1666,49.2489 41.1666,47.5C 41.1666,45.7511 39.7489,44.3333 38,44.3333 Z ")
    // Return
    // Case HintType.Caution
    // BorderBrush = New MyColor("#CCFF4444")
    // Gradient1.Color = New MyColor(CType(If(IsDarkMode, "#BBFF8888", "#BBFFBBBB"), String))
    // Gradient2.Color = New MyColor(CType(If(IsDarkMode, "#BBFF6666", "#BBFF8888"), String))
    // Path.Fill = New MyColor("#BF0000")
    // LabText.Foreground = New MyColor("#BF0000")
    // BtnClose.Foreground = New MyColor("#BF0000")
    // Path.Data = (New GeometryConverter).ConvertFromString("F1 M1024,1024z M0,0z M512,0C229.23,0 0,229.23 0,512 0,794.77 229.23,1024 512,1024 794.768,1024 1024,794.77 1024,512 1024,229.23 794.77,0 512,0z M746.76,656.252C754.568,664.06,754.566,676.724,746.762,684.536L684.534,746.76C676.726,754.568,664.064,754.574,656.248,746.762L512,602.51 367.75,746.76C359.94,754.572,347.276,754.568,339.466,746.76L277.24,684.536C269.43,676.728,269.428,664.064,277.24,656.252L421.492,512 277.242,367.75C269.432,359.942,269.432,347.276,277.242,339.466L339.468,277.242C347.278,269.43,359.942,269.432,367.752,277.242L512,421.49 656.252,277.24C664.058,269.428,676.722,269.43,684.534,277.24L746.76,339.464C754.566,347.276,754.568,359.938,746.76,367.748L602.51,512 746.76,656.252z")
    // Return
    // Case Else
    // BorderBrush = New MyColor("#CC4D76FF")
    // Gradient1.Color = New MyColor("#BBB0D0FF")
    // Gradient2.Color = New MyColor("#BB9EBAFF")
    // Path.Fill = New MyColor("#0062BF")
    // LabText.Foreground = New MyColor("#0062BF")
    // BtnClose.Foreground = New MyColor("#0062BF")
    // Path.Data = (New GeometryConverter).ConvertFromString("F1M38,19C48.4934,19 57,27.5066 57,38 57,48.4934 48.4934,57 38,57 27.5066,57 19,48.4934 19,38 19,27.5066 27.5066,19 38,19z M33.25,33.25L33.25,36.4167 36.4166,36.4167 36.4166,47.5 33.25,47.5 33.25,50.6667 44.3333,50.6667 44.3333,47.5 41.1666,47.5 41.1666,36.4167 41.1666,33.25 33.25,33.25z M38.7917,25.3333C37.48,25.3333 36.4167,26.3967 36.4167,27.7083 36.4167,29.02 37.48,30.0833 38.7917,30.0833 40.1033,30.0833 41.1667,29.02 41.1667,27.7083 41.1667,26.3967 40.1033,25.3333 38.7917,25.3333z")
    // Return
    // End Select
    // End Sub
}

public static partial class ModAnimation
{
    public static void AniDispose(MyHint Control, bool RemoveFromChildren, ParameterizedThreadStart CallBack = null)
    {
        if (!Control.IsHitTestVisible)
            return;
        Control.IsHitTestVisible = false;
        AniStart(new[]
        {
            AaScaleTransform(Control, -0.08d, 200, Ease: new AniEaseInFluent()),
            AaOpacity(Control, -1, 200, Ease: new AniEaseOutFluent()),
            AaHeight(Control, -Control.ActualHeight, 150, 100, new AniEaseOutFluent()),
            AaCode(() =>
            {
                if (RemoveFromChildren)
                    ((Panel)Control.Parent).Children.Remove(Control);
                else
                    Control.Visibility = Visibility.Collapsed;
                if (CallBack is not null)
                    CallBack(Control);
            }, After: true)
        }, "MyCard Dispose " + Control.Uuid);
    }
}
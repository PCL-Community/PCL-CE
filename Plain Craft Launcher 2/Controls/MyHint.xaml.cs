using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using PCL.Core.App;
using PCL.Core.App.Configuration;
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
        if (CanClose && ConfigService.TryGetConfigItemNoType(RelativeSetup, out var item) && item.GetValueNoType() != null)
            Visibility = Visibility.Collapsed;
    }

    private void BtnClose_Click(object sender, EventArgs e)
    {
        if (ConfigService.TryGetConfigItemNoType(RelativeSetup, out var item))
            item.SetValueNoType(true);
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
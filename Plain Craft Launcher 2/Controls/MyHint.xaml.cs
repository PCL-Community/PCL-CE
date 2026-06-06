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
                f.Theme = e.NewValue is not null ? Themes.Red : Themes.Blue;
            }));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyHint), new PropertyMetadata("", (d, e) =>
        {
            var f = (MyHint)d;
            f.LabText.Text = (string)e.NewValue;
        }));

    // 触发点击事件
    private bool isMouseDown;
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
                BorderThickness = Config.Preference.HintAlignRight
                    ? new Thickness(ModBase.GetWPFSize(1d), ModBase.GetWPFSize(1d), 3d, ModBase.GetWPFSize(1d))
                    : new Thickness(3d, ModBase.GetWPFSize(1d), ModBase.GetWPFSize(1d), ModBase.GetWPFSize(1d));
            else
                BorderThickness = Config.Preference.HintAlignRight
                    ? new Thickness(0d, 0d, 3d, 0d)
                    : new Thickness(3d, 0d, 0d, 0d);
        }
    }

    public Themes Theme
    {
        get => field;
        set
        {
            field = value;
            UpdateUI();
        }
    } = Themes.Red;

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

        // 根据提示气泡对齐方向刷新边框
        // 此处依赖 HasBorder 的副作用进行范围检查
        HasBorder = HasBorder;
    }

    private void MyHint_Loaded(object sender, RoutedEventArgs e)
    {
        ThemeService.ColorModeChanged += (v, theme) => _ThemeChanged(v, theme);
        if (CanClose && ConfigService.TryGetConfigItemNoType(RelativeSetup, out var item) && item.GetValueNoType() is not null)
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
        if (!isMouseDown)
            return;
        isMouseDown = false;
        ModBase.Log("[Control] 按下提示条" + (string.IsNullOrEmpty(Name) ? "" : "：" + Name));
        e.Handled = true;
        ModMain.RaiseCustomEvent(this);
    }

    private void MyHint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        isMouseDown = true;
    }

    private void MyHint_MouseLeave()
    {
        isMouseDown = false;
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
    public static void AniDispose(MyHint control, bool removeFromChildren, ParameterizedThreadStart callBack = null)
    {
        if (!control.IsHitTestVisible)
            return;
        control.IsHitTestVisible = false;
        AniStart(new[]
        {
            AaScaleTransform(control, -0.08d, 200, ease: new AniEaseInFluent()),
            AaOpacity(control, -1, 200, ease: new AniEaseOutFluent()),
            AaHeight(control, -control.ActualHeight, 150, 100, new AniEaseOutFluent()),
            AaCode(() =>
            {
                if (removeFromChildren)
                    ((Panel)control.Parent).Children.Remove(control);
                else
                    control.Visibility = Visibility.Collapsed;
                if (callBack is not null)
                    callBack(control);
            }, after: true)
        }, "MyCard Dispose " + control.Uuid);
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Core.App;
using PCL.Core.UI.Theme;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Shell;

/// <summary>
///     自绘标题栏：左侧 Logo 与四个主入口，二级页面显示返回键与标题，右侧为窗口按钮。
///     Logo 模式、自定义文本与左右布局由配置与主题驱动，文本全部走本地化。
/// </summary>
public partial class TitleBar : UserControl
{
    private const string TitleLayerAnimationKey = "TitleBar Title Layer";

    private const string InnerTitleAnimationKey = "TitleBar Inner Title";

    private bool _isSecondary;

    private bool _updating;

    public TitleBar()
    {
        InitializeComponent();
        AddHandler(
            PointerPressedEvent,
            Root_OnPointerPressed,
            RoutingStrategies.Tunnel,
            true);
        PanTitleMain.RenderTransform = new TranslateTransform();
        PanInnerTitle.RenderTransform = new TranslateTransform();
        PanTitleMain.SizeChanged += (_, _) => ApplyTitleLeftClamp();
        RefreshTitleAppearance();

        ThemeService.ColorThemeChanged += _OnColorThemeChanged;
        ThemeService.ColorModeChanged += _OnColorModeChanged;
        Unloaded += (_, _) =>
        {
            ThemeService.ColorThemeChanged -= _OnColorThemeChanged;
            ThemeService.ColorModeChanged -= _OnColorModeChanged;
        };
    }

    private bool CanAnimateShell =>
        AnimationService.IsEnabled
        && IsLoaded
        && TopLevel.GetTopLevel(this) is not null;

    private void _OnColorThemeChanged(ColorTheme theme)
    {
        RefreshTitleAppearanceOnUi();
    }

    private void _OnColorModeChanged(bool isDarkMode, ColorTheme theme)
    {
        RefreshTitleAppearanceOnUi();
    }

    private void RefreshTitleAppearanceOnUi()
    {
        if (Dispatcher.UIThread.CheckAccess())
            RefreshTitleAppearance();
        else
            Dispatcher.UIThread.Post(RefreshTitleAppearance);
    }

    public event Action<MainTab>? TabSelected;

    public event Action? BackRequested;

    public event Action? HelpRequested;

    public event Action? MinimizeRequested;

    public event Action? CloseRequested;

    public void RefreshTitleAppearance()
    {
        var type = EffectiveTitleType();
        ShapeTitleLogo.IsVisible = type == 1;
        ShapeHMCLTitleLogo.IsVisible = type == 4;
        BtnTitleHelp.IsVisible = type == 4;
        LabTitleLogo.IsVisible = type == 2;
        ImageTitleLogo.IsVisible = type == 3;
        if (type != 4) CELogo.IsVisible = type != 0;

        if (type == 2) LabTitleLogo.Text = Config.Preference.WindowTitleCustomText;
        if (type == 3)
            try
            {
                ImageTitleLogo.Source = Path.Combine(Paths.Data, "Logo.png");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UI] 加载标题栏图片失败：{ex.Message}");
                ImageTitleLogo.Source = null;
            }

        ApplyLeftAlign(Config.Preference.TopBarLeftAlign, type);
    }

    public void UpdateState(PageNavigationState state)
    {
        _updating = true;
        try
        {
            var wasSecondary = _isSecondary;
            if (state.IsSecondary != _isSecondary)
            {
                _isSecondary = state.IsSecondary;
                SetSecondaryLayer(state.IsSecondary, true);
            }

            if (state.IsSecondary)
            {
                if (wasSecondary
                    && !string.Equals(LabTitleInner.Text, state.Title, StringComparison.Ordinal))
                    AnimateInnerTitle(state.Title);
                else
                    LabTitleInner.Text = state.Title;
            }

            SetTabChecked(state.Tab);
        }
        finally
        {
            _updating = false;
        }
    }

    private static int EffectiveTitleType()
    {
        if (ThemeService.CurrentTheme == ColorTheme.HmclBlue) return 4;
        return (int)Config.Preference.WindowTitleType;
    }

    private void ApplyTitleLeftClamp()
    {
        var available = PanTitleMain.ColumnDefinitions[0].ActualWidth - 30d;
        PanTitleLeft.ColumnDefinitions[0].MaxWidth = available > 0d ? available : 0d;
    }

    private void ApplyLeftAlign(bool leftAlign, int titleType)
    {
        var width = leftAlign && titleType == 0
            ? new GridLength(0d)
            : new GridLength(1d, GridUnitType.Star);
        PanTitleMain.ColumnDefinitions[0].Width = width;
        ApplyTitleLeftClamp();
    }

    private void AnimateInnerTitle(string title)
    {
        AnimationService.CancelAnimationByName(InnerTitleAnimationKey);
        if (!CanAnimateShell)
        {
            LabTitleInner.Text = title;
            LabTitleInner.Opacity = 1d;
            return;
        }

        AnimationService.Run(
            new List<IAnimation>
            {
                new OpacityAnimation(LabTitleInner, -LabTitleInner.Opacity, 130),
                new ActionAnimation(() => LabTitleInner.Text = title, true),
                new OpacityAnimation(LabTitleInner, 1d, 150, 30)
            },
            InnerTitleAnimationKey);
    }

    private void SetSecondaryLayer(bool secondary, bool animate)
    {
        var main = PanTitleMain;
        var inner = PanInnerTitle;
        AnimationService.CancelAnimationByName(TitleLayerAnimationKey);

        if (!animate || !CanAnimateShell)
        {
            main.IsVisible = !secondary;
            main.Opacity = secondary ? 0d : 1d;
            main.IsHitTestVisible = !secondary;
            ResetTranslate(main);
            inner.IsVisible = secondary;
            inner.Opacity = secondary ? 1d : 0d;
            ResetTranslate(inner);
            return;
        }

        var mainX = GetTranslateX(main);
        if (secondary)
        {
            main.IsHitTestVisible = false;
            inner.IsVisible = true;
            if (inner is { Opacity: <= 0.1d, RenderTransform: TranslateTransform { X: 0d } })
                inner.RenderTransform = new TranslateTransform(-16d, 0d);
            var innerX = GetTranslateX(inner);
            AnimationService.Run(
                new List<IAnimation>
                {
                    new OpacityAnimation(main, -main.Opacity, 150),
                    new TranslateXAnimation(
                        main,
                        12d - mainX,
                        150,
                        ease: new FluentEaseIn(EasePower.Weak)),
                    new OpacityAnimation(inner, 1d - inner.Opacity, 150, 200),
                    new TranslateXAnimation(inner, -innerX, 350, 200, new BackEaseWithPowerOut()),
                    new ActionAnimation(() => main.IsVisible = false, true)
                },
                TitleLayerAnimationKey);
            return;
        }

        main.IsVisible = true;
        main.IsHitTestVisible = true;
        var innerCurrentX = GetTranslateX(inner);
        AnimationService.Run(
            new List<IAnimation>
            {
                new OpacityAnimation(inner, -inner.Opacity, 150),
                new TranslateXAnimation(inner, -18d - innerCurrentX, 150, ease: new FluentEaseIn()),
                new OpacityAnimation(main, 1d - main.Opacity, 150, 200),
                new TranslateXAnimation(
                    main,
                    -mainX,
                    350,
                    200,
                    new BackEaseWithPowerOut(EasePower.Weak)),
                new ActionAnimation(() => inner.IsVisible = false, true)
            },
            TitleLayerAnimationKey);
    }

    private static double GetTranslateX(Control control)
    {
        return control.RenderTransform is TranslateTransform transform ? transform.X : 0d;
    }

    private static void ResetTranslate(Control control)
    {
        if (control.RenderTransform is TranslateTransform transform) transform.X = 0d;
    }

    private void SetTabChecked(MainTab tab)
    {
        BtnTitleSelect0.Checked = tab == MainTab.Launch;
        BtnTitleSelect1.Checked = tab == MainTab.Download;
        BtnTitleSelect2.Checked = tab == MainTab.Setup;
        BtnTitleSelect3.Checked = tab == MainTab.Tools;
    }

    private void Tab_OnCheck(MyRadioButton sender, bool raiseByMouse)
    {
        if (_updating || !raiseByMouse) return;
        TabSelected?.Invoke(
            ReferenceEquals(sender, BtnTitleSelect1) ? MainTab.Download
            : ReferenceEquals(sender, BtnTitleSelect2) ? MainTab.Setup
            : ReferenceEquals(sender, BtnTitleSelect3) ? MainTab.Tools
            : MainTab.Launch);
    }

    private void Back_OnClick(object? sender, EventArgs e)
    {
        BackRequested?.Invoke();
    }

    private void Help_OnClick(object? sender, EventArgs e)
    {
        HelpRequested?.Invoke();
    }

    private void Minimize_OnClick(object? sender, EventArgs e)
    {
        MinimizeRequested?.Invoke();
    }

    private void Close_OnClick(object? sender, EventArgs e)
    {
        CloseRequested?.Invoke();
    }

    private void Root_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (IsInteractiveSource(e.Source)) return;
        if (TopLevel.GetTopLevel(this) is not Window window) return;

        // WPF 版本的窗口策略会强制保持 Normal；这里不再处理双击最大化，
        // 避免 Minimized/Maximized/Normal 与 BeginMoveDrag 叠加导致窗口卡死。
        if (e.ClickCount > 1) return;

        if (window.WindowState == WindowState.Maximized) return;

        try
        {
            window.BeginMoveDrag(e);
            e.Handled = true;
        }
        catch
        {
            // 平台不支持原生移动时由系统窗口管理器处理。
        }
    }

    private bool IsInteractiveSource(object? source)
    {
        for (var visual = source as Visual;
             visual is not null && visual != this;
             visual = visual.GetVisualParent())
            if (visual is MyIconButton or MyRadioButton or Button or RadioButton or ToggleButton)
                return true;
        return false;
    }
}
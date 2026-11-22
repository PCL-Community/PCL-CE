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

/// <summary>自绘标题栏：左侧 Logo 与四个主入口，二级页面显示返回键与标题，右侧为窗口按钮。 (macOS下会被<see cref="UseSystemCaptionButtons" />隐藏自绘按钮，变成系统原生红绿灯) Logo 模式、自定义文本与左右布局由配置与主题驱动，文本全部走本地化。</summary>
public partial class TitleBar : UserControl
{
    private const string TitleLayerAnimationKey = "TitleBar Title Layer";

    private const string InnerTitleAnimationKey = "TitleBar Inner Title";

    private readonly Dictionary<MainTab, MyRadioButton> _tabButtons = [];

    private readonly double _defaultHeight;

    private bool _isExtended;

    private bool _isSecondary;

    private double _leadingInset;

    private bool _updating;

    public TitleBar()
    {
        InitializeComponent();
        _defaultHeight = PanTitle.Height;
        BuildMainTabs();
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

    private int EffectiveTitleType()
    {
        if (ThemeService.CurrentTheme == ColorTheme.HmclBlue) return 4;
        return _isExtended ? (int)LauncherTitleType.None : (int)Config.Preference.WindowTitleType;
    }

    /// <summary>当窗口使用系统标题栏按钮（macOS 红绿灯）时，隐藏自绘的最小化与关闭按钮。</summary>
    public void UseSystemCaptionButtons()
    {
        BtnTitleMin.IsVisible = false;
        BtnTitleClose.IsVisible = false;
    }

    public void SetSystemTitleBarLayout(double? height, double leadingInset)
    {
        _isExtended = height is not null;
        PanTitle.Height = height ?? _defaultHeight;
        _leadingInset = leadingInset;
        var margin = new Thickness(leadingInset, 0d, 0d, 0d);
        PanTitleLeft.Margin = margin;
        PanInnerTitle.Margin = margin;
        RefreshTitleAppearance();
    }

    private void ApplyTitleLeftClamp()
    {
        var available = PanTitleMain.ColumnDefinitions[0].ActualWidth - 30d - PanTitleLeft.Margin.Left;
        PanTitleLeft.ColumnDefinitions[0].MaxWidth = available > 0d ? available : 0d;
    }

    private void ApplyLeftAlign(bool leftAlign, int titleType)
    {
        var width = leftAlign && titleType == 0
            ? new GridLength(_leadingInset)
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

    private void BuildMainTabs()
    {
        PanTitleSelect.Children.Clear();
        _tabButtons.Clear();

        foreach (var descriptor in PageCatalog.MainTabs.OrderBy(tab => tab.Order))
        {
            var button = new MyRadioButton
            {
                SvgIcon = descriptor.IconKey,
                LogoScale = descriptor.LogoScale,
                ColorType = MyRadioButton.ColorState.White,
                Padding = new Thickness(2d, 0d),
                Margin = new Thickness(5d, 0d),
                CheckCommandParameter = descriptor.Id
            };
            button.SetLocalizedValue(MyRadioButton.TextProperty, descriptor.TitleKey);
            button.Check += Tab_OnCheck;
            _tabButtons[descriptor.Id] = button;
            PanTitleSelect.Children.Add(button);
        }

        SetTabChecked(MainTab.Launch);
    }

    private void SetTabChecked(MainTab tab)
    {
        foreach (var (id, button) in _tabButtons)
            button.Checked = id == tab;
    }

    private void Tab_OnCheck(MyRadioButton sender, bool raiseByMouse)
    {
        if (_updating || !raiseByMouse) return;
        if (sender.CheckCommandParameter is MainTab tab)
            TabSelected?.Invoke(tab);
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
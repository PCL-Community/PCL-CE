using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.UI.Controls;
using PCL.UI.SystemIntegration;
using PCL.Views.Pages;
using PCL.Views.Shell;

namespace PCL.Navigation;

/// <summary>
///     页面路由：维护左右页面宿主、当前定位与返回栈，并驱动页面生命周期与转场。
///     顶级页面按 <see cref="PageType" /> 切换，二级页面按 <see cref="PageStackData" /> 返回；
///     所有公共入口必须在 UI 线程调用。
/// </summary>
public static class PageRouter
{
    private const int PageSwapDelayMilliseconds = 110;
    private const int PageEnterDelayMilliseconds = 30;
    private const string TransitionAnimationKey = "PageTransition";

    private static ContentControl? _leftHost;
    private static ContentControl? _rightHost;
    private static Control? _leftPage;
    private static Control? _mountedLeft;
    private static Control? _mountedRight;
    private static int _transitionGeneration;
    private static readonly List<PageStackData> PageStack = [];
    private static readonly Dictionary<MainTab, PageStackData> TabStates = [];
    private static bool _isNavigating;
    private static bool _hasNavigated;
    private static bool _languageHooked;

    public static Control? CurrentPage { get; private set; }
    public static INavigationService Navigation => NavigationService.Instance;
    public static PageStackData Current { get; private set; } = new(PageType.Launch);
    public static PageNavigationState? CurrentState { get; private set; }
    public static bool CanGoBack => PageStack.Count > 0;

    public static event Action<PageNavigationState>? Navigated;
    public static event Action<PageStackData>? PageChanged;

    /// <summary>页面宿主已经完成 110ms 换页；左栏背景按实际内容宽度重新测量。</summary>
    public static event Action? PageHostSwapped;

    public static void Attach(ContentControl? leftHost, ContentControl rightHost)
    {
        _leftHost = leftHost;
        _rightHost = rightHost;
        _leftHost?.IsVisible = false;

        if (_languageHooked) return;
        _languageHooked = true;
        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    private static void OnLanguageChanged()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnLanguageChanged);
            return;
        }

        try
        {
            RefreshState();
            (_mountedLeft as MyPageLeft)?.ApplyLocalizationChanged();
            (_mountedRight as MyPageRight)?.ApplyLocalizationChanged();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", "语言切换后刷新页面失败");
        }
    }

    public static void ShowStandalone(Control page)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowStandalone(page));
            return;
        }

        if (_rightHost is null) return;
        _hasNavigated = false;
        _leftPage = null;
        CurrentPage = page;
        CancelPendingTransition();
        if (_leftHost is not null)
        {
            _leftHost.Content = null;
            _leftHost.IsVisible = false;
        }

        _rightHost.Content = page;
        _mountedLeft = null;
        _mountedRight = page;
    }

    public static void Navigate(MainTab tab)
    {
        var page = PageRegistry.PageOfTab(tab);
        var target = TabStates.GetValueOrDefault(tab) ?? new PageStackData(page);
        Navigate(target);
    }

    public static void Navigate(
        PageType page,
        PageSubType subType = PageSubType.Default,
        object? parameter = null)
    {
        Navigate(new PageStackData(page, subType, parameter));
    }

    public static void Navigate(PageStackData stack)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Navigate(stack));
            return;
        }

        NavigateCore(stack);
    }

    public static bool Back()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Back());
            return false;
        }

        if (PageStack.Count > 0)
        {
            NavigateCore(PageStack[0]);
            return true;
        }

        NavigateCore(new PageStackData(PageType.Launch));
        return false;
    }

    public static void RefreshState()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshState);
            return;
        }

        PublishState();
    }

    private static void NavigateCore(PageStackData stack)
    {
        if (_isNavigating || _rightHost is null) return;

        var host = PageRegistry.GetHost(stack.Page);
        if (host is null)
        {
            LogWrapper.Warn("UI", $"页面未注册，无法导航：{stack.Page}");
            return;
        }

        if (_hasNavigated && Current.IsSameLocation(stack)) return;

        _isNavigating = true;
        AnimationService.BeginControl();
        try
        {
            if (host.IsSecondary)
            {
                if (PageStack.Contains(stack))
                    while (PageStack.Contains(stack))
                        PageStack.RemoveAt(0);
                else
                    PageStack.Insert(0, Current);
            }
            else
            {
                PageStack.Clear();
            }

            var left = host.GetLeft(stack);
            var right = host.GetRight(stack);

            if (left is null && _leftHost is not null)
            {
                _leftHost.Content = null;
                _leftHost.IsVisible = false;
            }

            _MountPages(left, right, stack);

            Current = stack;
            _hasNavigated = true;
            if (host.Tab is { } tab)
                TabStates[tab] = stack;

            PublishState();
            PageChanged?.Invoke(stack);

            try
            {
                ExtraButtonsPanel.Current?.RefreshBackButton();
                ExtraButtonsPanel.Current?.RefreshDownloadButton();
            }
            catch (Exception ex)
            {
                LogWrapper.Warn("UI", $"刷新快捷按钮失败：{ex.Message}");
            }

            LogWrapper.Debug("UI", $"切换页面：{stack.Page}/{stack.SubType}");
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", $"切换页面失败：{stack.Page}");
        }
        finally
        {
            AnimationService.EndControl();
            _isNavigating = false;
        }
    }

    private static void _MountPages(Control? left, Control right, PageStackData stack)
    {
        if (_rightHost is null) return;

        var generation = ++_transitionGeneration;
        AnimationService.CancelAnimationByName(TransitionAnimationKey);

        var oldLeft = _mountedLeft;
        var oldRight = _mountedRight;
        var animateLeft = !ReferenceEquals(oldLeft, left);
        _leftPage = left;
        CurrentPage = right;

        if (oldLeft is IPageView oldLeftView) oldLeftView.OnNavigatedFrom();
        if (animateLeft && oldLeft is MyPageLeft oldLeftPage) oldLeftPage.TriggerHideAnimation();
        if (oldRight is IPageView oldRightView) oldRightView.OnNavigatedFrom();
        if (oldRight is MyPageRight oldRightPage) oldRightPage.PageOnExit();

        AnimationService.Run(
            new List<IAnimation>
            {
                new ActionAnimation(
                    () =>
                    {
                        if (generation != _transitionGeneration) return;

                        using (AnimationService.SuspendControl())
                        {
                            if (oldRight is MyPageRight forceExitPage)
                                forceExitPage.PageOnForceExit();
                            SwapPageHosts(left, right, stack, animateLeft);
                        }

                        PageHostSwapped?.Invoke();
                    },
                    PageSwapDelayMilliseconds),
                new ActionAnimation(
                    () =>
                    {
                        if (generation != _transitionGeneration
                            || !ReferenceEquals(_mountedRight, right))
                        {
                            if (!ReferenceEquals(_mountedRight, right)) right.Opacity = 1d;
                            return;
                        }

                        EnterPage(left, right, stack, animateLeft);
                    },
                    PageEnterDelayMilliseconds,
                    true)
            },
            TransitionAnimationKey);
    }

    private static void SwapPageHosts(
        Control? left,
        Control right,
        PageStackData stack,
        bool animateLeft)
    {
        if (_leftHost is not null)
        {
            if (animateLeft && left is MyPageLeft)
                left.Opacity = 0d;
            _leftHost.Content = left;
            _leftHost.IsVisible = left is not null;
            _leftHost.Width = double.NaN;
        }

        _rightHost?.Content = right;
        right.Opacity = 0d;
        _mountedLeft = left;
        _mountedRight = right;
    }

    private static void EnterPage(
        Control? left,
        Control right,
        PageStackData stack,
        bool animateLeft)
    {
        try
        {
            right.Opacity = 1d;
            // 与 WPF 一致：左页先挂载一帧，等布局完成后再播放进入动画。
            if (animateLeft && left is MyPageLeft newLeftPage)
            {
                left.Opacity = 1d;
                newLeftPage.TriggerShowAnimation();
            }

            if (left is IPageView leftView) leftView.OnNavigatedTo(stack.Parameter);
            if (right is MyPageRight newRightPage) newRightPage.PageOnEnter();
            if (right is IPageView rightView) rightView.OnNavigatedTo(stack.Parameter);
            LayoutProbe.CheckAfterLayout(right, $"{stack.Page}/{stack.SubType}");
            FocusFirst(right);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", $"页面进入失败：{stack.Page}");
        }
    }

    private static void FocusFirst(Control page)
    {
        Dispatcher.UIThread.Post(
            () =>
            {
                if (!page.IsEffectivelyVisible) return;
                var target = page
                    .GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(control =>
                        control is
                            {
                                Focusable: true,
                                IsEffectivelyVisible: true,
                                IsEnabled: true
                            }
                            and not ScrollViewer
                            and not ScrollBar
                        && (control.IsTabStop
                            || (control is not MyListItem && control is not MyIconButton)));

                if (target is not null)
                    target.Focus();
                else if (page.Focusable)
                    page.Focus();
            },
            DispatcherPriority.Loaded);
    }

    private static void CancelPendingTransition()
    {
        AnimationService.CancelAnimationByName(TransitionAnimationKey);
    }

    private static void PublishState()
    {
        var host = PageRegistry.GetHost(Current.Page);
        var isSecondary = host?.IsSecondary ?? false;
        var title = host?.GetTitle(Current) ?? string.Empty;
        var tab = GetActiveTab(host);
        var leftWidth = _leftPage is not null ? host?.LeftWidth ?? 210d : 0d;
        var state = new PageNavigationState(
            Current,
            title,
            isSecondary,
            tab,
            CanGoBack,
            leftWidth);
        CurrentState = state;
        Navigated?.Invoke(state);
        NavigationService.Instance.RaiseNavigated(new RouteKey(Current.Page, Current.SubType));
    }

    private static MainTab GetActiveTab(IPageHost? host)
    {
        if (host?.Tab is { } tab) return tab;
        return PageStack.Count > 0 && PageRegistry.TabOfPage(PageStack[0].Page) is { } stackTab
            ? stackTab
            : PageRegistry.TabOfPage(Current.Page) ?? MainTab.Launch;
    }
}
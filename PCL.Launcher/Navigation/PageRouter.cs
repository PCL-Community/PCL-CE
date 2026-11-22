using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.UI.Controls;
using PCL.UI.SystemIntegration;
using PCL.Views.Pages;
using PCL.Views.Shell;

namespace PCL.Navigation;

/// <summary>
/// 页面路由：维护左右页面宿主、当前定位与返回栈，并驱动页面生命周期与转场。 顶级页面按 <see cref="PageId" /> 切换，二级页面按
/// <see cref="PageRoute" /> 返回； 所有公共入口必须在 UI 线程调用。
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
    private static readonly PageNavigationStateMachine NavigationState = new();
    private static bool _isNavigating;
    private static bool _hasNavigated;

    public static Control? CurrentPage { get; private set; }
    public static INavigationService Navigation => NavigationService.Instance;
    public static PageRoute Current => NavigationState.Current;
    public static PageNavigationState? CurrentState { get; private set; }
    public static bool CanGoBack => NavigationState.CanGoBack;

    public static event Action<PageNavigationState>? Navigated;
    public static event Action<PageRoute>? PageChanged;

    /// <summary>页面宿主已经完成 110ms 换页；左栏背景按实际内容宽度重新测量。</summary>
    public static event Action? PageHostSwapped;

    public static void Attach(ContentControl? leftHost, ContentControl rightHost)
    {
        _leftHost = leftHost;
        _rightHost = rightHost;
        _leftHost?.IsVisible = false;
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
        var target = NavigationState.GetTabState(tab) ?? new PageRoute(page);
        Navigate(target);
    }

    public static void Navigate(
        PageId page,
        SubPageId? subPage = null,
        PageParameter? parameter = null)
    {
        Navigate(new PageRoute(page, subPage, parameter));
    }

    public static void Navigate(
        PageLocation location,
        PageParameter? parameter = null)
    {
        Navigate(new PageRoute(location.Page, location.SubPage, parameter));
    }

    public static void Navigate(PageRoute route)
    {
        if (route.SubPage is { } subPage && subPage.Parent != route.Page)
            throw new ArgumentException(
                $"子页面 {subPage} 不属于页面 {route.Page}",
                nameof(route));

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Navigate(route));
            return;
        }

        NavigateCore(route);
    }

    public static bool Back()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Back());
            return false;
        }

        var canGoBack = NavigationState.Back(out var target);
        NavigateCore(target);
        return canGoBack;
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

    private static void NavigateCore(PageRoute route)
    {
        if (_isNavigating || _rightHost is null) return;

        var host = PageRegistry.GetHost(route.Page);
        if (host is null)
        {
            LogWrapper.Warn("UI", $"页面未注册，无法导航：{route.Page}");
            return;
        }

        if (_hasNavigated && Current.Equals(route)) return;

        _isNavigating = true;
        AnimationService.BeginControl();
        try
        {
            var left = host.GetLeft(route);
            var right = host.GetRight(route);

            NavigationState.Navigate(route, host.IsSecondary, host.Tab);

            if (left is null && _leftHost is not null)
            {
                _leftHost.Content = null;
                _leftHost.IsVisible = false;
            }

            _MountPages(left, right, route);

            _hasNavigated = true;

            PublishState();
            PageChanged?.Invoke(route);

            try
            {
                ExtraButtonsPanel.Current?.RefreshBackButton();
                ExtraButtonsPanel.Current?.RefreshDownloadButton();
            }
            catch (Exception ex)
            {
                LogWrapper.Warn("UI", $"刷新快捷按钮失败：{ex.Message}");
            }

            LogWrapper.Debug("UI", $"切换页面：{route.Page}/{route.SubPage}");
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", $"切换页面失败：{route.Page}");
        }
        finally
        {
            AnimationService.EndControl();
            _isNavigating = false;
        }
    }

    private static void _MountPages(Control? left, Control right, PageRoute route)
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
        if (animateLeft && oldLeft is IPageLeftContainer oldLeftPage)
            oldLeftPage.TriggerHideAnimation();
        if (oldRight is IPageView oldRightView) oldRightView.OnNavigatedFrom();
        if (oldRight is IPageRightContainer oldRightPage) oldRightPage.PageOnExit();

        AnimationService.Run(
            new List<IAnimation>
            {
                new ActionAnimation(
                    () =>
                    {
                        if (generation != _transitionGeneration) return;

                        using (AnimationService.SuspendControl())
                        {
                            if (oldRight is IPageRightContainer forceExitPage)
                                forceExitPage.PageOnForceExit();
                            SwapPageHosts(left, right, route, animateLeft);
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

                        EnterPage(left, right, route, animateLeft);
                    },
                    PageEnterDelayMilliseconds,
                    true)
            },
            TransitionAnimationKey);
    }

    private static void SwapPageHosts(
        Control? left,
        Control right,
        PageRoute route,
        bool animateLeft)
    {
        if (_leftHost is not null)
        {
            if (animateLeft && left is IPageLeftContainer)
                left.Opacity = 0d;
            _leftHost.Content = left;
            _leftHost.IsVisible = left is not null;
            _leftHost.Width = double.NaN;
        }

        if (ReferenceEquals(_rightHost?.Content, right))
            _rightHost!.Content = null;
        _rightHost?.Content = right;
        right.Opacity = 0d;
        _mountedLeft = left;
        _mountedRight = right;
    }

    private static void EnterPage(
        Control? left,
        Control right,
        PageRoute route,
        bool animateLeft)
    {
        try
        {
            right.Opacity = 1d;
            if (animateLeft && left is IPageLeftContainer newLeftPage)
            {
                left.Opacity = 1d;
                newLeftPage.TriggerShowAnimation();
            }

            if (left is IPageView leftView) leftView.OnNavigatedTo(route.Parameter);
            if (right is IPageRightContainer newRightPage) newRightPage.PageOnEnter();
            if (right is IPageView rightView) rightView.OnNavigatedTo(route.Parameter);
            LayoutProbe.CheckAfterLayout(right, $"{route.Page}/{route.SubPage}");
            FocusFirst(right);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", $"页面进入失败：{route.Page}");
        }
    }

    private static void FocusFirst(Control page)
    {
        Dispatcher.UIThread.Post(
            () =>
            {
                if (!page.IsEffectivelyVisible) return;

                if (DialogService.Instance.HasPending
                    || TopLevel.GetTopLevel(page)?.FindControl<Panel>("PanDialogLayer")
                        is { IsVisible: true })
                    return;
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
        NavigationService.Instance.RaiseNavigated(new PageLocation(Current.Page, Current.SubPage));
    }

    private static MainTab GetActiveTab(IPageHost? host)
    {
        if (host?.Tab is { } tab) return tab;
        return NavigationState.BackStack.Count > 0
            && PageRegistry.TabOfPage(NavigationState.BackStack[0].Page) is { } stackTab
                ? stackTab
                : PageRegistry.TabOfPage(Current.Page) ?? MainTab.Launch;
    }
}
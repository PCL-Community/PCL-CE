using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.Navigation;
using PCL.UI;
using PCL.UI.Controls;

namespace PCL.Views.Shell;

/// <summary>页面可实现的壳级快捷键扩展，由主窗口在按键时调用。</summary>
public interface IShellShortcutTarget
{
    bool ToggleHiddenInstances();

    bool LaunchGame();
}

/// <summary>功能隐藏（F12）的运行时开关，设置页读取该状态决定隐藏项是否显示。</summary>
public static class HiddenFeatureState
{
    public static bool ForceShow { get; private set; }

    public static event Action? Changed;

    public static void Toggle()
    {
        ForceShow = !ForceShow;
        Changed?.Invoke();
    }
}

/// <summary>
///     主窗口级键盘与鼠标快捷键：
///     Esc 返回、F11 切换隐藏实例、F12 切换功能隐藏、F5 刷新当前页、Enter 启动游戏、Alt 抑制系统菜单、鼠标侧键返回。
/// </summary>
public sealed class KeyboardHandler
{
    private readonly ContentControl? _leftHost;
    private readonly Window _window;

    public KeyboardHandler(Window window, ContentControl? leftHost)
    {
        _window = window;
        _leftHost = leftHost;
        window.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        window.AddHandler(
            InputElement.PointerPressedEvent,
            OnPointerPressed,
            RoutingStrategies.Tunnel);
    }

    private bool IsDialogLayerVisible =>
        _window.FindControl<Panel>("PanDialogLayer") is { IsVisible: true };

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftAlt or Key.RightAlt)
        {
            e.Handled = true;
            return;
        }

        if (DialogService.Instance.IsShowing
            || DialogService.Instance.HasPending
            || IsDialogLayerVisible) return;

        if (e.KeyModifiers != KeyModifiers.None) return;

        switch (e.Key)
        {
            case Key.Escape:
                PageRouter.Back();
                e.Handled = true;
                break;
            case Key.F11:
                ToggleHiddenInstances();
                e.Handled = true;
                break;
            case Key.F12:
                ToggleHiddenFeatures();
                e.Handled = true;
                break;
            case Key.F5:
                RefreshCurrentPage();
                e.Handled = true;
                break;
            case Key.Enter when PageRouter.Current.Page == PageType.Launch:
                if (_window.FocusManager?.GetFocusedElement() is TextBox) return;
                LaunchGame();
                e.Handled = true;
                break;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var kind = e.GetCurrentPoint(_window).Properties.PointerUpdateKind;
        if (kind is not (PointerUpdateKind.XButton1Pressed or PointerUpdateKind.XButton2Pressed))
            return;
        if (DialogService.Instance.IsShowing
            || DialogService.Instance.HasPending
            || IsDialogLayerVisible) return;

        PageRouter.Back();
        e.Handled = true;
    }

    private void ToggleHiddenInstances()
    {
        var target = FindShortcutTarget();
        if (target?.ToggleHiddenInstances() == true) return;
        LogWrapper.Info("UI", "当前页面未提供隐藏实例显隐开关，F11 已忽略");
    }

    private void ToggleHiddenFeatures()
    {
        HiddenFeatureState.Toggle();
        UiInteraction.Hint(
            Lang.Text(
                HiddenFeatureState.ForceShow
                    ? "Main.HiddenFeature.Disabled"
                    : "Main.HiddenFeature.Enabled"),
            NotificationType.Success);
    }

    private void RefreshCurrentPage()
    {
        var refreshed = false;
        if (PageRouter.CurrentPage is IRefreshable right)
        {
            right.Refresh();
            refreshed = true;
        }

        if (_leftHost?.Content is IRefreshable left)
        {
            left.Refresh();
            refreshed = true;
        }

        if (!refreshed)
            LogWrapper.Info("UI", "当前页面不支持刷新，F5 已忽略");
    }

    private void LaunchGame()
    {
        if (Basics.IsAprilFool && !ExtraButtonsPanel.IsAprilGiveup)
        {
            UiInteraction.Hint(Lang.Text("Main.April.Nope"), NotificationType.Info);
            return;
        }

        if (FindShortcutTarget()?.LaunchGame() == true) return;
        LogWrapper.Info("UI", "当前启动页未提供启动入口，Enter 已忽略");
    }

    private IShellShortcutTarget? FindShortcutTarget()
    {
        return UnwrapShortcutTarget(PageRouter.CurrentPage)
            ?? UnwrapShortcutTarget(_leftHost?.Content as Control);
    }

    private static IShellShortcutTarget? UnwrapShortcutTarget(Control? page)
    {
        if (page is IShellShortcutTarget target) return target;
        return page switch
        {
            Decorator { Child: Control child } => UnwrapShortcutTarget(child),
            ContentControl { Content: Control content } => UnwrapShortcutTarget(content),
            _ => null
        };
    }
}

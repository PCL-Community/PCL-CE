using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SharpDX.XInput;

namespace PCL.Modules.Gamepad;

public static partial class ModGamepad
{
    private static MyIconButton? GetTitleBarButton()
    {
        var visible = GetVisibleTitleBarButtons();
        if (visible.Count == 0) return null;

        var idx = visible.IndexOf(_titleBarIndex);
        return idx >= 0 ? GetButtonByIndex(_titleBarIndex) : GetButtonByIndex(visible[0]);
    }

    private static void MoveTitleBarIndex(int direction)
    {
        var visible = GetVisibleTitleBarButtons();
        if (visible.Count == 0) return;

        var currentPos = visible.IndexOf(_titleBarIndex);
        if (currentPos < 0) currentPos = -direction;

        var newPos = (currentPos + direction + visible.Count) % visible.Count;
        _titleBarIndex = visible[newPos];
    }

    private static List<int> GetVisibleTitleBarButtons()
    {
        var visible = new List<int>(3);
        if (_btnTitleMin is { Visibility: Visibility.Visible }) visible.Add(0);
        if (_btnTitleHelp is { Visibility: Visibility.Visible }) visible.Add(1);
        if (_btnTitleClose is { Visibility: Visibility.Visible }) visible.Add(2);
        return visible;
    }

    private static MyIconButton? GetButtonByIndex(int idx) => idx switch
    {
        0 => _btnTitleMin,
        1 => _btnTitleHelp,
        2 => _btnTitleClose,
        _ => null
    };

    private static void EnterTitleBarMode()
    {
        _isTitleBarMode = true;
        _titleBarIndex = 0;
        _gamepadActive = true;
        _focusScopeStack.Clear();
        _preTitleBarFocused = Keyboard.FocusedElement as UIElement;

        var btn = GetTitleBarButton();
        if (btn is not null)
            btn.Focus();
    }

    private static void ExitTitleBarMode()
    {
        _isTitleBarMode = false;
        if (_preTitleBarFocused is { IsVisible: true, IsEnabled: true })
        {
            _preTitleBarFocused.Focus();
            _preTitleBarFocused = null;
            UpdateHighlightFromFocus();
            return;
        }
        _preTitleBarFocused = null;
        _pendingPageFocus = true;
    }

    private static void HandleButtonPress(GamepadButtonFlags buttons)
    {
        var frm = GetForm();
        if (frm is null) return;
        if (!frm.IsActive) return;

        if (_isTitleBarMode)
        {
            HandleTitleBarInput(buttons);
            return;
        }

        if (frm.PanMsg.Children.Count > 0)
        {
            InvokeDialogButton(frm.PanMsg.Children[0], buttons);
            return;
        }

        if (IsDPadDirection(buttons))
        {
            _gamepadActive = true;
            _lastMousePos = Mouse.GetPosition(frm);

            if (Keyboard.FocusedElement is ComboBox { IsDropDownOpen: true })
                return;

            var direction = buttons switch
            {
                GamepadButtonFlags.DPadUp => FocusNavigationDirection.Up,
                GamepadButtonFlags.DPadDown => FocusNavigationDirection.Down,
                GamepadButtonFlags.DPadLeft => FocusNavigationDirection.Left,
                _ => FocusNavigationDirection.Right
            };

            if (!MoveFocus(direction))
            {
                if (direction == FocusNavigationDirection.Left)
                    TryFocusOnPage(frm.PageLeft);
                else if (direction == FocusNavigationDirection.Right)
                    TryFocusOnPage(frm.PageRight);
                else
                    TryFocusOnPage(IsFocusOnLeftPage() ? frm.PageLeft : frm.PageRight);
            }
        }
        else if (buttons.HasFlag(GamepadButtonFlags.A))
        {
            var focused = Keyboard.FocusedElement as FrameworkElement;
            if (focused is not null && focused.IsVisible && focused.IsEnabled
                && KeyControllAbility.GetCanActivate(focused))
            {
                KeyControllAbility.Activate(focused);
            }
            else
            {
                _gamepadActive = true;
                _pendingPageFocus = true;
            }
        }
        else if (buttons.HasFlag(GamepadButtonFlags.B))
        {
            if (_focusScopeStack.Count > 0)
            {
                var parent = _focusScopeStack.Pop();
                parent.Focus();
            }
            else
            {
                SimulateKey(Key.Escape);
            }
        }
        else if (buttons.HasFlag(GamepadButtonFlags.Y))
        {
            var focused = Keyboard.FocusedElement as UIElement;
            if (focused is not null)
            {
                var child = KeyControllAbility.FindFirstSelectableChild(focused);
                if (child is not null)
                {
                    _focusScopeStack.Push(focused);
                    child.Focus();
                }
            }
        }
        else if (buttons.HasFlag(GamepadButtonFlags.LeftShoulder))
            NavigateTitleTab(-1);
        else if (buttons.HasFlag(GamepadButtonFlags.RightShoulder))
            NavigateTitleTab(1);
        else if (buttons.HasFlag(GamepadButtonFlags.Start))
            EnterTitleBarMode();
        else if (buttons.HasFlag(GamepadButtonFlags.Back))
        {
            _gamepadActive = true;
            _focusScopeStack.Clear();
            _pendingPageFocus = true;

            var targetPage = IsFocusOnLeftPage()
                ? (DependencyObject?)frm.PageRight
                : (DependencyObject?)frm.PageLeft;
            var first = KeyControllAbility.FindFirstSelectable(targetPage);
            if (first is not null)
            {
                first.Focus();
                _pendingPageFocus = false;
            }
        }

        UpdateHighlightFromFocus();
    }

    private static void HandleTitleBarInput(GamepadButtonFlags buttons)
    {
        if (buttons is GamepadButtonFlags.DPadLeft or GamepadButtonFlags.DPadRight)
        {
            MoveTitleBarIndex(buttons == GamepadButtonFlags.DPadRight ? 1 : -1);
            GetTitleBarButton()?.Focus();
        }
        else if (buttons.HasFlag(GamepadButtonFlags.A))
        {
            var btn = GetTitleBarButton();
            if (btn is not null && btn.IsVisible && btn.IsEnabled)
                KeyControllAbility.Activate(btn);
            ExitTitleBarMode();
        }
        else if (buttons.HasFlag(GamepadButtonFlags.B) || buttons.HasFlag(GamepadButtonFlags.Start))
        {
            ExitTitleBarMode();
        }
    }

    private static void SimulateKey(Key key)
    {
        _isSimulatingInput = true;
        var frm = GetForm();
        if (frm is null) return;
        var source = PresentationSource.FromVisual(frm);
        if (source is null) return;

        var e = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        };
        frm.RaiseEvent(e);
        _isSimulatingInput = false;
    }

    private static void SimulateScroll(int delta)
    {
        if (delta == 0) return;
        _isSimulatingInput = true;

        var frm = GetForm();
        if (frm is null) return;
        var source = PresentationSource.FromVisual(frm);
        if (source is null) return;

        var target = Keyboard.FocusedElement as UIElement ?? frm;
        var e = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent
        };
        target.RaiseEvent(e);
        _isSimulatingInput = false;
    }

    private static void InvokeDialogButton(UIElement msg, GamepadButtonFlags buttons)
    {
        Action? action = (buttons, msg) switch
        {
            (GamepadButtonFlags.A, MyMsgInput input) => () => input.Btn1_Click(null, null),
            (GamepadButtonFlags.A, MyMsgSelect select) => () => select.Btn1_Click(null, null),
            (GamepadButtonFlags.A, MyMsgText text) => () => text.Btn1_Click(),
            (GamepadButtonFlags.A, MyMsgMarkdown markdown) => () => markdown.Btn1_Click(null, null),
            (GamepadButtonFlags.A, MyMsgLogin login) => () => login.Btn1_Click(null, null),
            (GamepadButtonFlags.B, MyMsgInput input) => () => input.Btn2_Click(null, null),
            (GamepadButtonFlags.B, MyMsgSelect select) => () => select.Btn2_Click(null, null),
            (GamepadButtonFlags.B, MyMsgText text) => () => text.Btn2_Click(null, null),
            (GamepadButtonFlags.B, MyMsgMarkdown markdown) => () => markdown.Btn2_Click(null, null),
            (GamepadButtonFlags.X, MyMsgText text) => () => text.Btn3_Click(null, null),
            (GamepadButtonFlags.X, MyMsgMarkdown markdown) => () => markdown.Btn3_Click(null, null),
            (GamepadButtonFlags.X, MyMsgLogin login) => () => login.Btn3_Click(null, null),
            _ => null
        };
        action?.Invoke();
    }
}

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SharpDX.XInput;

namespace PCL;

public static class ModGamepad
{
    private static Controller _controller;
    private static bool _isRunning;
    private static GamepadButtonFlags _prevButtons;
    private static GamepadButtonFlags _repeatButton;
    private static DateTime _lastRepeatTime;

    private static Border _highlightBorder;
    private static Canvas _highlightCanvas;
    private static bool _isSimulatingInput;
    private static Point _lastMousePos;
    private static bool _gamepadActive;

    private static bool _pendingPageFocus;

    private static double _scrollAccumulator;

    private static bool _isTitleBarMode;
    private static int _titleBarIndex;
    private static UIElement? _preTitleBarFocused;
    private static readonly Stack<UIElement> _focusScopeStack = new();
    private static MyIconButton? _btnTitleMin;
    private static MyIconButton? _btnTitleHelp;
    private static MyIconButton? _btnTitleClose;
    private static Brush _highlightBorderBrush = null!;
    private static Brush _highlightBackground = null!;
    private static Brush _titleBarBorderBrush = null!;
    private static Brush _titleBarBackground = null!;

    private const int PollIntervalMs = 30;
    private const int HighlightRefreshInterval = 5;
    private const short RightStickDeadZone = 4000;
    private const double ScrollSensitivity = 0.03;
    private const double MouseHideThreshold = 8;
    private const int ScrollWheelMultiplier = 120;
    private const int RepeatDelayMs = 300;
    private const int RepeatRateMs = 100;

    public static void Initialize()
    {
        try
        {
            _controller = new Controller(UserIndex.One);
            if (!_controller.IsConnected)
            {
                ModBase.Log("[Gamepad] No Xbox controller detected");
                return;
            }

            RunOnUiThread(CreateHighlightOverlay);
            _pendingPageFocus = true;

            _isRunning = true;
            var thread = new Thread(PollLoop)
            {
                IsBackground = true,
                Name = "Gamepad Poll",
                Priority = ThreadPriority.BelowNormal
            };
            thread.Start();
            ModBase.Log("[Gamepad] Xbox controller connected");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "Failed to initialize gamepad");
        }
    }

    public static void Shutdown()
    {
        _isRunning = false;
    }

    private static void CreateHighlightOverlay()
    {
        var frm = ModMain.FrmMain;
        if (frm is null) return;

        _highlightCanvas = new Canvas
        {
            IsHitTestVisible = false,
            Focusable = false
        };
        Grid.SetRowSpan(_highlightCanvas, 2);

        _highlightBorderBrush = new SolidColorBrush(Color.FromArgb(200, 0, 120, 215));
        _highlightBackground = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215));
        _titleBarBorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 165, 0));
        _titleBarBackground = new SolidColorBrush(Color.FromArgb(50, 255, 165, 0));

        _highlightBorder = new Border
        {
            IsHitTestVisible = false,
            BorderBrush = _highlightBorderBrush,
            BorderThickness = new Thickness(2.5),
            Background = _highlightBackground,
            CornerRadius = new CornerRadius(6),
            Visibility = Visibility.Collapsed,
            Focusable = false
        };
        _highlightCanvas.Children.Add(_highlightBorder);

        var panMainIdx = frm.PanForm.Children.IndexOf(frm.PanMain);
        frm.PanForm.Children.Insert(panMainIdx + 1, _highlightCanvas);

        _btnTitleMin = frm.FindName("BtnTitleMin") as MyIconButton;
        _btnTitleHelp = frm.FindName("BtnTitleHelp") as MyIconButton;
        _btnTitleClose = frm.FindName("BtnTitleClose") as MyIconButton;

        var childProp = DependencyPropertyDescriptor.FromName("Child", typeof(Border), typeof(Border));
        if (childProp is not null)
        {
            childProp.AddValueChanged(frm.PanMainLeft, OnPageChildChanged);
            childProp.AddValueChanged(frm.PanMainRight, OnPageChildChanged);
        }

        frm.PreviewMouseDown += OnAnyMouseDown;
        frm.PreviewMouseMove += OnAnyMouseMove;
        frm.PreviewKeyDown += OnAnyKeyDown;
    }

    private static void OnPageChildChanged(object? sender, EventArgs e)
    {
        _focusScopeStack.Clear();
        _pendingPageFocus = true;
    }

    private static void OnAnyMouseDown(object sender, MouseButtonEventArgs e)
    {
        _gamepadActive = false;
        if (_highlightBorder is not null)
            _highlightBorder.Visibility = Visibility.Collapsed;
    }

    private static void OnAnyMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(ModMain.FrmMain);
        var dx = pos.X - _lastMousePos.X;
        var dy = pos.Y - _lastMousePos.Y;
        _lastMousePos = pos;

        if (_gamepadActive && (Math.Abs(dx) > MouseHideThreshold || Math.Abs(dy) > MouseHideThreshold))
        {
            _gamepadActive = false;
            if (_highlightBorder is not null)
                _highlightBorder.Visibility = Visibility.Collapsed;
        }
    }

    private static void OnAnyKeyDown(object sender, KeyEventArgs e)
    {
        if (_isSimulatingInput) return;
        _gamepadActive = false;
        if (_highlightBorder is not null)
            _highlightBorder.Visibility = Visibility.Collapsed;
    }

    private static void UpdateHighlightFromFocus()
    {
        if (_highlightBorder is null) return;

        var frm = ModMain.FrmMain;
        if (frm is null) return;

        if (_isTitleBarMode)
            UpdateTitleBarHighlight();
        else if (_gamepadActive)
            UpdateNormalHighlight();
        else
            _highlightBorder.Visibility = Visibility.Collapsed;
    }

    private static void UpdateTitleBarHighlight()
    {
        var btn = GetTitleBarButton(0);
        if (btn is null || !btn.IsVisible || !btn.IsEnabled)
        {
            _highlightBorder.Visibility = Visibility.Collapsed;
            return;
        }

        _highlightBorder.BorderBrush = _titleBarBorderBrush;
        _highlightBorder.Background = _titleBarBackground;

        try
        {
            var transform = btn.TransformToAncestor(ModMain.FrmMain.PanForm);
            var position = transform.Transform(new Point(0, 0));

            _highlightBorder.Width = Math.Max(btn.ActualWidth, 4);
            _highlightBorder.Height = Math.Max(btn.ActualHeight, 4);
            _highlightBorder.CornerRadius = new CornerRadius(4);

            Canvas.SetLeft(_highlightBorder, position.X);
            Canvas.SetTop(_highlightBorder, position.Y);
            _highlightBorder.Visibility = Visibility.Visible;
        }
        catch
        {
            _highlightBorder.Visibility = Visibility.Collapsed;
        }
    }

    private static void UpdateNormalHighlight()
    {
        if (!_gamepadActive)
        {
            _highlightBorder.Visibility = Visibility.Collapsed;
            return;
        }

        var frm = ModMain.FrmMain;
        var focused = Keyboard.FocusedElement as FrameworkElement;
        if (focused is null || !focused.IsVisible || !focused.IsEnabled
            || !KeyControllAbility.GetCanSelect(focused))
        {
            _highlightBorder.Visibility = Visibility.Collapsed;
            return;
        }

        _highlightBorder.BorderBrush = _highlightBorderBrush;
        _highlightBorder.Background = _highlightBackground;

        try
        {
            var transform = focused.TransformToAncestor(frm.PanForm);
            var position = transform.Transform(new Point(0, 0));

            _highlightBorder.Width = Math.Max(focused.ActualWidth, 4);
            _highlightBorder.Height = Math.Max(focused.ActualHeight, 4);
            _highlightBorder.CornerRadius = focused is Border b
                ? b.CornerRadius
                : new CornerRadius(4);

            Canvas.SetLeft(_highlightBorder, position.X);
            Canvas.SetTop(_highlightBorder, position.Y);
            _highlightBorder.Visibility = Visibility.Visible;
        }
        catch
        {
            _highlightBorder.Visibility = Visibility.Collapsed;
        }
    }

    private static MyIconButton? GetTitleBarButton(int direction)
    {
        if (_btnTitleMin is null && _btnTitleHelp is null && _btnTitleClose is null)
            return null;

        var visible = new List<int>();
        if (_btnTitleMin is { Visibility: Visibility.Visible }) visible.Add(0);
        if (_btnTitleHelp is { Visibility: Visibility.Visible }) visible.Add(1);
        if (_btnTitleClose is { Visibility: Visibility.Visible }) visible.Add(2);

        if (visible.Count == 0) return null;

        if (direction == 0)
        {
            var idx = visible.IndexOf(_titleBarIndex);
            return idx >= 0 ? GetButtonByIndex(_titleBarIndex) : GetButtonByIndex(visible[0]);
        }

        var currentPos = visible.IndexOf(_titleBarIndex);
        if (currentPos < 0) currentPos = -direction;

        var newPos = (currentPos + direction + visible.Count) % visible.Count;
        _titleBarIndex = visible[newPos];
        return GetButtonByIndex(_titleBarIndex);
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

        var btn = GetTitleBarButton(0);
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

    private static void CheckPageChanged()
    {
        if (!_pendingPageFocus)
            return;

        var frm = ModMain.FrmMain;
        if (frm is null) return;

        foreach (var page in new DependencyObject?[] { frm.PageLeft, frm.PageRight })
        {
            var first = KeyControllAbility.FindFirstSelectable(page);
            if (first is not null)
            {
                first.Focus();
                _pendingPageFocus = false;
                if (_gamepadActive)
                    UpdateHighlightFromFocus();
                return;
            }
        }
    }

    private static bool IsFocusOnLeftPage()
    {
        var frm = ModMain.FrmMain;
        if (frm is null) return true;

        var focused = Keyboard.FocusedElement as DependencyObject;
        if (focused is null) return true;

        while (focused is not null)
        {
            if (focused == frm.PanMainLeft) return true;
            if (focused == frm.PanMainRight) return false;
            focused = VisualTreeHelper.GetParent(focused);
        }

        return frm.PanMainLeft.Visibility == Visibility.Visible;
    }

    private static void PollLoop()
    {
        var focusPollCounter = 0;

        while (_isRunning)
        {
            try
            {
                if (!_controller.IsConnected)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                var state = _controller.GetState();
                var current = state.Gamepad.Buttons;
                var pressed = current & ~_prevButtons;
                var released = _prevButtons & ~current;

                if (pressed != GamepadButtonFlags.None)
                {
                    RunOnUiThread(() => HandleButtonPress(pressed));
                    if (IsDPadDirection(pressed))
                    {
                        _repeatButton = pressed;
                        _lastRepeatTime = DateTime.UtcNow;
                    }
                }

                if ((current & _repeatButton) != 0 && IsDPadDirection(_repeatButton))
                {
                    var elapsed = (DateTime.UtcNow - _lastRepeatTime).TotalMilliseconds;
                    if (elapsed >= RepeatDelayMs)
                    {
                        var sinceRepeat = elapsed - RepeatDelayMs;
                        if (sinceRepeat >= RepeatRateMs)
                        {
                            _lastRepeatTime = DateTime.UtcNow.AddMilliseconds(-(sinceRepeat - RepeatRateMs));
                            RunOnUiThread(() => HandleButtonPress(_repeatButton));
                        }
                    }
                }

                if (released != GamepadButtonFlags.None && (_repeatButton & released) != 0)
                    _repeatButton = 0;

                _prevButtons = current;

                HandleRightStickScroll(state.Gamepad.RightThumbY);

                if (++focusPollCounter >= HighlightRefreshInterval)
                {
                    focusPollCounter = 0;
                    RunOnUiThread(() =>
                    {
                        CheckPageChanged();
                        UpdateHighlightFromFocus();
                    });
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "Gamepad poll error");
            }

            Thread.Sleep(PollIntervalMs);
        }
    }

    private static void HandleRightStickScroll(short thumbY)
    {
        if (thumbY > -RightStickDeadZone && thumbY < RightStickDeadZone)
        {
            _scrollAccumulator = 0;
            return;
        }

        _scrollAccumulator += thumbY * ScrollSensitivity;
        if (_scrollAccumulator >= 1.0 || _scrollAccumulator <= -1.0)
        {
            var delta = (int)_scrollAccumulator;
            _scrollAccumulator -= delta;
            RunOnUiThread(() => SimulateScroll(delta * ScrollWheelMultiplier));
        }
    }

    private static void SimulateScroll(int delta)
    {
        if (delta == 0) return;
        _isSimulatingInput = true;

        var frm = ModMain.FrmMain;
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

    private static bool IsDPadDirection(GamepadButtonFlags btn)
    {
        return btn is GamepadButtonFlags.DPadUp or GamepadButtonFlags.DPadDown
            or GamepadButtonFlags.DPadLeft or GamepadButtonFlags.DPadRight;
    }

    private static void RunOnUiThread(Action action)
    {
        if (ModMain.FrmMain is null) return;
        ModMain.FrmMain.Dispatcher.Invoke(action);
    }

    private static void HandleButtonPress(GamepadButtonFlags buttons)
    {
        var frm = ModMain.FrmMain;
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
            var dir = buttons == GamepadButtonFlags.DPadRight ? 1 : -1;
            GetTitleBarButton(dir);
            var btn = GetTitleBarButton(0);
            if (btn is not null)
                btn.Focus();
            UpdateHighlightFromFocus();
        }
        else if (buttons.HasFlag(GamepadButtonFlags.A))
        {
            var btn = GetTitleBarButton(0);
            if (btn is not null && btn.IsVisible && btn.IsEnabled)
                KeyControllAbility.Activate(btn);
            ExitTitleBarMode();
        }
        else if (buttons.HasFlag(GamepadButtonFlags.B) || buttons.HasFlag(GamepadButtonFlags.Start))
        {
            ExitTitleBarMode();
        }

        UpdateHighlightFromFocus();
    }

    private static void TryFocusOnPage(DependencyObject? page)
    {
        var first = KeyControllAbility.FindFirstSelectable(page);
        if (first is null)
        {
            var opposite = ReferenceEquals(page, ModMain.FrmMain?.PageLeft)
                ? (DependencyObject?)ModMain.FrmMain?.PageRight
                : (DependencyObject?)ModMain.FrmMain?.PageLeft;
            if (opposite != page)
                first = KeyControllAbility.FindFirstSelectable(opposite);
        }
        first?.Focus();
    }

    private static bool MoveFocus(FocusNavigationDirection direction)
    {
        var request = new TraversalRequest(direction);
        var prevFocus = Keyboard.FocusedElement;
        var focused = prevFocus as UIElement;

        bool moved = focused is not null
            ? focused.MoveFocus(request)
            : ModMain.FrmMain is not null && ((UIElement)ModMain.FrmMain).MoveFocus(request);

        if (!moved)
            return false;

        var newFocus = Keyboard.FocusedElement;
        return newFocus != prevFocus
            && newFocus is FrameworkElement fe
            && KeyControllAbility.GetCanSelect(fe);
    }

    private static void NavigateTitleTab(int direction)
    {
        var frm = ModMain.FrmMain;
        if (frm is null) return;

        if (frm.PanTitleMain.Visibility != Visibility.Visible)
            return;

        var buttons = frm.PanTitleSelect.Children;
        var currentIndex = 0;
        for (var i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] is MyRadioButton { Checked: true })
            {
                currentIndex = i;
                break;
            }
        }

        var newIndex = (currentIndex + direction + buttons.Count) % buttons.Count;
        if (buttons[newIndex] is MyRadioButton target)
            target.SetChecked(true, true, true);
    }

    private static void SimulateKey(Key key)
    {
        _isSimulatingInput = true;
        var frm = ModMain.FrmMain;
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

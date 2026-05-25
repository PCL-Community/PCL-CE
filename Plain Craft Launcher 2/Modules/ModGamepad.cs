using System.Windows;
using System.Windows.Controls;
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

    private static MyPageLeft _lastPageLeft;
    private static MyPageRight _lastPageRight;
    private static bool _pendingPageFocus;

    private static double _scrollAccumulator;

    private const int PollIntervalMs = 30;
    private const int HighlightRefreshInterval = 5;
    private const short RightStickDeadZone = 4000;
    private const double ScrollSensitivity = 0.03;
    private const double MouseHideThreshold = 8;
    private const int ScrollWheelMultiplier = -120;
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

        _highlightBorder = new Border
        {
            IsHitTestVisible = false,
            BorderBrush = new SolidColorBrush(Color.FromArgb(200, 0, 120, 215)),
            BorderThickness = new Thickness(2.5),
            Background = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215)),
            CornerRadius = new CornerRadius(6),
            Visibility = Visibility.Collapsed,
            Focusable = false
        };
        _highlightCanvas.Children.Add(_highlightBorder);

        var panMainIdx = frm.PanForm.Children.IndexOf(frm.PanMain);
        frm.PanForm.Children.Insert(panMainIdx + 1, _highlightCanvas);

        frm.PreviewMouseDown += OnAnyMouseDown;
        frm.PreviewMouseMove += OnAnyMouseMove;
        frm.PreviewKeyDown += OnAnyKeyDown;
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

        if (!_gamepadActive)
        {
            _highlightBorder.Visibility = Visibility.Collapsed;
            return;
        }

        var frm = ModMain.FrmMain;
        if (frm is null) return;

        var focused = Keyboard.FocusedElement as FrameworkElement;
        if (focused is null || !focused.IsVisible || !focused.IsEnabled
            || !KeyControllAbility.GetCanSelect(focused))
        {
            _highlightBorder.Visibility = Visibility.Collapsed;
            return;
        }

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

    private static void CheckPageChanged()
    {
        var frm = ModMain.FrmMain;
        if (frm is null) return;

        var pageChanged = !ReferenceEquals(frm.PageLeft, _lastPageLeft)
                       || !ReferenceEquals(frm.PageRight, _lastPageRight);

        if (pageChanged)
            _pendingPageFocus = true;

        if (!_pendingPageFocus)
            return;

        if (TryFocusFirstLeftElement())
        {
            _pendingPageFocus = false;
            _lastPageLeft = frm.PageLeft;
            _lastPageRight = frm.PageRight;
            if (_gamepadActive)
                UpdateHighlightFromFocus();
        }
    }

    private static bool TryFocusFirstLeftElement()
    {
        var frm = ModMain.FrmMain;
        var first = KeyControllAbility.FindFirstSelectable(frm?.PageLeft);
        if (first is null) return false;

        first.Focus();
        return true;
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

        if (frm.PanMsg.Children.Count > 0)
        {
            InvokeDialogButton(frm.PanMsg.Children[0], buttons);
            return;
        }

        if (IsDPadDirection(buttons))
        {
            _gamepadActive = true;
            _lastMousePos = Mouse.GetPosition(frm);
            MoveFocus(buttons switch
            {
                GamepadButtonFlags.DPadUp => FocusNavigationDirection.Up,
                GamepadButtonFlags.DPadDown => FocusNavigationDirection.Down,
                GamepadButtonFlags.DPadLeft => FocusNavigationDirection.Left,
                _ => FocusNavigationDirection.Right
            });
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
                TryFocusFirstLeftElement();
            }
        }
        else if (buttons.HasFlag(GamepadButtonFlags.B))
            SimulateKey(Key.Escape);
        else if (buttons.HasFlag(GamepadButtonFlags.Y))
            SimulateKey(Key.Apps);
        else if (buttons.HasFlag(GamepadButtonFlags.LeftShoulder))
            NavigateTitleTab(-1);
        else if (buttons.HasFlag(GamepadButtonFlags.RightShoulder))
            NavigateTitleTab(1);
        else if (buttons.HasFlag(GamepadButtonFlags.Start))
            frm.WindowState = WindowState.Minimized;
        else if (buttons.HasFlag(GamepadButtonFlags.Back))
            frm.EndProgram(true);

        UpdateHighlightFromFocus();
    }

    private static void MoveFocus(FocusNavigationDirection direction)
    {
        var request = new TraversalRequest(direction);
        var focused = Keyboard.FocusedElement;
        if (focused is UIElement ue)
        {
            ue.MoveFocus(request);
        }
        else
        {
            var frm = ModMain.FrmMain;
            if (frm is not null)
                ((UIElement)frm).MoveFocus(request);
        }
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

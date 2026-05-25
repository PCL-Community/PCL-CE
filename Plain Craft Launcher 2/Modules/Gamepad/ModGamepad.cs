using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SharpDX.XInput;

namespace PCL.Modules.Gamepad;

public static partial class ModGamepad
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

    private static bool _pendingPageFocus = true;
    private static DependencyPropertyDescriptor? _pageChildProp;

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
        RunOnUiThread(RemovePageChangeHandler);
    }

    private static void RemovePageChangeHandler()
    {
        if (_pageChildProp is null) return;
        var frm = GetForm();
        if (frm is null) return;
        _pageChildProp.RemoveValueChanged(frm.PanMainLeft, OnPageChildChanged);
        _pageChildProp.RemoveValueChanged(frm.PanMainRight, OnPageChildChanged);
    }

    private static FormMain? GetForm() => ModMain.FrmMain;

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

    private static void RunOnUiThread(Action action)
    {
        GetForm()?.Dispatcher.Invoke(action);
    }

    private static bool IsDPadDirection(GamepadButtonFlags btn) => btn
        is GamepadButtonFlags.DPadUp or GamepadButtonFlags.DPadDown
        or GamepadButtonFlags.DPadLeft or GamepadButtonFlags.DPadRight;
}

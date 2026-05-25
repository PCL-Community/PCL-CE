using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PCL.Modules.Gamepad;

public static partial class ModGamepad
{
    private static void CreateHighlightOverlay()
    {
        var frm = GetForm();
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
            _pageChildProp = childProp;
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

    private static void HideHighlight()
    {
        if (_highlightBorder is not null)
            _highlightBorder.Visibility = Visibility.Collapsed;
    }

    private static void OnAnyMouseDown(object sender, MouseButtonEventArgs e)
    {
        _gamepadActive = false;
        HideHighlight();
    }

    private static void OnAnyMouseMove(object sender, MouseEventArgs e)
    {
        var frm = GetForm();
        if (frm is null) return;
        var pos = e.GetPosition(frm);
        var dx = pos.X - _lastMousePos.X;
        var dy = pos.Y - _lastMousePos.Y;
        _lastMousePos = pos;

        if (_gamepadActive && (Math.Abs(dx) > MouseHideThreshold || Math.Abs(dy) > MouseHideThreshold))
        {
            _gamepadActive = false;
            HideHighlight();
        }
    }

    private static void OnAnyKeyDown(object sender, KeyEventArgs e)
    {
        if (_isSimulatingInput) return;
        _gamepadActive = false;
        HideHighlight();
    }

    private static void UpdateHighlightFromFocus()
    {
        if (_highlightBorder is null) return;

        if (_isTitleBarMode)
            UpdateTitleBarHighlight();
        else if (_gamepadActive)
            UpdateNormalHighlight();
        else
            HideHighlight();
    }

    private static void UpdateTitleBarHighlight()
    {
        var btn = GetTitleBarButton();
        if (btn is null || !btn.IsVisible || !btn.IsEnabled)
        {
            HideHighlight();
            return;
        }

        var frm = GetForm();
        if (frm is null) return;

        _highlightBorder.BorderBrush = _titleBarBorderBrush;
        _highlightBorder.Background = _titleBarBackground;

        try
        {
            var transform = btn.TransformToAncestor(frm.PanForm);
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
            HideHighlight();
        }
    }

    private static void UpdateNormalHighlight()
    {
        if (!_gamepadActive)
        {
            HideHighlight();
            return;
        }

        var focused = Keyboard.FocusedElement as FrameworkElement;
        if (focused is null || !focused.IsVisible || !focused.IsEnabled
            || !KeyControllAbility.GetCanSelect(focused))
        {
            HideHighlight();
            return;
        }

        var frm = GetForm();
        if (frm is null) return;

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
            HideHighlight();
        }
    }
}

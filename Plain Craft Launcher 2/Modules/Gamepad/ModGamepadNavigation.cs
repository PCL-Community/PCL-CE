using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SharpDX.XInput;

namespace PCL.Modules.Gamepad;

public static partial class ModGamepad
{
    private static void CheckPageChanged()
    {
        if (!_pendingPageFocus)
            return;

        var frm = GetForm();
        if (frm is null) return;

        var first = KeyControllAbility.FindFirstSelectable(frm.PageLeft)
            ?? KeyControllAbility.FindFirstSelectable(frm.PageRight);
        if (first is not null)
        {
            first.Focus();
            _pendingPageFocus = false;
            if (_gamepadActive)
                UpdateHighlightFromFocus();
        }
    }

    private static bool IsFocusOnLeftPage()
    {
        var frm = GetForm();
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

    private static void TryFocusOnPage(DependencyObject? page)
    {
        var first = KeyControllAbility.FindFirstSelectable(page);
        if (first is null)
        {
            var frm = GetForm();
            var opposite = ReferenceEquals(page, frm?.PageLeft)
                ? (DependencyObject?)frm?.PageRight
                : (DependencyObject?)frm?.PageLeft;
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

        bool moved;
        if (focused is not null)
        {
            moved = focused.MoveFocus(request);
        }
        else
        {
            var frm = GetForm();
            moved = frm is not null && ((UIElement)frm).MoveFocus(request);
        }

        if (!moved)
            return false;

        var newFocus = Keyboard.FocusedElement;
        return newFocus != prevFocus
            && newFocus is FrameworkElement fe
            && KeyControllAbility.GetCanSelect(fe);
    }

    private static void NavigateTitleTab(int direction)
    {
        var frm = GetForm();
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
}

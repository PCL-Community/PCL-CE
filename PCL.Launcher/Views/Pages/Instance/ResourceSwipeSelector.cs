using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     资源列表的滑动选择控制器。
///     只保留当前连续区间；指针滑回时，离开区间的条目会恢复到滑动开始前的状态。
/// </summary>
public sealed class ResourceSwipeSelector
{
    private readonly Func<int> _count;
    private readonly Func<int, bool> _isSelected;
    private readonly Dictionary<int, bool> _originalStates = [];
    private readonly Panel _panel;
    private readonly Action _refreshBar;
    private readonly MyResourceSelectBar _selectBar;
    private readonly Action<int, bool, bool> _setSelected;

    private bool _candidate;
    private Point _pressPoint;
    private int _rangeEnd;
    private int _rangeStart;
    private int _startIndex = -1;
    private bool _targetChecked;

    public ResourceSwipeSelector(
        Panel panel,
        MyResourceSelectBar selectBar,
        Func<int> count,
        Func<int, bool> isSelected,
        Action<int, bool, bool> setSelected,
        Action refreshBar)
    {
        _panel = panel;
        _selectBar = selectBar;
        _count = count;
        _isSelected = isSelected;
        _setSelected = setSelected;
        _refreshBar = refreshBar;

        _panel.AddHandler(
            InputElement.PointerPressedEvent,
            OnPointerPressed,
            RoutingStrategies.Tunnel);
        _panel.AddHandler(
            InputElement.PointerMovedEvent,
            OnPointerMoved,
            RoutingStrategies.Tunnel);
        _panel.AddHandler(
            InputElement.PointerReleasedEvent,
            OnPointerReleased,
            RoutingStrategies.Tunnel);
        _panel.AddHandler(
            InputElement.PointerCaptureLostEvent,
            OnPointerCaptureLost,
            RoutingStrategies.Tunnel);
    }

    public bool IsSwiping { get; private set; }

    /// <summary>页面退出、列表重建或搜索变化时调用，恢复所有被滑动触碰的条目。</summary>
    public void Cancel()
    {
        if (IsSwiping || _candidate)
            RestoreAll();
        Reset();
        _selectBar.IsSwipeActive = false;
        _refreshBar();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(_panel);
        if (!point.Properties.IsLeftButtonPressed
            || IsPointerOnActionButton(e.Source as Visual))
            return;

        var index = IndexAt(e.GetPosition(_panel));
        if (index < 0)
            return;

        _candidate = true;
        IsSwiping = false;
        _pressPoint = e.GetPosition(_panel);
        _startIndex = index;
        _rangeStart = index;
        _rangeEnd = index;
        _targetChecked = !_isSelected(index);
        _originalStates.Clear();
        RecordOriginal(index);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_candidate)
            return;
        if (!e.GetCurrentPoint(_panel).Properties.IsLeftButtonPressed)
        {
            Cancel();
            return;
        }

        var position = e.GetPosition(_panel);
        switch (IsSwiping)
        {
            case false when
                Math.Abs(position.Y - _pressPoint.Y) < 5d
                && Math.Abs(position.X - _pressPoint.X) < 5d:
                return;

            case false:
                IsSwiping = true;
                _selectBar.IsSwipeActive = true;
                _selectBar.IsHitTestVisible = false;
                e.Pointer.Capture(_panel);
                break;
        }

        var index = IndexAt(position);
        if (index < 0)
            return;
        MoveTo(index);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        var wasSwiping = IsSwiping;
        Complete();
        if (wasSwiping)
            e.Pointer.Capture(null);
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_candidate && ReferenceEquals(e.Source, _panel))
            Cancel();
    }

    private void MoveTo(int index)
    {
        index = Math.Clamp(index, 0, Math.Max(0, _count() - 1));
        var newStart = Math.Min(_startIndex, index);
        var newEnd = Math.Max(_startIndex, index);

        for (var i = _rangeStart; i <= _rangeEnd; i++)
            if (i < newStart || i > newEnd)
                RestoreIndex(i);

        _rangeStart = newStart;
        _rangeEnd = newEnd;
        for (var i = newStart; i <= newEnd; i++)
        {
            RecordOriginal(i);
            _setSelected(i, _targetChecked, false);
        }

        _refreshBar();
    }

    private void RecordOriginal(int index)
    {
        if (!_originalStates.ContainsKey(index))
            _originalStates[index] = _isSelected(index);
    }

    private void RestoreIndex(int index)
    {
        if (!_originalStates.TryGetValue(index, out var original))
            return;
        _setSelected(index, original, false);
    }

    private void RestoreAll()
    {
        foreach (var (index, original) in _originalStates)
            if (index >= 0 && index < _count())
                _setSelected(index, original, false);
    }

    private void Complete()
    {
        Reset();
        _selectBar.IsSwipeActive = false;
        _selectBar.IsHitTestVisible = true;
        _refreshBar();
    }

    private void Reset()
    {
        _candidate = false;
        IsSwiping = false;
        _startIndex = -1;
        _rangeStart = 0;
        _rangeEnd = 0;
        _originalStates.Clear();
        _selectBar.IsSwipeActive = false;
        _selectBar.IsHitTestVisible = true;
    }

    private int IndexAt(Point point)
    {
        var count = Math.Min(_panel.Children.Count, _count());
        for (var i = 0; i < count; i++)
        {
            var child = _panel.Children[i];
            var origin = child.TranslatePoint(default, _panel);
            if (origin is not null
                && new Rect(origin.Value, child.Bounds.Size).Contains(point))
                return i;
        }

        return -1;
    }

    private static bool IsPointerOnActionButton(Visual? source)
    {
        for (var current = source; current is not null; current = current.GetVisualParent())
            if (current is MyIconButton or MyIconTextButton)
                return true;
        return false;
    }
}
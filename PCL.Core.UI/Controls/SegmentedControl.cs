using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using PCL.Core.UI.Animation;
using PCL.Core.UI.Animation.Core;
using PCL.Core.UI.Animation.Easings;

namespace PCL.Core.UI.Controls;

/// <summary>通用分段选择控件：等宽内容项、滑动选中块、鼠标与键盘选择。 文本、图标和具体业务由调用方通过 <see cref="Items" /> 提供。</summary>
public sealed class SegmentedControl : UserControl
{
    public delegate void SelectionChangedEventHandler(
        SegmentedControl sender,
        int index,
        bool raiseByMouse);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<SegmentedControl, int>(
            nameof(SelectedIndex),
            defaultBindingMode: BindingMode.TwoWay);

    private static int _uuidSeed;
    private readonly string _indicatorKey;
    private readonly TranslateTransform _indicatorTransform = new(0d, 0d);

    private readonly Dictionary<SegmentedItem, EventHandler<PointerPressedEventArgs>>
        _itemHandlers = [];

    private readonly Border _panBack;
    private readonly Border _panSelect;
    private readonly UniformGrid _segmentHost;

    private bool _animateNextSelection;

    public SegmentedControl()
    {
        _indicatorKey = $"SegmentedControl Indicator {Interlocked.Increment(ref _uuidSeed)}";

        _segmentHost = new UniformGrid
        {
            Columns = 3
        };

        _panSelect = new Border
        {
            Width = 0d,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(5d),
            IsHitTestVisible = false
        };
        _panSelect.RenderTransform = _indicatorTransform;
        _panSelect.Bind(
            Border.BackgroundProperty,
            new DynamicResourceExtension("ColorBrushWhite"));
        _panSelect.Bind(
            Border.BorderBrushProperty,
            new DynamicResourceExtension("ColorBrushGray5"));

        _panBack = new Border
        {
            Height = 28d,
            Padding = new Thickness(2d),
            CornerRadius = new CornerRadius(6d),
            ClipToBounds = true
        };
        _panBack.Bind(
            Border.BackgroundProperty,
            new DynamicResourceExtension("ColorBrush8"));

        var grid = new Grid();
        grid.Children.Add(_panSelect);
        grid.Children.Add(_segmentHost);
        _panBack.Child = grid;
        Content = _panBack;

        MinHeight = 28d;
        Focusable = true;

        Items.CollectionChanged += OnItemsChanged;
        Loaded += (_, _) =>
        {
            UpdateVisuals();
            UpdateIndicator(false);
        };
        _panBack.SizeChanged += (_, _) => UpdateIndicator(false);
        Unloaded += (_, _) => AnimationService.CancelAnimationByName(_indicatorKey);
    }

    public ObservableCollection<SegmentedItem> Items { get; } = [];

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetCurrentValue(SelectedIndexProperty, value);
    }

    public event SelectionChangedEventHandler? SelectionChanged;

    /// <summary>设置当前选项。由页面同步外部状态时使用，不触发 <see cref="SelectionChanged" />。</summary>
    public void Select(int index, bool animate)
    {
        var maxIndex = Math.Max(0, Items.Count - 1);
        index = Math.Clamp(index, 0, maxIndex);
        if (SelectedIndex == index)
        {
            UpdateVisuals();
            UpdateIndicator(animate);
            return;
        }

        _animateNextSelection = animate;
        SetCurrentValue(SelectedIndexProperty, index);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != SelectedIndexProperty) return;

        UpdateVisuals();
        UpdateIndicator(_animateNextSelection);
        _animateNextSelection = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Items.Count == 0)
        {
            base.OnKeyDown(e);
            return;
        }

        if (e.Key is Key.Left or Key.Up)
        {
            SelectFromUser((SelectedIndex + Items.Count - 1) % Items.Count);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Right or Key.Down)
        {
            SelectFromUser((SelectedIndex + 1) % Items.Count);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var (item, handler) in _itemHandlers)
            item.PointerPressed -= handler;
        _itemHandlers.Clear();
        _segmentHost.Children.Clear();

        for (var index = 0; index < Items.Count; index++)
        {
            var itemIndex = index;
            var item = Items[index];
            EventHandler<PointerPressedEventArgs> handler = (_, args) =>
            {
                if (!args.GetCurrentPoint(item).Properties.IsLeftButtonPressed) return;
                Focus();
                SelectFromUser(itemIndex);
                args.Handled = true;
            };
            item.PointerPressed += handler;
            _itemHandlers[item] = handler;
            _segmentHost.Children.Add(item);
        }

        _segmentHost.Columns = Math.Max(1, Items.Count);
        var maxIndex = Math.Max(0, Items.Count - 1);
        if (SelectedIndex > maxIndex)
        {
            _animateNextSelection = false;
            SetCurrentValue(SelectedIndexProperty, maxIndex);
            return;
        }

        UpdateVisuals();
        UpdateIndicator(false);
    }

    private void SelectFromUser(int index)
    {
        if (index == SelectedIndex)
        {
            UpdateIndicator(true);
            return;
        }

        _animateNextSelection = true;
        SetCurrentValue(SelectedIndexProperty, index);
        SelectionChanged?.Invoke(this, index, true);
    }

    private void UpdateVisuals()
    {
        for (var index = 0; index < Items.Count; index++)
            Items[index].Classes.Set("selected", index == SelectedIndex);
    }

    private void UpdateIndicator(bool animate)
    {
        if (!IsLoaded || Items.Count == 0) return;

        var index = Math.Clamp(SelectedIndex, 0, Items.Count - 1);
        var item = Items[index];
        var width = item.Bounds.Width;
        if (width <= 0d) return;

        var targetX = item.Bounds.X;
        var currentWidth = double.IsNaN(_panSelect.Width) ? 0d : _panSelect.Width;
        var currentX = _indicatorTransform.X;

        if (!animate || !CanAnimate())
        {
            AnimationService.CancelAnimationByName(_indicatorKey);
            _panSelect.Width = width;
            _indicatorTransform.X = targetX;
            return;
        }

        AnimationService.Run(
            new List<IAnimation>
            {
                new WidthAnimation(
                    _panSelect,
                    width - currentWidth,
                    180,
                    ease: new FluentEaseOut(EasePower.Weak)),
                new TranslateXAnimation(
                    _panSelect,
                    targetX - currentX,
                    180,
                    ease: new FluentEaseOut(EasePower.Weak))
            },
            _indicatorKey);
    }

    private bool CanAnimate()
    {
        return AnimationService.IsEnabled
            && IsLoaded
            && TopLevel.GetTopLevel(this) is not null
            && !AnimationService.IsSuspended;
    }
}
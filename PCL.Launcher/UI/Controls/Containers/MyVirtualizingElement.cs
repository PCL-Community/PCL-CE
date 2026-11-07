using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace PCL.UI.Controls;

/// <summary>
///     懒加载占位控件：首次进入可视区域时用真实控件替换自己。
///     设置 <see cref="RecycleOffscreen" /> 后，真实控件离开可视区域超过一个视口时
///     会被移除并重新换回占位控件，避免长列表持续持有全部真实控件。
/// </summary>
public class MyVirtualizingElement : Control
{
    private readonly Func<Control> _initializer;
    private bool _isAttached;
    private Control? _realControl;
    private ScrollViewer? _viewer;

    public MyVirtualizingElement(Func<Control> initializer)
    {
        _initializer = initializer;
        AttachedToVisualTree += (_, _) =>
        {
            _isAttached = true;
            LayoutUpdated += OnLayoutUpdated;
            _viewer = this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
            if (_viewer is not null)
                _viewer.ScrollChanged += OnScrollChanged;
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _isAttached = false;
            LayoutUpdated -= OnLayoutUpdated;
            if (_viewer is not null)
                _viewer.ScrollChanged -= OnScrollChanged;
            _viewer = null;
        };
    }

    /// <summary>是否在离屏后回收真实控件。默认关闭，保持原有懒加载行为。</summary>
    public bool RecycleOffscreen { get; set; }

    /// <summary>真实控件被回收时触发，用于释放图片等资源。</summary>
    public event Action<Control>? Recycled;

    /// <summary>实例化此控件，并在父级 Panel 中替换自己的位置。</summary>
    public virtual Control Init()
    {
        if (_realControl is not null)
            return _realControl;

        var element = _initializer();
        _realControl = element;
        if (Parent is null)
            return element;
        if (Parent is not Panel panel)
            throw new Exception("MyVirtualizingElement 的父级必须是一个 Panel");

        var index = panel.Children.IndexOf(this);
        if (index >= 0)
        {
            panel.Children.RemoveAt(index);
            panel.Children.Insert(index, element);
        }

        return element;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        Evaluate();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        Evaluate();
    }

    private void Evaluate()
    {
        if (!_isAttached)
            return;

        var viewer = _viewer ?? this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        if (_realControl is not null)
        {
            if (RecycleOffscreen && viewer is not null && IsFarOutside(viewer, _realControl))
                Recycle();
            return;
        }

        if (Bounds.Width <= 0.5d || Bounds.Height <= 0.5d)
            return;

        if (viewer is null)
        {
            Init();
            return;
        }

        if (IsVisibleIn(viewer, this))
            Init();
    }

    private static bool IsVisibleIn(ScrollViewer viewer, Control element)
    {
        var point = element.TranslatePoint(new Point(0d, 0d), viewer);
        if (point is null) return false;
        var viewport = new Rect(0d, 0d, viewer.Viewport.Width, viewer.Viewport.Height);
        return viewport.Intersects(
            new Rect(point.Value.X, point.Value.Y, element.Bounds.Width, element.Bounds.Height));
    }

    private static bool IsFarOutside(ScrollViewer viewer, Control element)
    {
        var point = element.TranslatePoint(new Point(0d, 0d), viewer);
        if (point is null) return false;
        var height = Math.Max(element.Bounds.Height, 1d);
        var margin = Math.Max(viewer.Viewport.Height, height);
        var viewport = new Rect(
            0d,
            -margin,
            viewer.Viewport.Width,
            viewer.Viewport.Height + margin * 2d);
        return !viewport.Intersects(
            new Rect(point.Value.X, point.Value.Y, element.Bounds.Width, height));
    }

    private void Recycle()
    {
        var real = _realControl;
        _realControl = null;
        if (real is null) return;

        // 先在仍处于视觉树内时释放图片等资源，再替换回占位控件，
        // 避免回收后 GetVisualDescendants 无法遍历到真实内容。
        Recycled?.Invoke(real);

        if (real.Parent is Panel panel)
        {
            var index = panel.Children.IndexOf(real);
            if (index >= 0)
            {
                panel.Children.RemoveAt(index);
                panel.Children.Insert(index, this);
            }
        }
    }

    /// <summary>
    ///     获取实例化后的控件；若尚未实例化则立即实例化。
    /// </summary>
    public static Control TryInit(Control element)
    {
        var type = element.GetType();
        if (type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(MyVirtualizingElement<>))
        {
            var method = type.GetMethod(nameof(Init), Type.EmptyTypes);
            if (method?.Invoke(element, null) is Control result)
                return result;
        }

        return element is MyVirtualizingElement virtualizing ? virtualizing.Init() : element;
    }
}

/// <summary>懒加载占位控件的泛型形式。</summary>
public class MyVirtualizingElement<T>(
    Func<T> initializer)
    : MyVirtualizingElement(initializer) where T : Control
{
    public override T Init()
    {
        return (T)base.Init();
    }

    public static implicit operator T(MyVirtualizingElement<T> virtualized)
    {
        return virtualized.Init();
    }
}
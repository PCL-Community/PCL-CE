using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace PCL.UI.Controls;

/// <summary>
///     懒加载占位控件：首次进入可视区域时用真实控件替换自己。
///     首次进入可视区域时用实际控件替换占位控件。
/// </summary>
public class MyVirtualizingElement : Control
{
    private readonly Func<Control> _initializer;
    private bool _isAttached;
    private bool _isInitialized;

    public MyVirtualizingElement(Func<Control> initializer)
    {
        _initializer = initializer;
        AttachedToVisualTree += (_, _) =>
        {
            _isAttached = true;
            LayoutUpdated += OnLayoutUpdated;
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _isAttached = false;
            LayoutUpdated -= OnLayoutUpdated;
        };
    }

    /// <summary>实例化此控件，并在父级 Panel 中替换自己的位置。</summary>
    public virtual Control Init()
    {
        _isInitialized = true;
        var element = _initializer();
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
        if (_isInitialized || !_isAttached)
            return;
        if (Bounds.Width <= 0.5d || Bounds.Height <= 0.5d)
            return;

        var viewer = this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        if (viewer is null)
        {
            Init();
            return;
        }

        var point = this.TranslatePoint(new Point(0d, 0d), viewer);
        if (point is null)
            return;

        var viewport = new Rect(0d, 0d, viewer.Viewport.Width, viewer.Viewport.Height);
        var bounds = new Rect(point.Value.X, point.Value.Y, Bounds.Width, Bounds.Height);
        if (!viewport.Intersects(bounds))
            return;

        Init();
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
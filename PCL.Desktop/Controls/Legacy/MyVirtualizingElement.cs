// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;

namespace PCL.Desktop.Controls.Legacy;

public class MyVirtualizingElement<T>(Func<T> initializer) : Control, IMyVirtualizingElement
    where T : Control
{
    private T? _initializedElement;

    /// <summary>
    ///     实例化此控件，并在父级 Panel 中用真实控件替换占位控件。
    /// </summary>
    public T Init()
    {
        if (_initializedElement is not null)
            return _initializedElement;

        T element = initializer();
        ReplaceSelfWith(element);
        _initializedElement = element;
        return element;
    }

    public static implicit operator T(MyVirtualizingElement<T> virtualized) =>
        virtualized.Init();

    Control IMyVirtualizingElement.InitControl() => Init();

    protected override Size MeasureOverride(Size availableSize)
    {
        Init();
        return default;
    }

    private void ReplaceSelfWith(T element)
    {
        if (Parent is null)
            return;

        if (Parent is not Panel parentPanel)
            throw new InvalidOperationException("MyVirtualizingElement 的父级必须是一个 Panel");

        int currentIndex = parentPanel.Children.IndexOf(this);
        if (currentIndex < 0)
            return;

        parentPanel.Children.RemoveAt(currentIndex);
        parentPanel.Children.Insert(currentIndex, element);
    }
}

public class MyVirtualizingElement(Func<Control> initializer) : Control, IMyVirtualizingElement
{
    private Control? _initializedElement;

    /// <summary>
    ///     实例化此控件，并在父级 Panel 中用真实控件替换占位控件。
    /// </summary>
    public Control Init()
    {
        if (_initializedElement is not null)
            return _initializedElement;

        Control element = initializer();
        ReplaceSelfWith(element);
        _initializedElement = element;
        return element;
    }

    /// <summary>
    ///     获取实例化后的控件。如果该控件尚未实例化，则会立即实例化；如果类型不匹配，则返回原值。
    /// </summary>
    public static Control TryInit(Control element)
    {
        return element is IMyVirtualizingElement virtualized ? virtualized.InitControl() : element;
    }

    Control IMyVirtualizingElement.InitControl() => Init();

    protected override Size MeasureOverride(Size availableSize)
    {
        Init();
        return default;
    }

    private void ReplaceSelfWith(Control element)
    {
        if (Parent is null)
            return;

        if (Parent is not Panel parentPanel)
            throw new InvalidOperationException("MyVirtualizingElement 的父级必须是一个 Panel");

        int currentIndex = parentPanel.Children.IndexOf(this);
        if (currentIndex < 0)
            return;

        parentPanel.Children.RemoveAt(currentIndex);
        parentPanel.Children.Insert(currentIndex, element);
    }
}

internal interface IMyVirtualizingElement
{
    Control InitControl();
}

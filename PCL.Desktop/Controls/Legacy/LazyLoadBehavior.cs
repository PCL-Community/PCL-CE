// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace PCL.Desktop.Controls.Legacy;

public static class LazyLoader
{
    public static void EnableLazyLoad(this Control element, Action action)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(action);

        _ = new LazyLoadBehavior(element, action);
    }
}

internal sealed class LazyLoadBehavior
{
    private readonly Control _element;
    private readonly Action _action;
    private bool _hasRun;

    public LazyLoadBehavior(Control element, Action action)
    {
        _element = element;
        _action = action;

        _element.AttachedToVisualTree += ElementAttachedToVisualTree;
        _element.DetachedFromVisualTree += ElementDetachedFromVisualTree;
        _element.LayoutUpdated += ElementLayoutUpdated;
        _element.EffectiveViewportChanged += ElementEffectiveViewportChanged;
        TryRun();
    }

    private void ElementAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => TryRun();

    private void ElementDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => Detach();

    private void ElementLayoutUpdated(object? sender, EventArgs e) => TryRun();

    private void ElementEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e) => TryRun();

    private void TryRun()
    {
        if (_hasRun || !_element.IsVisible || !_element.IsAttachedToVisualTree() || !IsInViewport())
            return;

        _hasRun = true;
        Detach();
        _action();
    }

    private bool IsInViewport()
    {
        ScrollViewer? scroll = _element.FindAncestorOfType<ScrollViewer>();
        if (scroll is null)
            return true;

        Point? topLeft = _element.TranslatePoint(new Point(0d, 0d), scroll);
        if (topLeft is null)
            return false;

        Size elementSize = _element.Bounds.Size;
        if (elementSize.Width <= 0d && elementSize.Height <= 0d)
            elementSize = _element.DesiredSize;

        Rect elementRect = new(topLeft.Value, elementSize);
        Rect viewport = new(0d, 0d, Math.Max(0d, scroll.Bounds.Width), Math.Max(0d, scroll.Bounds.Height));
        return viewport.Intersects(elementRect);
    }

    private void Detach()
    {
        _element.AttachedToVisualTree -= ElementAttachedToVisualTree;
        _element.DetachedFromVisualTree -= ElementDetachedFromVisualTree;
        _element.LayoutUpdated -= ElementLayoutUpdated;
        _element.EffectiveViewportChanged -= ElementEffectiveViewportChanged;
    }
}

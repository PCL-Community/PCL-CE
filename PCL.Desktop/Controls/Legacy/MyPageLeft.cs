// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

public class MyPageLeft : Grid
{
    public static readonly StyledProperty<Control?> AnimatedControlProperty =
        AvaloniaProperty.Register<MyPageLeft, Control?>(nameof(AnimatedControl));

    public Control? AnimatedControl
    {
        get => GetValue(AnimatedControlProperty);
        set => SetValue(AnimatedControlProperty, value);
    }

    public void TriggerShowAnimation()
    {
        if (AnimatedControl is null)
        {
            RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
            RenderTransform = new ScaleTransform(1d, 1d);
            Opacity = 1d;
            return;
        }

        foreach (Control control in GetAllAnimControls(AnimatedControl, ignoreInvisibility: true))
        {
            if (!control.IsVisible)
                continue;

            control.Opacity = control is TextBlock ? 0.6d : 1d;
            control.RenderTransform = null;
        }
    }

    public void TriggerHideAnimation()
    {
        if (AnimatedControl is null)
        {
            RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
            RenderTransform = new ScaleTransform(0.95d, 0.95d);
            Opacity = 0d;
            return;
        }

        foreach (Control control in GetAllAnimControls(AnimatedControl))
        {
            control.Opacity = 0d;
            control.RenderTransform = new TranslateTransform(-6d, 0d);
        }
    }

    private static IEnumerable<Control> GetAllAnimControls(Control element, bool ignoreInvisibility = false)
    {
        if (!ignoreInvisibility && !element.IsVisible)
            yield break;

        if (element is MyTextButton or MyListItem or TextBlock)
        {
            yield return element;
            yield break;
        }

        if (element is ContentControl { Content: Control content })
        {
            foreach (Control child in GetAllAnimControls(content, ignoreInvisibility))
                yield return child;
            yield break;
        }

        if (element is Panel panel)
        {
            foreach (Control child in panel.Children)
            {
                foreach (Control nested in GetAllAnimControls(child, ignoreInvisibility))
                    yield return nested;
            }

            yield break;
        }

        yield return element;
    }
}

public interface IRefreshable
{
    void Refresh();
}

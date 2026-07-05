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

    private readonly string _uuid = Guid.NewGuid().ToString("N");

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
            if (RenderTransform is not ScaleTransform)
                RenderTransform = new ScaleTransform(0.96d, 0.96d);

            Opacity = 0d;
            ModAnimation.AniStart(
                new List<ModAnimation.AniData>
                {
                    ModAnimation.AaScaleTransform(
                        this,
                        1d - GetScaleX(this),
                        ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaOpacity(this, 1d, 100)
                },
                $"PageLeft PageChange {_uuid}");
            return;
        }

        List<ModAnimation.AniData> animations = [];
        int id = 0;
        int delay = 0;
        foreach (Control control in GetAllAnimControls(AnimatedControl, ignoreInvisibility: true))
        {
            if (!control.IsVisible)
            {
                control.Opacity = 1d;
                control.RenderTransform = new TranslateTransform();
                if (control is MyListItem collapsedItem)
                    collapsedItem.isMouseOverAnimationEnabled = true;
                continue;
            }

            control.Opacity = 0d;
            control.RenderTransform = new TranslateTransform(-25d, 0d);
            if (control is MyListItem listItem)
                listItem.isMouseOverAnimationEnabled = false;
            animations.Add(ModAnimation.AaOpacity(
                control,
                control is TextBlock ? 0.6d : 1d,
                100,
                delay,
                new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
            animations.Add(ModAnimation.AaTranslateX(control, 5d, 200, delay, new ModAnimation.AniEaseOutFluent()));
            animations.Add(ModAnimation.AaTranslateX(
                control,
                20d,
                300,
                delay,
                new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)));
            if (control is MyListItem)
            {
                MyListItem animatedListItem = (MyListItem)control;
                animations.Add(ModAnimation.AaCode(
                    () =>
                    {
                        animatedListItem.isMouseOverAnimationEnabled = true;
                        animatedListItem.RefreshColor(this, EventArgs.Empty);
                    },
                    delay + 280));
            }
            delay += Math.Max(15 - id, 7) * 2;
            id++;
        }

        ModAnimation.AniStart(animations, $"PageLeft PageChange {_uuid}");
    }

    public void TriggerHideAnimation()
    {
        if (AnimatedControl is null)
        {
            RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
            if (RenderTransform is not ScaleTransform)
                RenderTransform = new ScaleTransform(1d, 1d);

            ModAnimation.AniStart(
                new List<ModAnimation.AniData>
                {
                    ModAnimation.AaScaleTransform(
                        this,
                        0.95d - GetScaleX(this),
                        110,
                        ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaOpacity(this, -Opacity, 80, 30)
                },
                $"PageLeft PageChange {_uuid}");
            return;
        }

        List<Control> controls = GetAllAnimControls(AnimatedControl).ToList();
        List<ModAnimation.AniData> animations = [];
        for (int i = 0; i < controls.Count; i++)
        {
            Control control = controls[i];
            int delay = controls.Count == 0 ? 0 : (int)Math.Round(70d / controls.Count * i);
            animations.Add(ModAnimation.AaOpacity(control, -control.Opacity, 50, delay));
            animations.Add(ModAnimation.AaTranslateX(control, -6d, 50, delay));
        }

        ModAnimation.AniStart(animations, $"PageLeft PageChange {_uuid}");
    }

    private static double GetScaleX(Control control) =>
        control.RenderTransform is ScaleTransform scale ? scale.ScaleX : 1d;

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

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Launching.Views;

public sealed partial class MyMsgLogin : Grid
{
    private readonly string _uuid = Guid.NewGuid().ToString("N");

    public MyMsgLogin()
    {
        AvaloniaXamlLoader.Load(this);
        OpenedLikeWpf();
    }

    public event EventHandler? ReopenWebpageRequested;

    public event EventHandler? CopyCodeRequested;

    public event EventHandler? CancelRequested;

    public event EventHandler? Closed;

    public event EventHandler<PointerPressedEventArgs>? DragRequested;

    public string Title
    {
        get => this.FindControl<TextBlock>("LabTitle")?.Text ?? string.Empty;
        set
        {
            if (this.FindControl<TextBlock>("LabTitle") is { } title)
                title.Text = value;
        }
    }

    public string Caption
    {
        get => this.FindControl<TextBlock>("LabCaption")?.Text ?? string.Empty;
        set
        {
            if (this.FindControl<TextBlock>("LabCaption") is { } caption)
                caption.Text = value;
        }
    }

    public string UserCode { get; set; } = string.Empty;

    public string Website { get; set; } = string.Empty;

    public void CloseLikeWpf()
    {
        TranslateTransform translate = GetTranslateTransform();
        RotateTransform rotate = GetRotateTransform();
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaOpacity(this, -Opacity, 80, 20),
                ModAnimation.AaDouble(value => translate.Y += value, 20d - translate.Y, 150, ease: new ModAnimation.AniEaseOutFluent()),
                ModAnimation.AaDouble(
                    value => rotate.Angle += value,
                    6d - rotate.Angle,
                    150,
                    ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaCode(() =>
                {
                    if (Parent is Panel panel)
                        panel.Children.Remove(this);
                    Closed?.Invoke(this, EventArgs.Empty);
                }, after: true)
            },
            "MyMsgBox " + _uuid);
    }

    private void OpenedLikeWpf()
    {
        Opacity = 0d;
        TranslateTransform translate = GetTranslateTransform();
        RotateTransform rotate = GetRotateTransform();
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaOpacity(this, 1d, 120, 60),
                ModAnimation.AaDouble(
                    value => translate.Y += value,
                    -translate.Y,
                    300,
                    60,
                    new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaDouble(
                    value => rotate.Angle += value,
                    -rotate.Angle,
                    300,
                    60,
                    new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
            },
            "MyMsgBox " + _uuid);
    }

    private void Btn1_Click(object? sender, EventArgs e) => ReopenWebpageRequested?.Invoke(this, EventArgs.Empty);

    private void Btn2_Click(object? sender, EventArgs e) => CopyCodeRequested?.Invoke(this, EventArgs.Empty);

    private void Btn3_Click(object? sender, EventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
        CloseLikeWpf();
    }

    private void Drag(object? sender, PointerPressedEventArgs e)
    {
        if (this.FindControl<Rectangle>("ShapeLine") is not { } line)
            return;

        if (e.GetPosition(line).Y <= 2d)
            DragRequested?.Invoke(this, e);
    }

    private TranslateTransform GetTranslateTransform()
    {
        TransformGroup group = EnsureTransformGroup();
        TranslateTransform? translate = group.Children.OfType<TranslateTransform>().FirstOrDefault();
        if (translate is not null)
            return translate;

        translate = new TranslateTransform(0d, 40d);
        group.Children.Add(translate);
        return translate;
    }

    private RotateTransform GetRotateTransform()
    {
        TransformGroup group = EnsureTransformGroup();
        RotateTransform? rotate = group.Children.OfType<RotateTransform>().FirstOrDefault();
        if (rotate is not null)
            return rotate;

        rotate = new RotateTransform(-4d);
        group.Children.Insert(0, rotate);
        return rotate;
    }

    private TransformGroup EnsureTransformGroup()
    {
        if (RenderTransform is TransformGroup group)
            return group;

        group = new TransformGroup();
        RenderTransform = group;
        return group;
    }
}

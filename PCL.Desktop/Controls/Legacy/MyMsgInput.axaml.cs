// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FluentValidation;

namespace PCL.Desktop.Controls.Legacy;

public sealed class MyMsgInputClosedEventArgs(string? result) : EventArgs
{
    public string? Result { get; } = result;

    public bool IsConfirmed => Result is not null;
}

public partial class MyMsgInput : Grid
{
    private readonly string _uuid = Guid.NewGuid().ToString("N");
    private TranslateTransform? _transformPos;
    private RotateTransform? _transformRotate;
    private string? _pendingResult;
    private AnimationMode _animationMode;

    public MyMsgInput()
    {
        AvaloniaXamlLoader.Load(this);
        CaptureTransforms();
        Opacity = 0d;
        AttachedToVisualTree += (_, _) =>
        {
            if (this.FindControl<MyTextBox>("TextArea") is { } input)
            {
                input.Focus(NavigationMethod.Pointer);
                input.CaretIndex = input.Text?.Length ?? 0;
                input.Validate();
            }
        };
    }

    public event EventHandler<MyMsgInputClosedEventArgs>? Closed;

    public string? Result { get; private set; }

    public bool IsClosing => _animationMode == AnimationMode.Closing;

    private MyTextBox? InputBox => this.FindControl<MyTextBox>("TextArea");

    public void Configure(
        string title,
        string text,
        string content = "",
        string hintText = "",
        string primaryButton = "确定",
        string secondaryButton = "取消",
        bool isWarn = false,
        Collection<IValidator<string>>? validateRules = null)
    {
        if (this.FindControl<TextBlock>("LabTitle") is { } titleBlock)
        {
            titleBlock.Text = title;
            IBrush titleBrush = isWarn
                ? FindBrush("ColorBrushRedLight", "#ff4c4c")
                : FindBrush("ColorBrush2", "#3a3a3a");
            titleBlock.Foreground = titleBrush;
            SyncTitleLine(titleBrush);
        }
        if (this.FindControl<TextBlock>("LabText") is { } caption)
            caption.Text = text;
        if (this.FindControl<MyScrollViewer>("PanText") is { } textPanel)
            textPanel.IsVisible = !string.IsNullOrEmpty(text);
        if (this.FindControl<MyTextBox>("TextArea") is { } input)
        {
            input.Text = content;
            input.HintText = hintText;
            input.ValidateRules = validateRules ?? [];
            input.Validate();
        }

        ConfigurePrimaryButton(primaryButton, isWarn);
        ConfigureSecondaryButton(secondaryButton);
        TextCaptionValidateChanged(this, EventArgs.Empty);
    }

    public void BeginShowAnimation()
    {
        CaptureTransforms();
        _pendingResult = null;
        _animationMode = AnimationMode.Opening;
        Opacity = 0d;
        SetTransform(40d, -4d);
        ModAnimation.AniStart(
        new List<ModAnimation.AniData>
        {
            ModAnimation.AaOpacity(this, 1d, 120, 60),
            ModAnimation.AaDouble(AddTransformY, -GetTransformY(), 300, 60, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaDouble(AddTransformAngle, -GetTransformAngle(), 300, 60, new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() => _animationMode = AnimationMode.None, after: true)
        }, $"MyMsgBox {_uuid}");
    }

    public void BeginCloseAnimation(Action? completed = null)
    {
        if (_animationMode == AnimationMode.Closing)
            return;

        CaptureTransforms();
        _animationMode = AnimationMode.Closing;
        ModAnimation.AniStart(
        new List<ModAnimation.AniData>
        {
            ModAnimation.AaOpacity(this, -Opacity, 80, 20),
            ModAnimation.AaDouble(AddTransformY, 20d - GetTransformY(), 150, 0, new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaDouble(AddTransformAngle, 6d - GetTransformAngle(), 150, 0, new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() =>
            {
                _animationMode = AnimationMode.None;
                completed?.Invoke();
            }, after: true)
        }, $"MyMsgBox {_uuid}");
    }

    public void Btn1Click(object? sender, EventArgs e)
    {
        if (InputBox is not { } input)
            return;

        input.Validate();
        if (!input.IsValidated)
            return;

        CloseWithResult(input.Text ?? string.Empty);
    }

    public void Btn2Click(object? sender, EventArgs e) =>
        CloseWithResult(null);

    private void ConfigurePrimaryButton(string text, bool isWarn)
    {
        if (this.FindControl<MyButton>("Btn1") is not { } primary)
            return;

        primary.Text = text;
        primary.ColorType = isWarn ? MyButton.ColorState.Red : MyButton.ColorState.Normal;
    }

    private void ConfigureSecondaryButton(string text)
    {
        if (this.FindControl<MyButton>("Btn2") is not { } secondary)
            return;

        secondary.Text = text;
        secondary.IsVisible = !string.IsNullOrEmpty(text);
        if (secondary.IsVisible &&
            this.FindControl<MyButton>("Btn1") is { } primary &&
            primary.ColorType != MyButton.ColorState.Red)
        {
            primary.ColorType = MyButton.ColorState.Highlight;
        }
    }

    private void TextCaptionValidateChanged(object? sender, EventArgs e)
    {
        if (this.FindControl<MyButton>("Btn1") is { } primary && InputBox is { } input)
            primary.IsEnabled = input.IsValidated;
    }

    private void CloseWithResult(string? result)
    {
        if (_animationMode == AnimationMode.Closing)
            return;

        Result = result;
        _pendingResult = result;
        BeginCloseAnimation(() => Closed?.Invoke(this, new MyMsgInputClosedEventArgs(_pendingResult)));
    }

    private void CaptureTransforms()
    {
        if (RenderTransform is not TransformGroup group)
            return;

        foreach (ITransform transform in group.Children)
        {
            _transformRotate ??= transform as RotateTransform;
            _transformPos ??= transform as TranslateTransform;
        }
    }

    private void SetTransform(double y, double angle)
    {
        if (_transformPos is not null)
            _transformPos.Y = y;
        if (_transformRotate is not null)
            _transformRotate.Angle = angle;
    }

    private double GetTransformY() =>
        _transformPos?.Y ?? 0d;

    private double GetTransformAngle() =>
        _transformRotate?.Angle ?? 0d;

    private void AddTransformY(double value)
    {
        if (_transformPos is not null)
            _transformPos.Y += value;
    }

    private void AddTransformAngle(double value)
    {
        if (_transformRotate is not null)
            _transformRotate.Angle += value;
    }

    private IBrush FindBrush(string resourceKey, string fallback)
    {
        return LegacyResourceResolver.Brush(this, resourceKey, fallback);
    }

    private void SyncTitleLine(IBrush brush)
    {
        if (this.FindControl<Rectangle>("ShapeLine") is { } line)
            line.Fill = brush;
    }

    private enum AnimationMode
    {
        None,
        Opening,
        Closing
    }
}

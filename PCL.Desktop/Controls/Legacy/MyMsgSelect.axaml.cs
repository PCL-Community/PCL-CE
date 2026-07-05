// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

public sealed class MyMsgSelectClosedEventArgs(int? selectedIndex) : EventArgs
{
    public int? SelectedIndex { get; } = selectedIndex;
}

public partial class MyMsgSelect : Grid
{
    private readonly List<MyListItem> _items = [];
    private readonly int _uuid = Random.Shared.Next();
    private int _selectedIndex = -1;
    private TranslateTransform? _transformPos;
    private RotateTransform? _transformRotate;
    private int? _pendingSelectedIndex;
    private AnimationMode _animationMode;

    public MyMsgSelect()
    {
        AvaloniaXamlLoader.Load(this);
        CaptureTransforms();
        Opacity = 0d;
        if (this.FindControl<MyButton>("Btn1") is { } confirm)
            confirm.IsEnabled = false;
    }

    public event EventHandler<MyMsgSelectClosedEventArgs>? Closed;

    public IReadOnlyList<MyListItem> Items => _items;

    public int SelectedIndex => _selectedIndex;

    public bool IsClosing => _animationMode == AnimationMode.Closing;

    public void Configure(
        string title,
        IEnumerable<MyListItem> items,
        string primaryButton = "继续",
        string secondaryButton = "取消")
    {
        if (this.FindControl<TextBlock>("LabTitle") is { } titleBlock)
            titleBlock.Text = title;
        if (this.FindControl<MyButton>("Btn1") is { } confirm)
        {
            confirm.Text = primaryButton;
            confirm.IsEnabled = false;
        }
        if (this.FindControl<MyButton>("Btn2") is { } cancel)
        {
            cancel.Text = secondaryButton;
            cancel.IsVisible = !string.IsNullOrWhiteSpace(secondaryButton);
        }

        _selectedIndex = -1;
        _items.Clear();
        if (this.FindControl<StackPanel>("PanSelection") is not { } panel)
            return;

        panel.Children.Clear();
        foreach (MyListItem item in items)
        {
            item.Type = MyListItem.CheckType.RadioBox;
            item.MinHeight = 24d;
            item.Click += SelectionClick;
            _items.Add(item);
            panel.Children.Add(item);
        }
    }

    private void SelectionClick(object? sender, EventArgs e)
    {
        if (sender is not MyListItem item)
            return;

        _selectedIndex = _items.IndexOf(item);
        if (this.FindControl<MyButton>("Btn1") is { } confirm)
            confirm.IsEnabled = _selectedIndex >= 0;
        if (_selectedIndex >= 0)
            CloseWithResult(_selectedIndex);
    }

    private void Btn1Click(object? sender, EventArgs e)
    {
        if (_selectedIndex < 0)
            return;

        CloseWithResult(_selectedIndex);
    }

    private void Btn2Click(object? sender, EventArgs e) =>
        CloseWithResult(null);

    public void BeginShowAnimation()
    {
        CaptureTransforms();
        _pendingSelectedIndex = null;
        _animationMode = AnimationMode.Opening;
        Opacity = 0d;
        SetTransform(y: 40d, angle: -4d);
        string name = $"MyMsgBox {_uuid}";
        ModAnimation.AniStart(
        new List<ModAnimation.AniData>
        {
            ModAnimation.AaOpacity(this, 1d, 120, 60),
            ModAnimation.AaDouble(AddTransformY, -GetTransformY(), 300, 60, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaDouble(AddTransformAngle, -GetTransformAngle(), 300, 60, new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() => _animationMode = AnimationMode.None, after: true)
        }, name);
    }

    public void BeginCloseAnimation(Action? completed = null)
    {
        if (_animationMode == AnimationMode.Closing)
            return;

        CaptureTransforms();
        _animationMode = AnimationMode.Closing;
        string name = $"MyMsgBox {_uuid}";
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
        }, name);
    }

    private void CloseWithResult(int? selectedIndex)
    {
        if (_animationMode == AnimationMode.Closing)
            return;

        _pendingSelectedIndex = selectedIndex;
        BeginCloseAnimation(() => Closed?.Invoke(this, new MyMsgSelectClosedEventArgs(_pendingSelectedIndex)));
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

    private enum AnimationMode
    {
        None,
        Opening,
        Closing
    }
}

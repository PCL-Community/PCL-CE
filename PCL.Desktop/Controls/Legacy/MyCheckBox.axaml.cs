// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace PCL.Desktop.Controls.Legacy;

public partial class MyCheckBox : Grid
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyCheckBox, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<bool?> CheckedProperty =
        AvaloniaProperty.Register<MyCheckBox, bool?>(nameof(Checked), false);

    public static readonly StyledProperty<bool> IsThreeStateProperty =
        AvaloniaProperty.Register<MyCheckBox, bool>(nameof(IsThreeState));

    private readonly TextBlock? _label;
    private readonly Control? _check;
    private readonly Control? _indeterminate;

    public MyCheckBox()
    {
        AvaloniaXamlLoader.Load(this);
        _label = this.FindControl<TextBlock>("LabText");
        _check = this.FindControl<Control>("ShapeCheck");
        _indeterminate = this.FindControl<Control>("ShapeIndeterminate");
        PointerReleased += OnPointerReleased;
        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_label is not null)
                _label.Text = text;
        });
        this.GetObservable(CheckedProperty).Subscribe(_ => RefreshVisual());
        RefreshVisual();
    }

    public event EventHandler? Change;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool? Checked
    {
        get => GetValue(CheckedProperty);
        set => SetValue(CheckedProperty, value);
    }

    public bool IsThreeState
    {
        get => GetValue(IsThreeStateProperty);
        set => SetValue(IsThreeStateProperty, value);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        Checked = IsThreeState
            ? Checked switch { true => false, false => null, _ => true }
            : Checked != true;
        Change?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void RefreshVisual()
    {
        if (_check is not null)
            _check.Opacity = Checked == true ? 1 : 0;
        if (_indeterminate is not null)
            _indeterminate.Opacity = Checked is null ? 1 : 0;
    }
}

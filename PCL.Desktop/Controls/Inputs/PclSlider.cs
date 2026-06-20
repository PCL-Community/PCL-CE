// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;

namespace PCL.Desktop.Controls.Inputs;

public class PclSlider : Slider
{
    public static readonly DirectProperty<PclSlider, string> HintTextProperty =
        AvaloniaProperty.RegisterDirect<PclSlider, string>(
            nameof(HintText),
            static slider => slider.HintText);

    private string _hintText = string.Empty;
    private Func<double, string>? _hintFormatter;

    public Func<double, string>? HintFormatter
    {
        get => _hintFormatter;
        set
        {
            _hintFormatter = value;
            UpdateHintText();
        }
    }

    public string HintText
    {
        get => _hintText;
        private set => SetAndRaise(
            HintTextProperty,
            ref _hintText,
            value);
    }

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
            UpdateHintText();
    }

    private void UpdateHintText() =>
        HintText = HintFormatter?.Invoke(Value) ??
                   Value.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
}

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using FluentValidation;

namespace PCL.Desktop.Controls.Legacy;

public class MyTextBox : TextBox
{
#pragma warning disable CA1711
    public delegate void ValidateChangedEventHandler(object sender, EventArgs e);
#pragma warning restore CA1711

    public static readonly StyledProperty<bool> HasBackgroundProperty =
        AvaloniaProperty.Register<MyTextBox, bool>(nameof(HasBackground), true);

    public static readonly StyledProperty<string> HintTextProperty =
        AvaloniaProperty.Register<MyTextBox, string>(nameof(HintText), string.Empty);

    public static readonly StyledProperty<string> ValidateResultProperty =
        AvaloniaProperty.Register<MyTextBox, string>(nameof(ValidateResult), string.Empty);

    public static readonly StyledProperty<bool> ShowValidateResultProperty =
        AvaloniaProperty.Register<MyTextBox, bool>(nameof(ShowValidateResult), true);

    private readonly List<EventHandler<TextChangedEventArgs>> _validatedTextChangedHandlers = [];
    private bool _isAttached;
    private bool _isTextChanged;

    protected override Type StyleKeyOverride => typeof(TextBox);

    public MyTextBox()
    {
        BorderThickness = new Thickness(1d);
        CornerRadius = new CornerRadius(3d);
        MinHeight = 28d;
        Padding = new Thickness(6d, 0d, 6d, 0d);
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        MaxLength = 1000;
        Cursor = new Cursor(StandardCursorType.Ibeam);

        PointerPressed += OnPointerPressed;
        PointerEntered += (_, _) => RefreshVisual();
        PointerExited += (_, _) => RefreshVisual();
        GotFocus += (_, _) => RefreshVisual();
        LostFocus += (_, _) => RefreshVisual();
        TextChanged += MyTextBoxTextChanged;
        AttachedToVisualTree += (_, _) =>
        {
            _isAttached = true;
            Validate();
        };
        DetachedFromVisualTree += (_, _) => _isAttached = false;
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RefreshVisual());
        this.GetObservable(HasBackgroundProperty).Subscribe(_ => RefreshVisual());
        this.GetObservable(ShowValidateResultProperty).Subscribe(_ => RefreshVisual());
        this.GetObservable(HintTextProperty).Subscribe(hint => PlaceholderText = hint);
        this.GetObservable(ValidateResultProperty).Subscribe(_ =>
        {
            RefreshVisual();
            ValidateChanged?.Invoke(this, EventArgs.Empty);
        });
        RefreshVisual();
    }

    public event ValidateChangedEventHandler? ValidateChanged;

    public event EventHandler<TextChangedEventArgs> ValidatedTextChanged
    {
        add => _validatedTextChangedHandlers.Add(value);
        remove => _validatedTextChangedHandlers.Remove(value);
    }

    public bool HasBackground
    {
        get => GetValue(HasBackgroundProperty);
        set => SetValue(HasBackgroundProperty, value);
    }

    public bool ShowValidateResult
    {
        get => GetValue(ShowValidateResultProperty);
        set => SetValue(ShowValidateResultProperty, value);
    }

    public string HintText
    {
        get => GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    public string ValidateResult
    {
        get => GetValue(ValidateResultProperty);
        set => SetValue(ValidateResultProperty, value);
    }

    public bool IsValidated => string.IsNullOrEmpty(ValidateResult);

    public Collection<IValidator<string>> ValidateRules
    {
        get;
        set
        {
            field = value;
            Validate();
        }
    } = [];

    public void Validate()
    {
        string newResult = string.Empty;
        string value = Text ?? string.Empty;
        foreach (IValidator<string> rule in ValidateRules)
        {
            FluentValidation.Results.ValidationResult result = rule.Validate(value);
            if (!result.IsValid)
            {
                newResult = result.Errors.FirstOrDefault()?.ErrorMessage ?? "输入内容不符合要求";
                break;
            }
        }

        ValidateResult = newResult;
    }

    public void ForceShowAsSuccess()
    {
        _isTextChanged = false;
        RefreshVisual();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Focus(NavigationMethod.Pointer, e.KeyModifiers);
    }

    private void MyTextBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        _isTextChanged = _isAttached;
        Validate();
        if (!IsValidated)
            return;

        foreach (EventHandler<TextChangedEventArgs> handler in _validatedTextChangedHandlers.ToArray())
            handler.Invoke(this, e);
    }

    private void RefreshVisual()
    {
        if (TemplatedParent is MyComboBox)
            return;

        bool showInvalid = IsEnabled && ShowValidateResult && !IsValidated && _isTextChanged;
        string foreColorName;
        string backColorName;
        int animationTime;
        if (IsEnabled)
        {
            if (showInvalid)
            {
                foreColorName = "ColorBrushRedLight";
                backColorName = "ColorBrushRedBack";
                animationTime = 200;
            }
            else if (IsKeyboardFocusWithin)
            {
                foreColorName = "ColorBrush3";
                backColorName = "ColorBrush7";
                animationTime = 10;
            }
            else if (IsPointerOver)
            {
                foreColorName = "ColorBrush4";
                backColorName = "ColorBrush7";
                animationTime = 100;
            }
            else
            {
                foreColorName = "ColorBrushBg0";
                backColorName = "ColorBrushHalfWhite";
                animationTime = 100;
            }

            Foreground = FindBrush("ColorBrush1", "#343d4a");
            SelectionBrush = FindBrush("ColorBrush3", "#1370f3");
            Cursor = new Cursor(StandardCursorType.Ibeam);
        }
        else
        {
            foreColorName = "ColorBrushGray5";
            backColorName = "ColorBrushGray6";
            animationTime = 200;
            Foreground = FindBrush("ColorBrushGray4", "#a6a6a6");
            Cursor = Cursor.Default;
        }

        if (!HasBackground)
            backColorName = "ColorBrushTransparent";

        if (ControlVisualHelpers.ShouldAnimate(this))
        {
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(this, BorderBrushProperty, foreColorName, animationTime),
                    ModAnimation.AaColor(this, BackgroundProperty, backColorName, animationTime)
                },
                $"MyTextBox Color {GetHashCode()}");
            return;
        }

        ModAnimation.AniStop($"MyTextBox Color {GetHashCode()}");
        BorderBrush = FindBrush(foreColorName, "#96c0f9");
        Background = HasBackground ? FindBrush(backColorName, "#55ffffff") : Brushes.Transparent;
    }

    private IBrush FindBrush(string key, string fallback)
    {
        return LegacyResourceResolver.Brush(this, key, fallback);
    }
}

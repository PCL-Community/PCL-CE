// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace PCL.Desktop.Controls.Legacy;

public class MyComboBox : ComboBox
{
    // Keep the WPF event shape for copied pages that bind TextChanged in XAML.
    #pragma warning disable CA1711
    public delegate void TextChangedEventHandler(object sender, TextChangedEventArgs? e);
    #pragma warning restore CA1711

    public static readonly StyledProperty<string> HintTextProperty =
        AvaloniaProperty.Register<MyComboBox, string>(nameof(HintText), string.Empty);

    private bool _isMouseDown;
    private bool _isTextChanging;
    private double _realWidth = double.NaN;
    private string _text = string.Empty;

    public MyComboBox()
    {
        _text = SelectedItem?.ToString() ?? string.Empty;
        PointerPressed += MyComboBox_PointerPressed;
        PointerReleased += MyComboBox_PointerReleased;
        PointerExited += MyComboBox_PointerReleased;
        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        GotFocus += (_, _) => RefreshColor();
        LostFocus += (_, _) => RefreshColor();
        DropDownOpened += MyComboBox_DropDownOpened;
        DropDownClosed += MyComboBox_DropDownClosed;
        SelectionChanged += MyComboBox_SelectionChanged;
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RefreshColor());
        this.GetObservable(IsDropDownOpenProperty).Subscribe(_ => RefreshColor());
        this.GetObservable(HintTextProperty).Subscribe(text => PlaceholderText = text);
        this.GetObservable(ComboBox.TextProperty).Subscribe(OnTextPropertyChanged);
        RefreshColor();
    }

    public event TextChangedEventHandler? TextChanged;

    public string HintText
    {
        get => GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    public new string Text
    {
        get => IsEditable
            ? base.Text ?? _text
            : SelectedItem?.ToString() ?? string.Empty;
        set
        {
            if (!IsEditable)
                throw new NotSupportedException("该 ComboBox 不支持修改文本。");

            _text = value;
            base.Text = value;
        }
    }

    public bool DropDownWidthSync { get; set; } = true;

    public ContentPresenter? ContentPresenter =>
        this.FindDescendantOfType<ContentPresenter>();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        if (item is MyComboBoxItem)
        {
            recycleKey = null;
            return false;
        }

        recycleKey = typeof(MyComboBoxItem);
        return true;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new MyComboBoxItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is MyComboBoxItem comboBoxItem && item is not MyComboBoxItem)
            comboBoxItem.Content = item;
    }

    public void RefreshColor()
    {
        IBrush foreground;
        IBrush background;
        if (IsEnabled)
        {
            if (_isMouseDown || IsDropDownOpen || IsFocused)
            {
                foreground = FindBrush("ColorBrush3", "#1370f3");
                background = FindBrush("ColorBrush7", "#e0eafd");
            }
            else if (IsPointerOver)
            {
                foreground = FindBrush("ColorBrush4", "#4890f5");
                background = FindBrush("ColorBrush7", "#e0eafd");
            }
            else
            {
                foreground = FindBrush("ColorBrushBg0", "#96c0f9");
                background = FindBrush("ColorBrushHalfWhite", "#55ffffff");
            }
        }
        else
        {
            foreground = FindBrush("ColorBrushGray5", "#cccccc");
            background = FindBrush("ColorBrushGray6", "#ebebeb");
        }

        Foreground = foreground;
        Background = background;
    }

    private void MyComboBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isMouseDown = true;
            RefreshColor();
        }
    }

    private void MyComboBox_PointerReleased(object? sender, PointerEventArgs e)
    {
        _isMouseDown = false;
        RefreshColor();
    }

    private void MyComboBox_DropDownOpened(object? sender, EventArgs e)
    {
        _realWidth = Width;
        if (DropDownWidthSync && !double.IsNaN(Bounds.Width) && Bounds.Width > 0d)
            Width = Bounds.Width;
    }

    private void MyComboBox_DropDownClosed(object? sender, EventArgs e)
    {
        Width = _realWidth;
    }

    private void MyComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!IsEditable || SelectedItem is null)
            return;

        _text = SelectedItem.ToString() ?? string.Empty;
    }

    private void OnTextPropertyChanged(string? text)
    {
        if (_isTextChanging || !IsEditable)
            return;

        _text = text ?? string.Empty;
        TextChanged?.Invoke(this, new TextChangedEventArgs(TextBox.TextChangedEvent, this));
        if (SelectedItem is null || Text == SelectedItem.ToString())
            return;

        string rawText = Text;
        _isTextChanging = true;
        SelectedItem = null;
        base.Text = rawText;
        _isTextChanging = false;
    }

    private IBrush FindBrush(string key, string fallback)
    {
        if (this.TryGetResource(key, null, out object? resource) && resource is IBrush brush)
            return brush;

        return new SolidColorBrush(Color.Parse(fallback));
    }
}

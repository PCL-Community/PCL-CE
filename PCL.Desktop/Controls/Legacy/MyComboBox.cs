// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using PathShape = Avalonia.Controls.Shapes.Path;

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
    private PathShape? _dropDownArrow;
    private ContentPresenter? _selectedContentPresenter;
    private TextBox? _editableTextBox;
    private Grid? _panPopup;
    private Border? _dropDownBorder;
    private bool _usesWpfEditableItemTemplate;

    private static readonly IDataTemplate WpfEditableItemTemplate = new FuncDataTemplate<object?>(
        static (item, _) =>
        {
            MyIconButton deleteButton = new()
            {
                Height = 20d,
                Width = 20d,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Tag = item,
                SvgIcon = "lucide/x"
            };

            return new MyListItem
            {
                Title = item?.ToString() ?? string.Empty,
                FontSize = 13d,
                MaxHeight = 26d,
                IsScaleAnimationEnabled = false,
                Margin = new Thickness(-3d, 0d, 0d, 0d),
                Buttons = [deleteButton]
            };
        });

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
        this.GetObservable(IsDropDownOpenProperty).Subscribe(_ =>
        {
            RefreshColor();
            RefreshDropDownArrow(animate: true);
        });
        this.GetObservable(IsEditableProperty).Subscribe(_ => RefreshEditableVisibility());
        this.GetObservable(HintTextProperty).Subscribe(text => PlaceholderText = text);
        this.GetObservable(ComboBox.TextProperty).Subscribe(OnTextPropertyChanged);
        AttachedToVisualTree += (_, _) => EnsureWpfMarkedSelection();
        RefreshColor();
    }

    public event TextChangedEventHandler? TextChanged;

    public int Uuid { get; } = Random.Shared.Next();

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

    public string SelectedValuePath { get; set; } = string.Empty;

    public ContentPresenter? ContentPresenter =>
        _selectedContentPresenter ?? this.FindDescendantOfType<ContentPresenter>();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _dropDownArrow = e.NameScope.Find<PathShape>("PART_DropDownArrow");
        _selectedContentPresenter = e.NameScope.Find<ContentPresenter>("PART_Content")
            ?? e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
        _editableTextBox = e.NameScope.Find<TextBox>("PART_EditableTextBox");
        _panPopup = e.NameScope.Find<Grid>("PanPopup");
        _dropDownBorder = e.NameScope.Find<Border>("dropDownBorder");
        if (_dropDownArrow is not null)
        {
            _dropDownArrow.RenderTransformOrigin = new RelativePoint(0.3d, 0.5d, RelativeUnit.Relative);
            if (_dropDownArrow.RenderTransform is not RotateTransform)
                _dropDownArrow.RenderTransform = new RotateTransform();
        }

        if (_editableTextBox is not null)
        {
            _editableTextBox.Tag = Tag;
            _editableTextBox.GetObservable(IsFocusedProperty).Subscribe(_ => RefreshColor());
            if (_editableTextBox is MyTextBox myTextBox)
                myTextBox.HintText = HintText;
        }

        RefreshEditableVisibility();
        RefreshDropDownArrow(animate: false);
    }

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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty ||
            change.Property == SelectedItemProperty ||
            change.Property == SelectedIndexProperty)
        {
            _text = SelectedItem?.ToString() ?? string.Empty;
        }
    }

    public void RefreshColor()
    {
        string foreColorName;
        string backColorName;
        int time;
        if (IsEnabled)
        {
            if (_isMouseDown || IsDropDownOpen || IsFocused || (IsEditable && _editableTextBox?.IsFocused == true))
            {
                foreColorName = "ColorBrush3";
                backColorName = "ColorBrush7";
                time = 10;
            }
            else if (IsPointerOver)
            {
                foreColorName = "ColorBrush4";
                backColorName = "ColorBrush7";
                time = 100;
            }
            else
            {
                foreColorName = "ColorBrushBg0";
                backColorName = "ColorBrushHalfWhite";
                time = 100;
            }
        }
        else
        {
            foreColorName = "ColorBrushGray5";
            backColorName = "ColorBrushGray6";
            time = 200;
        }

        if (ControlVisualHelpers.ShouldAnimate(this))
        {
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(this, ForegroundProperty, foreColorName, time),
                    ModAnimation.AaColor(this, BackgroundProperty, backColorName, time)
                },
                "MyComboBox Color " + Uuid);
            return;
        }

        ModAnimation.AniStop("MyComboBox Color " + Uuid);
        Foreground = FindBrush(foreColorName, "#96c0f9");
        Background = FindBrush(backColorName, "#55ffffff");
    }

    private void RefreshEditableVisibility()
    {
        if (_selectedContentPresenter is not null)
            _selectedContentPresenter.IsVisible = !IsEditable;
        if (_editableTextBox is not null)
            _editableTextBox.IsVisible = IsEditable;

        RefreshEditableItemTemplate();
    }

    private void RefreshEditableItemTemplate()
    {
        if (IsEditable)
        {
            IsTabStop = false;
            if (ItemTemplate is null)
            {
                ItemTemplate = WpfEditableItemTemplate;
                _usesWpfEditableItemTemplate = true;
            }
            else
            {
                _usesWpfEditableItemTemplate = ReferenceEquals(ItemTemplate, WpfEditableItemTemplate);
            }

            return;
        }

        if (_usesWpfEditableItemTemplate && ReferenceEquals(ItemTemplate, WpfEditableItemTemplate))
        {
            ItemTemplate = null;
            _usesWpfEditableItemTemplate = false;
        }
    }

    private void RefreshDropDownArrow(bool animate)
    {
        if (_dropDownArrow?.RenderTransform is not RotateTransform rotate)
            return;

        double targetAngle = IsDropDownOpen ? 180d : 0d;
        if (animate && ControlVisualHelpers.ShouldAnimate(this))
        {
            ModAnimation.AniStart(
                ModAnimation.AaRotateTransform(
                    _dropDownArrow,
                    targetAngle - rotate.Angle,
                    200,
                    ease: new ModAnimation.AniEaseOutFluent()),
                "MyComboBox Arrow " + Uuid);
            return;
        }

        ModAnimation.AniStop("MyComboBox Arrow " + Uuid);
        rotate.Angle = targetAngle;
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

        if (_panPopup is not null)
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            _panPopup.Opacity = topLevel?.Opacity ?? 1d;
        }

        if (!DropDownWidthSync && _dropDownBorder is not null && Bounds.Width > 0d)
            _dropDownBorder.MinWidth = Bounds.Width;
    }

    private void MyComboBox_DropDownClosed(object? sender, EventArgs e)
    {
        Width = _realWidth;
    }

    private void MyComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _text = SelectedItem?.ToString() ?? string.Empty;
    }

    private void EnsureWpfMarkedSelection()
    {
        if (SelectedIndex >= 0 || SelectedItem is not null)
            return;

        foreach (object? item in Items)
        {
            if (item is not MyComboBoxItem { IsSelected: true } comboBoxItem)
                continue;

            SelectedItem = comboBoxItem;
            _text = comboBoxItem.ToString();
            return;
        }
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
        int? rawCaretIndex = _editableTextBox?.CaretIndex;
        _isTextChanging = true;
        SelectedItem = null;
        base.Text = rawText;
        if (_editableTextBox is not null && rawCaretIndex is int caretIndex)
            _editableTextBox.CaretIndex = Math.Clamp(caretIndex, 0, _editableTextBox.Text?.Length ?? 0);
        _isTextChanging = false;
    }

    private IBrush FindBrush(string key, string fallback)
    {
        return LegacyResourceResolver.Brush(this, key, fallback);
    }
}

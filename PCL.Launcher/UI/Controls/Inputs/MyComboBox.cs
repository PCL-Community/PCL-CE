using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentValidation;
using Path = Avalonia.Controls.Shapes.Path;

namespace PCL.UI.Controls;

/// <summary>
///     下拉框：在 <see cref="ComboBox" /> 模板上补充主题配色、可编辑文本校验与下拉宽度控制。
///     保留 <see cref="HintText" />（映射 <see cref="ComboBox.PlaceholderText" />）、
///     <see cref="DropDownWidthSync" />、<see cref="TextChanged" /> 事件与
///     “可编辑文本与选项不一致时取消选中”的修正；可编辑文本由 <see cref="MyTextBox" /> 承载，
///     通过 <see cref="ValidateRules" /> 参与校验并显示错误文本。
///     前景/背景色经 新动画引擎 过渡；动画关闭时直接落到终值。
/// </summary>
public class MyComboBox : ComboBox
{
    public delegate void TextChangedEventHandler(object sender, TextChangedEventArgs e);

    public static readonly StyledProperty<string> HintTextProperty =
        AvaloniaProperty.Register<MyComboBox, string>(nameof(HintText), string.Empty);

    // 基础

    public int Uuid = InputTheme.NewUuid();

    private ContentPresenter? _contentPresenter;
    private Path? _dropDownGlyph;
    private TextBox? _editableTextBox;

    // 键盘导航标记：下拉列表展开后由方向键等按键置位，指针进入列表或按下鼠标时清除。

    // 鼠标按下接口
    private bool _isMouseDown;

    // 修复：下拉框文本修改后，依然误认为还选择着此前的选项
    private bool _isTextChanging;

    // 最近一次输入来自键盘还是指针，用于决定下拉列表展开时的高亮来源。
    private bool _lastInputFromKeyboard;

    private Popup? _popup;
    private Control? _popupContent;
    private TopLevel? _popupOpacitySource;
    private double _realWidth = double.NaN;
    private Collection<IValidator<string>> _validateRules = [];

    public MyComboBox()
    {
        FontSize = 13d;
        MaxDropDownHeight = 160d;
        BorderThickness = new Thickness(1d);

        DropDownOpened += MyComboBox_DropDownOpened;
        DropDownClosed += MyComboBox_DropDownClosed;
        SelectionChanged += MyComboBox_SelectionChanged;
        PointerPressed += (_, _) =>
        {
            NotifyPointerActivity();
            _isMouseDown = true;
            RefreshColor();
        };
        PointerReleased += (_, _) =>
        {
            _isMouseDown = false;
            RefreshColor();
        };
        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) =>
        {
            _isMouseDown = false;
            RefreshColor();
        };
        GotFocus += (_, _) => RefreshColor();
        LostFocus += (_, _) => RefreshColor();
        Unloaded += (_, _) => StopAnimations();
    }

    private string ForegroundKey => InputAnimation.Key("MyComboBox Foreground", Uuid);
    private string BackgroundKey => InputAnimation.Key("MyComboBox Background", Uuid);

    private string EditableForegroundKey =>
        InputAnimation.Key("MyComboBox EditableForeground", Uuid);

    private string GlyphKey => InputAnimation.Key("MyComboBox Glyph", Uuid);

    // 自定义属性

    /// <summary>
    ///     输入提示文本；可编辑模式下同步到内部文本框的占位提示。
    /// </summary>
    public string HintText
    {
        get => GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    /// <summary>
    ///     下拉列表展开时是否把控件宽度同步到 Popup；关闭时 Popup 至少与控件同宽。
    /// </summary>
    public bool DropDownWidthSync { get; set; } = true;

    /// <summary>
    ///     可编辑文本的校验规则；模板就绪后转发给内部 MyTextBox，由它执行校验并显示错误文本。
    /// </summary>
    public Collection<IValidator<string>> ValidateRules
    {
        get => _validateRules;
        set
        {
            _validateRules = value ?? [];
            if (_editableTextBox is MyTextBox editable)
                editable.ValidateRules = _validateRules;
        }
    }

    /// <summary>
    ///     模板中的可编辑文本框；模板未就绪或不是 MyTextBox 时为 <c>null</c>。
    /// </summary>
    public MyTextBox? EditableTextBox => _editableTextBox as MyTextBox;

    /// <summary>
    ///     可编辑文本是否通过校验；不可编辑或模板未就绪时视为通过。
    /// </summary>
    public bool IsValidated =>
        !IsEditable || _editableTextBox is not MyTextBox editable || editable.IsValidated;

    /// <summary>
    ///     可编辑文本的校验结果；没有错误或不可编辑时为空字符串。
    /// </summary>
    public string ValidateResult =>
        IsEditable && _editableTextBox is MyTextBox editable
            ? editable.ValidateResult
            : string.Empty;

    /// <summary>
    ///     当前是否由键盘导航决定列表项高亮。
    ///     Avalonia 的 <see cref="ComboBoxItem" /> 没有 WPF 的 IsHighlighted，
    ///     这里用焦点（键盘）与指针状态近似：键盘模式下高亮跟随焦点项，指针模式下跟随悬停项。
    /// </summary>
    internal bool IsKeyboardNavigation { get; private set; }

    /// <summary>
    ///     模板中的内容宿主；模板未就绪时为 <c>null</c>。
    /// </summary>
    public ContentPresenter? ContentPresenter =>
        _contentPresenter ??=
            this.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault();

    private void StopAnimations()
    {
        InputAnimation.Stop(ForegroundKey, BackgroundKey, EditableForegroundKey, GlyphKey);
    }

    private void RefreshGlyphRotation(bool animate)
    {
        if (_dropDownGlyph?.RenderTransform is not RotateTransform rotate)
            return;

        var target = IsDropDownOpen ? 180d : 0d;
        if (!animate || !InputAnimation.CanAnimate(this))
        {
            AnimationService.CancelAnimationByName(GlyphKey);
            rotate.Angle = target;
            return;
        }

        AnimationService.Run(
            new RotateTransformAnimation(
                _dropDownGlyph,
                target - rotate.Angle,
                200,
                ease: new FluentEaseOut(
                    EasePower.Strong)),
            GlyphKey);
    }

    /// <summary>
    ///     重新执行可编辑文本的校验；不可编辑时不执行。
    /// </summary>
    public void Validate()
    {
        if (IsEditable)
            (_editableTextBox as MyTextBox)?.Validate();
    }

    /// <summary>
    ///     下拉框文本改变（可编辑模式下由内部文本框转发）。
    /// </summary>
    public event TextChangedEventHandler? TextChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_editableTextBox is not null)
        {
            _editableTextBox.TextChanged -= EditableTextBox_TextChanged;
            _editableTextBox = null;
        }

        // 用非泛型查找再转换：泛型 Find<T> 在模板部件类型不匹配时会抛异常（Avalonia 12 行为），
        // 这里改为安全回退，避免模板与代码类型不一致时直接让整个界面线程崩溃。
        _contentPresenter = e.NameScope.Find("PART_ContentPresenter") as ContentPresenter
            ?? this.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault();
        _popup = e.NameScope.Find<Popup>("PART_Popup");
        _dropDownGlyph = e.NameScope.Find<Path>("DropDownGlyph");
        RefreshGlyphRotation(false);

        try
        {
            _editableTextBox = e.NameScope.Find<TextBox>("PART_EditableTextBox");
            if (_editableTextBox is null)
                return;

            if (_editableTextBox is MyTextBox editable)
            {
                // 可编辑文本框由 MyTextBox 承担：外壳配色交给组合框，校验规则与错误层沿用 MyTextBox。
                editable.HasBackground = false;
                editable.BorderThickness = new Thickness(0d);
                editable.HintText = HintText;
                editable.ValidateRules = ValidateRules;
            }

            _editableTextBox.TextChanged += EditableTextBox_TextChanged;
            _editableTextBox.Tag = Tag;
            _editableTextBox.PlaceholderText = HintText;
            _editableTextBox.CaretBrush = InputTheme.TextSecondary(this);

            if (!string.IsNullOrEmpty(Text))
                _editableTextBox.Text = Text;

            RefreshColor(false);
        }
        catch (Exception ex)
        {
            InputTheme.LogDebug(ex, $"初始化可编辑文本框失败（{Name ?? ""}）");
        }
    }

    // 状态配色

    /// <summary>
    ///     重新计算当前状态的前景/背景色。
    /// </summary>
    public void RefreshColor(bool animate = true)
    {
        string foregroundKey;
        IBrush foregroundFallback;
        string backgroundKey;
        IBrush backgroundFallback;
        int time;

        if (IsEnabled)
        {
            if (_isMouseDown
                || IsDropDownOpen
                || (IsEditable && _editableTextBox?.IsFocused == true))
            {
                foregroundKey = InputTheme.AccentKey;
                foregroundFallback = InputTheme.Accent(this);
                backgroundKey = InputTheme.AccentLightKey;
                backgroundFallback = InputTheme.AccentLight(this);
                time = 10;
            }
            else if (IsPointerOver)
            {
                foregroundKey = InputTheme.AccentHoverKey;
                foregroundFallback = InputTheme.AccentHover(this);
                backgroundKey = InputTheme.AccentLightKey;
                backgroundFallback = InputTheme.AccentLight(this);
                time = 100;
            }
            else
            {
                foregroundKey = InputTheme.IdleBorderKey;
                foregroundFallback = InputTheme.IdleBorder(this);
                backgroundKey = InputTheme.IdleBackgroundKey;
                backgroundFallback = InputTheme.IdleBackground(this);
                time = 100;
            }
        }
        else
        {
            foregroundKey = InputTheme.DisabledBorderKey;
            foregroundFallback = InputTheme.TextSecondary(this);
            backgroundKey = InputTheme.DisabledBackgroundKey;
            backgroundFallback = InputTheme.DisabledBackground(this);
            time = 200;
        }

        InputAnimation.Color(
            this,
            ForegroundProperty,
            foregroundKey,
            foregroundFallback,
            time,
            ForegroundKey,
            animate);
        InputAnimation.Color(
            this,
            BackgroundProperty,
            backgroundKey,
            backgroundFallback,
            time,
            BackgroundKey,
            animate);

        // MyTextBox 自行维护前景色，避免两套颜色过渡互相覆盖
        if (_editableTextBox is not null && _editableTextBox is not MyTextBox)
            InputAnimation.Color(
                _editableTextBox,
                ForegroundProperty,
                foregroundKey,
                foregroundFallback,
                time,
                EditableForegroundKey,
                animate);
    }

    private void MyComboBox_DropDownOpened(object? sender, EventArgs e)
    {
        _realWidth = Width;
        if (DropDownWidthSync && !double.IsNaN(Bounds.Width) && Bounds.Width > 0d)
            Width = Bounds.Width;

        // 不同步宽度时，Popup 至少与控件同宽。
        if (_popup is not null && !double.IsNaN(Bounds.Width))
            _popup.MinWidth = DropDownWidthSync ? 0d : Bounds.Width;

        // 由键盘打开时直接采用键盘高亮，由指针打开时采用悬停高亮。
        SetKeyboardNavigation(_lastInputFromKeyboard);

        AttachPopupOpacity();
        RefreshColor();
    }

    private void MyComboBox_DropDownClosed(object? sender, EventArgs e)
    {
        Width = _realWidth;
        SetKeyboardNavigation(false);
        DetachPopupOpacity();
        RefreshColor();
    }

    private void MyComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsEditable && SelectedItem is not null)
            FillEditableText(GetItemDisplayText(SelectedItem));

        if (IsLoaded && !AnimationService.IsSuspended)
            CustomEventService.Raise(this);
    }

    private void FillEditableText(string text)
    {
        if (_editableTextBox is not null && _editableTextBox.Text != text)
        {
            _isTextChanging = true;
            try
            {
                _editableTextBox.Text = text;
            }
            finally
            {
                _isTextChanging = false;
            }
        }

        if (!string.Equals(Text, text, StringComparison.Ordinal))
            SetCurrentValue(TextProperty, text);
    }

    private static string GetItemDisplayText(object? item)
    {
        if (item is null)
            return string.Empty;
        if (item is ContentControl content)
            return content.Content?.ToString() ?? string.Empty;
        return item.ToString() ?? string.Empty;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // 记录输入来源，并在下拉列表展开时把高亮来源切到键盘；指针进入列表后切回指针。
        _lastInputFromKeyboard = true;

        if (IsDropDownOpen)
            switch (e.Key)
            {
                case Key.Up:
                case Key.Down:
                case Key.Home:
                case Key.End:
                case Key.PageUp:
                case Key.PageDown:
                    SetKeyboardNavigation(true);
                    break;
            }

        base.OnKeyDown(e);
    }

    /// <summary>
    ///     指针进入控件或列表项时调用：高亮来源切回指针，并刷新已生成的列表项。
    /// </summary>
    internal void NotifyPointerActivity()
    {
        _lastInputFromKeyboard = false;
        SetKeyboardNavigation(false);
    }

    private void SetKeyboardNavigation(bool value)
    {
        if (IsKeyboardNavigation == value)
            return;

        IsKeyboardNavigation = value;
        RefreshItemHighlights();
    }

    private void RefreshItemHighlights()
    {
        foreach (var item in GetRealizedContainers().OfType<MyComboBoxItem>())
            item.RefreshColor();
    }

    // Popup 透明度

    /// <summary>
    ///     把所属窗口的透明度同步到 Popup 承载内容。
    ///     Popup 在桌面平台上是独立顶层窗口，无法直接设置透明度；这里订阅所属 TopLevel 的
    ///     <see cref="Visual.Opacity" /> 并写入内容根元素。浮层模式（<see cref="Popup.IsUsingOverlayLayer" />）
    ///     下内容已位于主窗口的浮层中，随窗口一起透明，不重复设置。
    ///     Popup 宿主会绑定主题变体，主题切换时内容随主窗口一起更新。
    /// </summary>
    private void AttachPopupOpacity()
    {
        DetachPopupOpacity();

        if (_popup is null || _popup.IsUsingOverlayLayer || _popup.Child is not Control content)
            return;

        var owner = TopLevel.GetTopLevel(this);
        if (owner is null)
            return;

        _popupOpacitySource = owner;
        _popupContent = content;
        owner.PropertyChanged += OwnerTopLevel_PropertyChanged;
        content.Opacity = owner.Opacity;
    }

    private void DetachPopupOpacity()
    {
        if (_popupOpacitySource is not null)
            _popupOpacitySource.PropertyChanged -= OwnerTopLevel_PropertyChanged;

        _popupOpacitySource = null;
        _popupContent = null;
    }

    private void OwnerTopLevel_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == OpacityProperty && _popupContent is not null && sender is Visual source)
            _popupContent.Opacity = source.Opacity;
    }

    private void EditableTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        // 非可编辑模式下模板仍会把 Text 同步到隐藏文本框，这里不向外转发变化
        if (!IsEditable || _editableTextBox is null)
            return;

        if (_isTextChanging)
        {
            TextChanged?.Invoke(sender ?? this, e);
            return;
        }

        var rawText = _editableTextBox.Text ?? string.Empty;
        var selectedText = GetItemDisplayText(SelectedItem);
        if (SelectedItem is not null && rawText != selectedText)
        {
            var rawSelectionStart = _editableTextBox.SelectionStart;
            _isTextChanging = true;
            try
            {
                SelectedItem = null;
                _editableTextBox.Text = rawText;
                _editableTextBox.SelectionStart = rawSelectionStart;
                _editableTextBox.SelectionEnd = rawSelectionStart;
            }
            finally
            {
                _isTextChanging = false;
            }
        }

        TextChanged?.Invoke(sender ?? this, e);
    }

    // 容器

    protected override Control CreateContainerForItemOverride(
        object? item,
        int index,
        object? recycleKey)
    {
        return new MyComboBoxItem();
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        return NeedsContainer<ComboBoxItem>(item, out recycleKey);
    }

    // 属性变更处理

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty && IsEditable && !_isTextChanging)
        {
            FillEditableText(change.NewValue as string ?? string.Empty);
        }
        else if (change.Property == HintTextProperty)
        {
            var hint = change.NewValue as string ?? string.Empty;
            PlaceholderText = hint;
            _editableTextBox?.PlaceholderText = hint;
            if (_editableTextBox is MyTextBox editableText)
                editableText.HintText = hint;
        }
        else if (change.Property == IsEditableProperty)
        {
            IsTabStop = !IsEditable;
            RefreshColor();
        }
        else if (change.Property == IsEnabledProperty)
        {
            RefreshColor();
        }
        else if (change.Property == IsDropDownOpenProperty)
        {
            RefreshGlyphRotation(true);
        }
        else if (change.Property == TagProperty)
        {
            _editableTextBox?.Tag = change.NewValue;
        }
    }

    // 主题

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        RefreshColor(false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        DetachPopupOpacity();
        StopAnimations();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        RefreshColor(false);
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例资源列表项，对齐 WPF <c>MyLocalCompItem</c>：
///     44px 单行、34px 图标与状态角标、可压缩的标题/副标题、标签、更新按钮、悬停操作按钮。
/// </summary>
public partial class MyLocalModItem : Grid
{
    private readonly ColumnDefinition _columnExtend;
    private readonly ColumnDefinition _columnPaddingRight;
    private readonly ColumnDefinition _columnSubtitle;
    private readonly ColumnDefinition _columnTitle;
    private readonly int _uuid = LauncherCommon.GetUuid();
    private StackPanel? _buttonStack;
    private TranslateTransform? _buttonTransform;
    private IReadOnlyList<MyIconButton> _buttons = [];
    private bool _checked;
    private GridLength _extendWidth = new(1d, GridUnitType.Star);
    private bool _isMouseDown;
    private DateTime _lastClick = DateTime.MinValue;
    private string? _pathHint;
    private string? _stateLast;
    private GridLength _subtitleWidth = GridLength.Auto;
    private bool _suppressClick;
    private GridLength _titleWidth = GridLength.Auto;

    public MyLocalModItem()
    {
        Entry = null!;
        ViewModel = null!;
        InitializeComponent();
        _columnTitle = PanTitle.ColumnDefinitions[0];
        _columnSubtitle = PanTitle.ColumnDefinitions[1];
        _columnExtend = PanTitle.ColumnDefinitions[3];
        _columnPaddingRight = ColumnDefinitions[5];
    }

    public MyLocalModItem(LocalComp.LocalCompFile entry)
    {
        Entry = entry;
        ViewModel = new MyLocalModItemViewModel();
        InitializeComponent();

        _columnTitle = PanTitle.ColumnDefinitions[0];
        _columnSubtitle = PanTitle.ColumnDefinitions[1];
        _columnExtend = PanTitle.ColumnDefinitions[3];
        _columnPaddingRight = ColumnDefinitions[5];

        Cursor = new Cursor(StandardCursorType.Hand);
        ViewModel.Initialize(entry, new ItemHost(this));

        BtnUpdate.Click += (_, _) => ViewModel.UpdateCommand.Execute(null);
        BtnUpdate.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(BtnUpdate).Properties.IsRightButtonPressed)
            {
                e.Handled = true;
                ViewModel.ShowUpdateLogCommand.Execute(null);
            }
        };

        PointerEntered += (_, _) => RefreshColors();
        PointerExited += (_, _) =>
        {
            _isMouseDown = false;
            RefreshColors();
        };
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) =>
        {
            _isMouseDown = false;
            RefreshColors();
        };
        SizeChanged += (_, _) => UpdateCompression();
        Loaded += (_, _) =>
        {
            _stateLast = null;
            Refresh();
            RefreshColors();
            UpdateCompression();
        };
        Refresh();
    }

    public LocalComp.LocalCompFile Entry { get; }

    public MyLocalModItemViewModel ViewModel { get; }

    public bool AllowFolderNavigation { get; set; } = true;

    public string? PathHint
    {
        get => _pathHint;
        set
        {
            _pathHint = value;
            UpdateToolTip();
        }
    }

    public bool Checked
    {
        get => _checked;
        set => SetChecked(value, true);
    }

    private double ActionColumnWidth => _buttons.Count == 0 ? 4d : 5d + _buttons.Count * 25d;

    public IEnumerable<MyIconButton>? Buttons
    {
        set
        {
            if (_buttonStack is not null)
            {
                Children.Remove(_buttonStack);
                _buttonStack = null;
            }

            _buttons = value?.ToList() ?? [];
            _columnPaddingRight.Width = new GridLength(ActionColumnWidth);
            if (_buttons.Count == 0)
                return;

            _buttonTransform = new TranslateTransform(4d, 0d);
            _buttonStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Opacity = 0d,
                IsHitTestVisible = false,
                Margin = new Thickness(0d, 0d, 5d, 0d),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = _buttonTransform
            };
            foreach (var button in _buttons)
            {
                if (double.IsNaN(button.Height))
                    button.Height = 25d;
                if (double.IsNaN(button.Width))
                    button.Width = 25d;
                _buttonStack.Children.Add(button);
            }

            SetColumn(_buttonStack, 5);
            SetRowSpan(_buttonStack, 99);
            Children.Add(_buttonStack);
            UpdateCompression();
            RefreshColors();
        }
    }

    public event Action<MyLocalModItem>? UpdateRequested;

    public event Action<MyLocalModItem>? ChangelogRequested;

    public event Action<MyLocalModItem>? InfoRequested;

    public event Action<MyLocalModItem>? OpenLocationRequested;

    public event Action<MyLocalModItem>? DeleteRequested;

    public event Action<MyLocalModItem>? EnableDisableRequested;

    public event Action<MyLocalModItem>? Clicked;

    public event Action<MyLocalModItem>? FolderEntered;

    public event Action<MyLocalModItem, bool>? CheckedChanged;

    public void SuppressNextClick()
    {
        _suppressClick = true;
    }

    public void Refresh()
    {
        ViewModel.Refresh();
        ApplyViewModel();
    }

    public void SetChecked(bool value, bool animate)
    {
        if (_checked == value)
            return;
        _checked = value;
        ViewModel.IsChecked = value;
        ApplyChecked(animate);
        CheckedChanged?.Invoke(this, value);
    }

    private void ApplyViewModel()
    {
        LabTitle.Text = ViewModel.TitleText;
        LabTitle.TextDecorations = ViewModel.IsTitleStrikethrough
            ? TextDecorations.Strikethrough
            : null;
        LabSubtitle.Text = ViewModel.SubTitleText;
        LabSubtitle.IsVisible = ViewModel.IsSubTitleVisible;
        _columnSubtitle.Width = LabSubtitle.IsVisible ? _subtitleWidth : new GridLength(0d);
        LabInfo.Text = ViewModel.DescriptionText;
        UpdateToolTip();

        PathLogo.Source = ViewModel.Logo;
        PathLogo.FallbackSource = $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";
        ImgState.IsVisible = ViewModel.IsStateImageVisible;
        ImgState.Source = ViewModel.StateImage;

        TagPanelHelper.FillLocalized(
            PanTags,
            ViewModel.TagKeys,
            ContainerTheme.TextSecondary(this));
        PanTags.IsVisible = ViewModel.IsTagsVisible;

        BtnUpdate.IsVisible = ViewModel.IsUpdateVisible;
        ToolTip.SetTip(
            BtnUpdate,
            string.IsNullOrEmpty(ViewModel.UpdateToolTip) ? null : ViewModel.UpdateToolTip);

        ApplyChecked(false);
        UpdateCompression();
    }

    private void UpdateToolTip()
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(_pathHint))
            lines.Add(_pathHint);
        if (!string.IsNullOrWhiteSpace(ViewModel.InfoToolTip))
            lines.Add(ViewModel.InfoToolTip);
        ToolTip.SetTip(this, lines.Count == 0 ? null : string.Join("\r\n", lines));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || IsPointerOnActionButton(e.Source as Visual))
            return;
        _isMouseDown = true;
        RefreshColors();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right
            && !IsPointerOnActionButton(e.Source as Visual))
        {
            e.Handled = true;
            InfoRequested?.Invoke(this);
            return;
        }

        var wasMouseDown = _isMouseDown;
        _isMouseDown = false;
        if (e.InitialPressMouseButton != MouseButton.Left
            || !wasMouseDown
            || IsPointerOnActionButton(e.Source as Visual))
        {
            RefreshColors();
            return;
        }

        if (_suppressClick)
        {
            _suppressClick = false;
            RefreshColors();
            return;
        }

        var now = DateTime.UtcNow;
        if (Entry.IsFolder
            && AllowFolderNavigation
            && (now - _lastClick).TotalMilliseconds <= 300d)
        {
            _lastClick = DateTime.MinValue;
            SetChecked(false, true);
            FolderEntered?.Invoke(this);
            RefreshColors();
            return;
        }

        _lastClick = now;
        SetChecked(!Checked, true);
        Clicked?.Invoke(this);
        RefreshColors();
    }

    private void RefreshColors()
    {
        var stateNew = _isMouseDown
            ? "MouseDown"
            : IsPointerOver
                ? "MouseOver"
                : Checked
                    ? "Checked"
                    : "Idle";
        if (_stateLast == stateNew)
            return;
        _stateLast = stateNew;

        var showButtons = stateNew is "MouseOver" or "MouseDown";
        var time = IsPointerOver ? 120 : 180;
        if (!ContainerAnimation.CanAnimate(this))
        {
            ApplyColorStateInstant(stateNew, showButtons);
            return;
        }

        var animations = new List<IAnimation>();
        AddButtonAnimations(animations, showButtons, time);
        var scale = EnsureBackScale();

        if (stateNew is "MouseOver" or "MouseDown" or "Checked")
        {
            animations.Add(
                new ColorAnimation(
                    RectBack,
                    Border.BackgroundProperty,
                    stateNew == "MouseDown" ? "ColorBrush6" : "ColorBrushBg1",
                    time));
            animations.Add(
                new OpacityAnimation(
                    RectBack,
                    1d - RectBack.Opacity,
                    time,
                    ease: new FluentEaseOut()));
            animations.Add(
                new ScaleTransformAnimation(
                    RectBack,
                    (stateNew == "MouseDown" ? 0.996d : 1d) - scale.ScaleX,
                    (int)Math.Round(time * 1.2d),
                    ease: new FluentEaseOut()));
        }
        else
        {
            animations.Add(new OpacityAnimation(RectBack, -RectBack.Opacity, time));
            animations.Add(
                new ColorAnimation(RectBack, Border.BackgroundProperty, "ColorBrush7", time));
            animations.Add(
                new ScaleTransformAnimation(
                    RectBack,
                    0.996d - scale.ScaleX,
                    time,
                    ease: new FluentEaseOut()));
            animations.Add(new ScaleTransformAnimation(RectBack, -0.196d, 1, after: true));
        }

        AnimationService.Run(animations, $"LocalModItem Color {_uuid}");
    }

    private void ApplyColorStateInstant(string stateNew, bool showButtons)
    {
        AnimationService.CancelAnimationByName($"LocalModItem Color {_uuid}");
        if (_buttonStack is not null)
        {
            _buttonStack.Opacity = showButtons ? 1d : 0d;
            _buttonStack.IsHitTestVisible = showButtons;
            if (_buttonStack.RenderTransform is TranslateTransform transform)
                transform.X = showButtons ? 0d : 4d;
        }

        var showBack = stateNew is not "Idle";
        RectBack.Opacity = showBack ? 1d : 0d;
        ContainerTheme.BindDynamic(
            RectBack,
            Border.BackgroundProperty,
            stateNew == "MouseDown"
                ? "ColorBrush6"
                : showBack
                    ? "ColorBrushBg1"
                    : "ColorBrush7");

        var scale = EnsureBackScale();
        var scaleValue = stateNew switch
        {
            "MouseDown" => 0.996d,
            "Idle" => 0.8d,
            _ => 1d
        };
        scale.ScaleX = scaleValue;
        scale.ScaleY = scaleValue;
    }

    private void AddButtonAnimations(List<IAnimation> animations, bool showButtons, int time)
    {
        if (_buttonStack is null)
            return;

        _buttonStack.IsHitTestVisible = showButtons;
        _buttonTransform ??= new TranslateTransform(4d, 0d);
        if (_buttonStack.RenderTransform is not TranslateTransform)
            _buttonStack.RenderTransform = _buttonTransform;

        animations.Add(
            new OpacityAnimation(
                _buttonStack,
                (showButtons ? 1d : 0d) - _buttonStack.Opacity,
                (int)Math.Round(time * (showButtons ? 0.7d : 0.4d)),
                showButtons ? (int)Math.Round(time * 0.3d) : 0));
        animations.Add(
            new TranslateXAnimation(
                _buttonStack,
                (showButtons ? 0d : 4d) - _buttonTransform.X,
                (int)Math.Round(time * (showButtons ? 0.3d : 0.4d)),
                showButtons ? (int)Math.Round(time * 0.7d) : 0));
    }

    private void ApplyChecked(bool animate)
    {
        var titleKey = _checked
            ? Entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine
                ? "ColorBrush2"
                : "ColorBrush5"
            : Entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine
                ? "ColorBrush1"
                : "ColorBrushGray4";

        if (!animate || !ContainerAnimation.CanAnimate(this))
        {
            AnimationService.CancelAnimationByName($"MyLocalModItem Checked {_uuid}");
            RectCheck.VerticalAlignment = VerticalAlignment.Center;
            RectCheck.Margin = new Thickness(-3d, 0d, 0d, 0d);
            RectCheck.Height = _checked ? 32d : 0d;
            RectCheck.Opacity = _checked ? 1d : 0d;
            ContainerTheme.BindDynamic(LabTitle, TextBlock.ForegroundProperty, titleKey);
            RefreshColors();
            return;
        }

        var animations = new List<IAnimation>();
        if (_checked)
        {
            var delta = 32d - RectCheck.Height;
            RectCheck.VerticalAlignment = VerticalAlignment.Center;
            RectCheck.Margin = new Thickness(-3d, 0d, 0d, 0d);
            animations.Add(
                new HeightAnimation(
                    RectCheck,
                    delta * 0.4d,
                    200,
                    ease: new FluentEaseOut(EasePower.Weak)));
            animations.Add(
                new HeightAnimation(
                    RectCheck,
                    delta * 0.6d,
                    300,
                    ease: new BackEaseWithPowerOut(EasePower.Weak)));
            animations.Add(new OpacityAnimation(RectCheck, 1d - RectCheck.Opacity, 30));
            animations.Add(
                new ColorAnimation(LabTitle, TextBlock.ForegroundProperty, titleKey, 200));
        }
        else
        {
            animations.Add(
                new HeightAnimation(
                    RectCheck,
                    -RectCheck.Height,
                    120,
                    ease: new FluentEaseIn(EasePower.Weak)));
            animations.Add(new OpacityAnimation(RectCheck, -RectCheck.Opacity, 70, 40));
            animations.Add(
                new ColorAnimation(LabTitle, TextBlock.ForegroundProperty, titleKey, 120));
            animations.Add(new ActionAnimation(() => RectCheck.Height = 0d, true));
        }

        RefreshColors();
        AnimationService.Run(animations, $"MyLocalModItem Checked {_uuid}");
    }

    private ScaleTransform EnsureBackScale()
    {
        if (RectBack.RenderTransform is ScaleTransform scale)
            return scale;

        scale = new ScaleTransform(0.8d, 0.8d);
        RectBack.RenderTransformOrigin = RelativePoint.Center;
        RectBack.RenderTransform = scale;
        return scale;
    }

    private void UpdateCompression()
    {
        if (!IsLoaded || Bounds.Width <= 0d)
            return;
        var titleWidth = LabTitle.DesiredSize.Width;
        var subtitleWidth = LabSubtitle.IsVisible ? LabSubtitle.DesiredSize.Width : 0d;
        var fixedWidth = 6d + 34d + 7d + 4d + ActionColumnWidth + 8d;
        var available = Math.Max(0d, Bounds.Width - fixedWidth);

        var level = 0;
        if (titleWidth + subtitleWidth > available)
            level = subtitleWidth > 0d ? 1 : 2;
        if (level == 1 && titleWidth > available)
            level = 2;

        switch (level)
        {
            case 0:
                _titleWidth = GridLength.Auto;
                _subtitleWidth = GridLength.Auto;
                _extendWidth = new GridLength(1d, GridUnitType.Star);
                LabSubtitle.IsVisible = !string.IsNullOrEmpty(LabSubtitle.Text);
                break;

            case 1:
                _titleWidth = GridLength.Auto;
                _subtitleWidth = new GridLength(1d, GridUnitType.Star);
                _extendWidth = new GridLength(0d, GridUnitType.Pixel);
                LabSubtitle.IsVisible = false;
                break;

            default:
                _titleWidth = new GridLength(1d, GridUnitType.Star);
                _subtitleWidth = new GridLength(0d, GridUnitType.Pixel);
                _extendWidth = new GridLength(0d, GridUnitType.Pixel);
                LabSubtitle.IsVisible = false;
                break;
        }

        _columnTitle.Width = _titleWidth;
        _columnSubtitle.Width = _subtitleWidth;
        _columnExtend.Width = _extendWidth;
    }

    private bool IsPointerOnActionButton(Visual? source)
    {
        for (var current = source;
             current is not null && current != this;
             current = current.GetVisualParent())
            if (current is MyIconButton or MyIconTextButton)
                return true;
        return false;
    }

    /// <summary>把 ViewModel 的命令转回控件现有的公开事件，保持页面构造方式不变。</summary>
    private sealed class ItemHost(MyLocalModItem owner) : ILocalModItemHost
    {
        public Comp.CompType CompType => owner.Entry.Comp?.Type ?? Comp.CompType.Mod;

        public void OpenFileLocation(LocalComp.LocalCompFile entry)
        {
            owner.OpenLocationRequested?.Invoke(owner);
        }

        public void ShowInfo(LocalComp.LocalCompFile entry)
        {
            owner.InfoRequested?.Invoke(owner);
        }

        public void Delete(LocalComp.LocalCompFile entry)
        {
            owner.DeleteRequested?.Invoke(owner);
        }

        public void ToggleEnabled(LocalComp.LocalCompFile entry)
        {
            owner.EnableDisableRequested?.Invoke(owner);
        }

        public void EnterFolder(string path)
        {
            owner.FolderEntered?.Invoke(owner);
        }

        public void Update(LocalComp.LocalCompFile entry)
        {
            owner.UpdateRequested?.Invoke(owner);
        }

        public void ShowChangelog(LocalComp.LocalCompFile entry)
        {
            owner.ChangelogRequested?.Invoke(owner);
        }
    }
}
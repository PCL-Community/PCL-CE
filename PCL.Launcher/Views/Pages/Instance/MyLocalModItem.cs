using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例资源列表项，对齐 WPF <c>MyLocalCompItem</c>：
///     44px 单行、34px 图标与状态角标、可压缩的标题/副标题、标签、更新按钮、悬停操作按钮。
/// </summary>
public class MyLocalModItem : Grid
{
    private readonly MyIconButton _btnUpdate;
    private readonly ColumnDefinition _columnExtend;
    private readonly ColumnDefinition _columnPaddingRight;
    private readonly ColumnDefinition _columnSubtitle;
    private readonly ColumnDefinition _columnTitle;
    private readonly TextBlock _labInfo;
    private readonly TextBlock _labSubtitle;
    private readonly TextBlock _labTitle;
    private readonly MyImage _logo;
    private readonly StackPanel _panTags;
    private readonly Border _rectBack;
    private readonly Border _rectCheck;
    private readonly MyImage _state;
    private StackPanel? _buttonStack;
    private bool _checked;
    private GridLength _extendWidth = new(1d, GridUnitType.Star);
    private GridLength _subtitleWidth = GridLength.Auto;
    private GridLength _titleWidth = GridLength.Auto;

    public MyLocalModItem(LocalComp.LocalCompFile entry)
    {
        Entry = entry;
        ViewModel = new MyLocalModItemViewModel();
        ViewModel.Initialize(entry, new ItemHost(this));
        Height = 44d;
        ColumnDefinitions = new ColumnDefinitions("6,34,7,Auto,*,4");
        RowDefinitions = new RowDefinitions("*,20,18,*");
        RenderTransformOrigin = RelativePoint.Center;
        ContainerTheme.BindDynamic(this, BackgroundProperty, "ColorBrushSemiTransparent");

        _rectBack = new Border
        {
            CornerRadius = new CornerRadius(3d),
            BorderThickness = new Thickness(1d),
            IsHitTestVisible = false,
            Opacity = 0d
        };
        ContainerTheme.BindDynamic(_rectBack, Border.BackgroundProperty, "ColorBrush7");
        ContainerTheme.BindDynamic(_rectBack, Border.BorderBrushProperty, "ColorBrush6");
        SetRowSpan(_rectBack, 10);
        SetColumnSpan(_rectBack, 10);
        Children.Add(_rectBack);

        _rectCheck = new Border
        {
            Width = 5d,
            Height = 0d,
            CornerRadius = new CornerRadius(2d),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(-3d, 0d, 0d, 0d)
        };
        ContainerTheme.BindDynamic(_rectCheck, Border.BackgroundProperty, "ColorBrush3");
        SetRowSpan(_rectCheck, 10);
        Children.Add(_rectCheck);

        _logo = new MyImage
        {
            Width = 34d,
            Height = 34d,
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(6d),
            FallbackSource = $"{LauncherPaths.ImageRoot}Icons/NoIcon.png",
            IsHitTestVisible = false
        };
        SetRow(_logo, 1);
        SetRowSpan(_logo, 2);
        SetColumn(_logo, 1);
        Children.Add(_logo);

        _state = new MyImage
        {
            Width = 20d,
            Height = 20d,
            Margin = new Thickness(0d, 0d, -5d, -3d),
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
            IsVisible = false
        };
        SetRow(_state, 1);
        SetRowSpan(_state, 2);
        SetColumn(_state, 1);
        Children.Add(_state);

        var titleGrid = new Grid
        {
            Margin = new Thickness(0d, 1d, 0d, 0d),
            VerticalAlignment = VerticalAlignment.Center
        };
        _columnTitle = new ColumnDefinition(_titleWidth);
        _columnSubtitle = new ColumnDefinition(_subtitleWidth);
        _columnExtend = new ColumnDefinition(_extendWidth);
        var columnUpdate = new ColumnDefinition(GridLength.Auto);
        titleGrid.ColumnDefinitions.Add(_columnTitle);
        titleGrid.ColumnDefinitions.Add(_columnSubtitle);
        titleGrid.ColumnDefinitions.Add(columnUpdate);
        titleGrid.ColumnDefinitions.Add(_columnExtend);
        SetRow(titleGrid, 1);
        SetColumn(titleGrid, 3);
        SetColumnSpan(titleGrid, 2);
        Children.Add(titleGrid);

        _labTitle = new TextBlock
        {
            FontSize = 14d,
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        ContainerTheme.BindDynamic(_labTitle, TextBlock.ForegroundProperty, "ColorBrush1");
        titleGrid.Children.Add(_labTitle);

        _labSubtitle = new TextBlock
        {
            FontSize = 12d,
            Opacity = 0.4d,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsHitTestVisible = false
        };
        ContainerTheme.BindDynamic(_labSubtitle, TextBlock.ForegroundProperty, "ColorBrushGray1");
        SetColumn(_labSubtitle, 1);
        titleGrid.Children.Add(_labSubtitle);

        _btnUpdate = new MyIconButton
        {
            Width = 21d,
            Height = 21d,
            Margin = new Thickness(-2d, -1.6d, 0d, 0d),
            SvgIcon = "lucide/upload",
            Opacity = 0.4d,
            Theme = MyIconButton.Themes.Black,
            IsVisible = false
        };
        _btnUpdate.Click += (_, _) => ViewModel.UpdateCommand.Execute(null);
        _btnUpdate.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(_btnUpdate).Properties.IsRightButtonPressed)
            {
                e.Handled = true;
                ViewModel.ShowUpdateLogCommand.Execute(null);
            }
        };
        SetColumn(_btnUpdate, 2);
        titleGrid.Children.Add(_btnUpdate);

        _panTags = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(-1d, 0d, 1d, 0d),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        SetRow(_panTags, 2);
        SetColumn(_panTags, 3);
        Children.Add(_panTags);

        _labInfo = new TextBlock
        {
            FontSize = 12d,
            Margin = new Thickness(0d, 0d, 3d, 1d),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsHitTestVisible = false
        };
        ContainerTheme.BindDynamic(_labInfo, TextBlock.ForegroundProperty, "ColorBrushGray4");
        SetRow(_labInfo, 2);
        SetColumn(_labInfo, 4);
        Children.Add(_labInfo);

        _columnPaddingRight = ColumnDefinitions[5];
        PointerEntered += (_, _) => RefreshColors();
        PointerExited += (_, _) => RefreshColors();
        SizeChanged += (_, _) => UpdateCompression();
        Loaded += (_, _) =>
        {
            Refresh();
            UpdateCompression();
        };
        Refresh();
    }

    public LocalComp.LocalCompFile Entry { get; }

    public MyLocalModItemViewModel ViewModel { get; }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
                return;
            _checked = value;
            ViewModel.IsChecked = value;
            ApplyChecked();
        }
    }

    public IEnumerable<MyIconButton>? Buttons
    {
        set
        {
            if (_buttonStack is not null)
            {
                Children.Remove(_buttonStack);
                _buttonStack = null;
            }

            if (value is null)
                return;
            var buttons = value.ToList();
            if (buttons.Count == 0)
                return;

            _buttonStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Opacity = 0d,
                Margin = new Thickness(0d, 0d, 5d, 0d),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            foreach (var button in buttons)
            {
                if (double.IsNaN(button.Height))
                    button.Height = 25d;
                if (double.IsNaN(button.Width))
                    button.Width = 25d;
                _buttonStack.Children.Add(button);
            }

            SetColumnSpan(_buttonStack, 10);
            SetRowSpan(_buttonStack, 10);
            Children.Add(_buttonStack);
            PointerEntered += (_, _) => ShowButtons(true);
            PointerExited += (_, _) => ShowButtons(false);
        }
    }

    public event Action<MyLocalModItem>? UpdateRequested;

    public event Action<MyLocalModItem>? ChangelogRequested;

    public event Action<MyLocalModItem>? InfoRequested;

    public event Action<MyLocalModItem>? OpenLocationRequested;

    public event Action<MyLocalModItem>? DeleteRequested;

    public event Action<MyLocalModItem>? EnableDisableRequested;

    public void Refresh()
    {
        ViewModel.Refresh();
        ApplyViewModel();
    }

    private void ApplyViewModel()
    {
        _labTitle.Text = ViewModel.TitleText;
        _labTitle.TextDecorations = ViewModel.IsTitleStrikethrough
            ? TextDecorations.Strikethrough
            : null;
        _labSubtitle.Text = ViewModel.SubTitleText;
        _labSubtitle.IsVisible = ViewModel.IsSubTitleVisible;
        _columnSubtitle.Width = _labSubtitle.IsVisible ? _subtitleWidth : new GridLength(0d);
        _labInfo.Text = ViewModel.DescriptionText;
        ToolTip.SetTip(
            _labInfo,
            string.IsNullOrEmpty(ViewModel.InfoToolTip) ? null : ViewModel.InfoToolTip);

        _logo.Source = ViewModel.Logo;
        _logo.FallbackSource = $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";
        _state.IsVisible = ViewModel.IsStateImageVisible;
        _state.Source = ViewModel.StateImage;

        TagPanelHelper.FillLocalized(
            _panTags,
            ViewModel.TagKeys,
            ContainerTheme.TextSecondary(this));
        _panTags.IsVisible = ViewModel.IsTagsVisible;

        _btnUpdate.IsVisible = ViewModel.IsUpdateVisible;
        ToolTip.SetTip(
            _btnUpdate,
            string.IsNullOrEmpty(ViewModel.UpdateToolTip) ? null : ViewModel.UpdateToolTip);

        ApplyChecked();
        UpdateCompression();
    }

    private void ShowButtons(bool show)
    {
        if (_buttonStack is null)
            return;
        _buttonStack.Opacity = show ? 1d : 0d;
        _columnPaddingRight.Width =
            new GridLength(show ? 5d + _buttonStack.Children.Count * 25d : 4d);
        UpdateCompression();
    }

    private void ApplyChecked()
    {
        _rectCheck.Height = _checked ? 32d : 0d;
        _rectCheck.Opacity = _checked ? 1d : 0d;
        _rectCheck.Margin = _checked
            ? new Thickness(-3d, 6d, 0d, 6d)
            : new Thickness(-3d, 0d, 0d, 0d);
        ContainerTheme.BindDynamic(
            _labTitle,
            TextBlock.ForegroundProperty,
            _checked
                ? Entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine
                    ? "ColorBrush2"
                    : "ColorBrush5"
                : Entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine
                    ? "ColorBrush1"
                    : "ColorBrushGray4");
        _rectBack.Opacity = _checked ? 1d : 0d;
    }

    private void RefreshColors()
    {
        if (_checked || IsPointerOver)
            ContainerTheme.BindDynamic(_rectBack, Border.BackgroundProperty, "ColorBrushBg1");
        _rectBack.Opacity = _checked || IsPointerOver ? 1d : 0d;
        ShowButtons(IsPointerOver);
    }

    private void UpdateCompression()
    {
        if (!IsLoaded || Bounds.Width <= 0d)
            return;
        var titleWidth = _labTitle.DesiredSize.Width;
        var subtitleWidth = _labSubtitle.IsVisible ? _labSubtitle.DesiredSize.Width : 0d;
        var fixedWidth = 6d + 34d + 7d + 4d + _columnPaddingRight.Width.Value + 8d;
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
                _labSubtitle.IsVisible = !string.IsNullOrEmpty(_labSubtitle.Text);
                break;

            case 1:
                _titleWidth = GridLength.Auto;
                _subtitleWidth = new GridLength(1d, GridUnitType.Star);
                _extendWidth = new GridLength(0d, GridUnitType.Pixel);
                _labSubtitle.IsVisible = false;
                break;

            default:
                _titleWidth = new GridLength(1d, GridUnitType.Star);
                _subtitleWidth = new GridLength(0d, GridUnitType.Pixel);
                _extendWidth = new GridLength(0d, GridUnitType.Pixel);
                _labSubtitle.IsVisible = false;
                break;
        }

        _columnTitle.Width = _titleWidth;
        _columnSubtitle.Width = _subtitleWidth;
        _columnExtend.Width = _extendWidth;
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
            owner.Checked = false;
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
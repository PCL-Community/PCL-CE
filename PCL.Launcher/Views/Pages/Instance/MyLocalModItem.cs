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
public partial class MyLocalModItem : Grid
{
    private readonly ColumnDefinition _columnExtend;
    private readonly ColumnDefinition _columnPaddingRight;
    private readonly ColumnDefinition _columnSubtitle;
    private readonly ColumnDefinition _columnTitle;
    private StackPanel? _buttonStack;
    private bool _checked;
    private GridLength _extendWidth = new(1d, GridUnitType.Star);
    private GridLength _subtitleWidth = GridLength.Auto;
    private GridLength _titleWidth = GridLength.Auto;

    public MyLocalModItem()
    {
        Entry = null!;
        ViewModel = null!;
        InitializeComponent();
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

            SetColumnSpan(_buttonStack, 99);
            SetRowSpan(_buttonStack, 99);
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
        LabTitle.Text = ViewModel.TitleText;
        LabTitle.TextDecorations = ViewModel.IsTitleStrikethrough
            ? TextDecorations.Strikethrough
            : null;
        LabSubtitle.Text = ViewModel.SubTitleText;
        LabSubtitle.IsVisible = ViewModel.IsSubTitleVisible;
        _columnSubtitle.Width = LabSubtitle.IsVisible ? _subtitleWidth : new GridLength(0d);
        LabInfo.Text = ViewModel.DescriptionText;
        ToolTip.SetTip(
            LabInfo,
            string.IsNullOrEmpty(ViewModel.InfoToolTip) ? null : ViewModel.InfoToolTip);

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
        RectCheck.Height = _checked ? 32d : 0d;
        RectCheck.Opacity = _checked ? 1d : 0d;
        RectCheck.Margin = _checked
            ? new Thickness(-3d, 6d, 0d, 6d)
            : new Thickness(-3d, 0d, 0d, 0d);
        ContainerTheme.BindDynamic(
            LabTitle,
            TextBlock.ForegroundProperty,
            _checked
                ? Entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine
                    ? "ColorBrush2"
                    : "ColorBrush5"
                : Entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine
                    ? "ColorBrush1"
                    : "ColorBrushGray4");
        RectBack.Opacity = _checked ? 1d : 0d;
    }

    private void RefreshColors()
    {
        if (_checked || IsPointerOver)
            ContainerTheme.BindDynamic(RectBack, Border.BackgroundProperty, "ColorBrushBg1");
        RectBack.Opacity = _checked || IsPointerOver ? 1d : 0d;
        ShowButtons(IsPointerOver);
    }

    private void UpdateCompression()
    {
        if (!IsLoaded || Bounds.Width <= 0d)
            return;
        var titleWidth = LabTitle.DesiredSize.Width;
        var subtitleWidth = LabSubtitle.IsVisible ? LabSubtitle.DesiredSize.Width : 0d;
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
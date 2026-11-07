using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

public sealed partial class MyCompItem : Grid
{
    public MyCompItem()
    {
        ViewModel = null!;
        Project = null!;
        InitializeComponent();
    }

    public MyCompItem(
        Comp.CompProject project,
        bool showMcVersionDesc = true,
        bool showLoaderDesc = true,
        bool showQuickDownload = false)
        : this(
            MyCompItemViewModel.Create(
                project,
                showMcVersionDesc,
                showLoaderDesc,
                showQuickDownload),
            project)
    {
    }

    public MyCompItem(MyCompItemViewModel viewModel) : this(
        viewModel,
        viewModel.Project ?? throw new ArgumentNullException(nameof(viewModel)))
    {
    }

    private MyCompItem(MyCompItemViewModel viewModel, Comp.CompProject project)
    {
        ViewModel = viewModel;
        Project = project;
        InitializeComponent();

        Cursor = new Cursor(StandardCursorType.Hand);
        ApplyLogo(project);
        Tags = viewModel.Tags;
        ApplyStatsVisibility(viewModel);
        BtnDownload.IsVisible = viewModel.ShowDownloadButton;
        BtnFavorite.SvgIcon = viewModel.FavoriteIcon;
        BtnFavorite.IsVisible = viewModel.ShowFavoriteButton;
        UpdatePanButtons();

        LabInfo.PointerEntered += (_, _) => UpdateDescriptionToolTip();
        BtnDownload.Click += (_, _) =>
        {
            if (CanInteraction && PanButtons is { Opacity: > 0d })
                Comp.QuickDownload(Project);
        };
        BtnFavorite.Click += (_, _) =>
        {
            if (CanInteraction && PanButtons is { Opacity: > 0d })
                CompFavoritesMenu.Show(Project, BtnFavorite, RefreshFavoriteStatus);
        };

        PointerEntered += (_, _) =>
        {
            RectBack.Opacity = 1d;
            if (ShowFavoriteBtn || ShowDownloadBtn)
                PanButtons.Opacity = 1d;
        };
        PointerExited += (_, _) =>
        {
            RectBack.Opacity = 0d;
            PanButtons.Opacity = 0d;
        };
        PointerReleased += (_, e) =>
        {
            if (!CanInteraction || e.InitialPressMouseButton != MouseButton.Left)
                return;
            if (IsPointerOnActionButton(e.Source as Visual))
                return;
            ViewModel.ClickCommand.Execute(null);
        };
        ViewModel.ClickRequested += _ => Click?.Invoke(this);
    }

    public Comp.CompProject Project { get; }

    public MyCompItemViewModel ViewModel { get; }

    public bool CanInteraction { get; set; } = true;

    /// <summary>标签列表，内容为本地化资源键。</summary>
    public IEnumerable<string>? Tags
    {
        set =>
            TagPanelHelper.FillLocalized(
                PanTags,
                value,
                new SolidColorBrush(Color.FromRgb(134, 134, 134)));
    }

    public bool ShowDownloadBtn
    {
        get => BtnDownload.IsVisible;
        set
        {
            BtnDownload.IsVisible = value;
            UpdatePanButtons();
        }
    }

    public bool ShowFavoriteBtn
    {
        get => BtnFavorite.IsVisible;
        set
        {
            BtnFavorite.IsVisible = value;
            UpdatePanButtons();
        }
    }

    private void UpdatePanButtons()
    {
        PanButtons.IsVisible = ShowFavoriteBtn || ShowDownloadBtn;
    }

    private void ApplyStatsVisibility(MyCompItemViewModel viewModel)
    {
        var showVersion = viewModel.ShowVersion && !string.IsNullOrEmpty(viewModel.VersionText);
        var showDownload = viewModel.ShowDownload && !string.IsNullOrEmpty(viewModel.DownloadText);
        var showTime = viewModel.ShowTime && !string.IsNullOrEmpty(viewModel.TimeText);
        var showSource = viewModel.ShowSource && !string.IsNullOrEmpty(viewModel.SourceText);

        SvgIconVersion.IsVisible = showVersion;
        LabVersion.IsVisible = showVersion;
        SvgIconDownload.IsVisible = showDownload;
        LabDownload.IsVisible = showDownload;
        SvgIconTime.IsVisible = showTime;
        LabTime.IsVisible = showTime;
        SvgIconSource.IsVisible = showSource;
        LabSource.IsVisible = showSource;

        var columns = PanStats.ColumnDefinitions;
        SetStatColumns(columns, 0, showVersion);
        SetStatColumns(columns, 3, showDownload);
        SetStatColumns(columns, 6, showTime);
        SetStatColumns(columns, 9, showSource);
    }

    private static void SetStatColumns(ColumnDefinitions columns, int start, bool show)
    {
        columns[start].Width = show ? GridLength.Auto : new GridLength(0d);
        columns[start + 1].Width = show ? GridLength.Auto : new GridLength(0d);
        columns[start + 2].Width = show
            ? new GridLength(0.25d, GridUnitType.Star)
            : new GridLength(0d);
    }

    private void UpdateDescriptionToolTip()
    {
        var text = LabInfo.Text;
        if (string.IsNullOrEmpty(text) || LabInfo.Bounds.Width <= 0d)
        {
            ViewModel.SetDescriptionToolTip(null);
            ToolTip.SetTip(LabInfo, null);
            return;
        }

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                LabInfo.FontFamily,
                LabInfo.FontStyle,
                LabInfo.FontWeight,
                LabInfo.FontStretch),
            LabInfo.FontSize,
            LabInfo.Foreground);
        var tooltip = formatted.WidthIncludingTrailingWhitespace
            > LabInfo.Bounds.Width + 0.5d
                ? text
                : null;
        ViewModel.SetDescriptionToolTip(tooltip);
        ToolTip.SetTip(LabInfo, tooltip);
    }

    public event Action<MyCompItem>? Click;

    public void RefreshFavoriteStatus()
    {
        ViewModel.RefreshFavoriteStatus();
        BtnFavorite.SvgIcon = ViewModel.FavoriteIcon;
    }

    private bool IsPointerOnActionButton(Visual? source)
    {
        for (var current = source;
             current is not null && current != this;
             current = current.GetVisualParent())
            if (current is MyIconButton)
                return true;
        return false;
    }

    private void ApplyLogo(Comp.CompProject project)
    {
        if (string.IsNullOrEmpty(project.LogoUrl))
        {
            PathLogo.Source = "avares://PCL.Launcher/Assets/Images/Icons/NoIcon.png";
            return;
        }

        PathLogo.Source = project.LogoUrl;
        try
        {
            var fallback = GameDownload.DlSourceModGet(project.LogoUrl);
            if (!string.IsNullOrEmpty(fallback))
                PathLogo.FallbackSource = fallback;
        }
        catch (Exception)
        {
            // ignored
        }
    }
}
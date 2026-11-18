using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>资源列表条目的视图模型。负责标题、标签、统计列、tooltip 与收藏状态， 控件本身只负责按照这些状态构建/更新视觉树。</summary>
public partial class MyCompItemViewModel : ViewModelBase
{
    public const string VersionSizeGroup = "CompItemVersion";
    public const string DownloadSizeGroup = "LabDownload";
    public const string TimeSizeGroup = "CompItemTime";
    public const string SourceSizeGroup = "CompItemSource";

    private bool _showLoaderDesc = true;
    private bool _showMcVersionDesc = true;

    public string DescriptionText =>
        (Project?.Description ?? string.Empty)
        .Replace("\r", string.Empty)
        .Replace("\n", string.Empty);

    [ObservableProperty] public partial string? DescriptionToolTip { get; set; }

    public string DownloadText =>
        Project is null
            ? string.Empty
            : Lang.CompactNumber(Project.DownloadCount);

    [ObservableProperty] public partial string FavoriteIcon { get; set; } = "lucide/heart";

    [ObservableProperty] public partial bool IsVirtualized { get; set; }

    [ObservableProperty] public partial Comp.CompProject? Project { get; set; }

    [ObservableProperty] public partial bool ShowDownload { get; set; } = true;

    [ObservableProperty] public partial bool ShowDownloadButton { get; set; }

    [ObservableProperty] public partial bool ShowFavoriteButton { get; set; } = true;

    [ObservableProperty] public partial bool ShowSource { get; set; } = true;

    [ObservableProperty] public partial bool ShowTime { get; set; } = true;

    [ObservableProperty] public partial bool ShowVersion { get; set; } = true;

    public string SourceText =>
        Project is null
            ? string.Empty
            : Project.FromCurseForge
                ? "CurseForge"
                : "Modrinth";

    public string SubTitleText => Project?.GetControlTitle(true).Value ?? string.Empty;

    public bool SubTitleVisible => !string.IsNullOrEmpty(SubTitleText);

    public string TimeText =>
        Project?.LastUpdate is { } lastUpdate
            ? Lang.TimeSpan(lastUpdate - DateTime.Now, 1)
            : string.Empty;

    public string TitleText => Project?.GetControlTitle(true).Key ?? string.Empty;

    [ObservableProperty] public partial bool UseSharedSizeGroups { get; set; } = true;

    public string VersionText =>
        Project is null
            ? string.Empty
            : Project.BuildVersionDescription(_showMcVersionDesc, _showLoaderDesc);

    public IReadOnlyList<string> Tags { get; set; } = [];

    public string VersionSharedSizeGroup { get; set; } = VersionSizeGroup;

    public string DownloadSharedSizeGroup { get; set; } = DownloadSizeGroup;

    public string TimeSharedSizeGroup { get; set; } = TimeSizeGroup;

    public string SourceSharedSizeGroup { get; set; } = SourceSizeGroup;

    public event Action<MyCompItemViewModel>? ClickRequested;

    public static MyCompItemViewModel Create(
        Comp.CompProject project,
        bool showMcVersionDesc = true,
        bool showLoaderDesc = true,
        bool showQuickDownload = false,
        bool showFavoriteButton = true)
    {
        var viewModel = new MyCompItemViewModel();
        viewModel.Update(
            project,
            showMcVersionDesc,
            showLoaderDesc,
            showQuickDownload,
            showFavoriteButton);
        return viewModel;
    }

    public void Update(
        Comp.CompProject project,
        bool showMcVersionDesc = true,
        bool showLoaderDesc = true,
        bool showQuickDownload = false,
        bool showFavoriteButton = true)
    {
        _showMcVersionDesc = showMcVersionDesc;
        _showLoaderDesc = showLoaderDesc;
        Project = project;
        Tags = [.. project.Tags];
        ShowVersion = showMcVersionDesc || showLoaderDesc;
        ShowTime = project.LastUpdate is not null;
        ShowDownload = !string.IsNullOrEmpty(DownloadText);
        ShowSource = true;
        ShowFavoriteButton = showFavoriteButton;
        ShowDownloadButton = showQuickDownload;
        IsVirtualized = false;
        NotifyTextProperties();
        RefreshFavoriteStatus();
    }

    private void NotifyTextProperties()
    {
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(SubTitleText));
        OnPropertyChanged(nameof(SubTitleVisible));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(DownloadText));
        OnPropertyChanged(nameof(SourceText));
    }

    [RelayCommand]
    public void RefreshFavoriteStatus()
    {
        if (Project is null)
            return;
        FavoriteIcon = Comp.CompFavorites.IsFavourite(Project.Id)
            ? "lucide/heart-filled"
            : "lucide/heart";
    }

    [RelayCommand]
    public void RefreshStatistics()
    {
        if (Project is null)
            return;
        Update(
            Project,
            _showMcVersionDesc,
            _showLoaderDesc,
            ShowDownloadButton,
            ShowFavoriteButton);
    }

    public void PrepareForReuse()
    {
        IsVirtualized = true;
        DescriptionToolTip = null;
    }

    public void SetDescriptionToolTip(string? tooltip)
    {
        DescriptionToolTip = tooltip;
    }

    [RelayCommand]
    private void Click()
    {
        ClickRequested?.Invoke(this);
    }
}
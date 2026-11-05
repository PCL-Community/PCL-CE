using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>
///     资源列表条目的视图模型。负责标题、标签、统计列、tooltip 与收藏状态，
///     控件本身只负责按照这些状态构建/更新视觉树。
/// </summary>
public partial class MyCompItemViewModel : ViewModelBase
{
    /// <summary>版本列使用的共享尺寸组名。</summary>
    public const string VersionSizeGroup = "CompItemVersion";

    /// <summary>下载量列使用的共享尺寸组名。</summary>
    public const string DownloadSizeGroup = "LabDownload";

    /// <summary>更新时间列使用的共享尺寸组名。</summary>
    public const string TimeSizeGroup = "CompItemTime";

    /// <summary>来源列使用的共享尺寸组名。</summary>
    public const string SourceSizeGroup = "CompItemSource";

    private bool _showLoaderDesc = true;

    private bool _showMcVersionDesc = true;

    [ObservableProperty] public partial string DescriptionText { get; set; } = "";

    [ObservableProperty] public partial string? DescriptionToolTip { get; set; }

    [ObservableProperty] public partial string DownloadText { get; set; } = "";

    [ObservableProperty] public partial string FavoriteIcon { get; set; } = "lucide/heart";

    /// <summary>容器是否处于虚拟化回收/复用状态；控件在复用前据此重置旧视觉状态。</summary>
    [ObservableProperty]
    public partial bool IsVirtualized { get; set; }

    [ObservableProperty] public partial Comp.CompProject? Project { get; set; }

    [ObservableProperty] public partial bool ShowDownload { get; set; } = true;

    [ObservableProperty] public partial bool ShowDownloadButton { get; set; }

    [ObservableProperty] public partial bool ShowFavoriteButton { get; set; } = true;

    [ObservableProperty] public partial bool ShowSource { get; set; } = true;

    [ObservableProperty] public partial bool ShowTime { get; set; } = true;

    [ObservableProperty] public partial bool ShowVersion { get; set; } = true;

    [ObservableProperty] public partial string SourceText { get; set; } = "";

    [ObservableProperty] public partial string SubTitleText { get; set; } = "";

    [ObservableProperty] public partial bool SubTitleVisible { get; set; }

    [ObservableProperty] public partial string TimeText { get; set; } = "";

    [ObservableProperty] public partial string TitleText { get; set; } = "";

    [ObservableProperty] public partial bool UseSharedSizeGroups { get; set; } = true;

    [ObservableProperty] public partial string VersionText { get; set; } = "";

    /// <summary>条目标签（本地化资源键）。</summary>
    public IReadOnlyList<string> Tags { get; set; } = [];

    public string VersionSharedSizeGroup { get; set; } = VersionSizeGroup;

    public string DownloadSharedSizeGroup { get; set; } = DownloadSizeGroup;

    public string TimeSharedSizeGroup { get; set; } = TimeSizeGroup;

    public string SourceSharedSizeGroup { get; set; } = SourceSizeGroup;

    /// <summary>条目被点击时触发；页面可订阅此事件执行导航。</summary>
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

    /// <summary>用工程数据刷新所有展示字段；语言切换或容器复用时调用。</summary>
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
        var title = project.GetControlTitle(true);
        TitleText = title.Key;
        SubTitleText = title.Value;
        SubTitleVisible = !string.IsNullOrEmpty(title.Value);
        DescriptionText = (project.Description ?? string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);
        Tags = [.. project.Tags];
        SourceText = project.FromCurseForge ? "CurseForge" : "Modrinth";
        ShowVersion = showMcVersionDesc || showLoaderDesc;
        VersionText = project.BuildVersionDescription(showMcVersionDesc, showLoaderDesc);
        ShowTime = project.LastUpdate is not null;
        TimeText = project.LastUpdate is { } lastUpdate
            ? Lang.TimeSpan(lastUpdate - DateTime.Now, 1)
            : string.Empty;
        DownloadText = Lang.CompactNumber(project.DownloadCount);
        ShowDownload = !string.IsNullOrEmpty(DownloadText);
        ShowSource = true;
        ShowFavoriteButton = showFavoriteButton;
        ShowDownloadButton = showQuickDownload;
        IsVirtualized = false;
        RefreshFavoriteStatus();
    }

    /// <summary>根据当前工程刷新收藏图标。</summary>
    [RelayCommand]
    public void RefreshFavoriteStatus()
    {
        if (Project is null)
            return;
        FavoriteIcon = Comp.CompFavorites.IsFavourite(Project.Id)
            ? "lucide/heart-filled"
            : "lucide/heart";
    }

    /// <summary>旧调用点使用的更新入口：按最近一次构造参数重新计算统计数据。</summary>
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

    /// <summary>控件在虚拟化容器复用前调用，清理上一项残留的 tooltip 与回收标记。</summary>
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
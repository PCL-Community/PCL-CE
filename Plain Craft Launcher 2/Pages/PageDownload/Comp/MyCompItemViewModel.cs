using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.ViewModel;
using System.Windows;

namespace PCL;

public partial class MyCompItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _titleText = "";

    [ObservableProperty] private string _subTitleText = "";

    [ObservableProperty] private Visibility _subTitleVisibility = Visibility.Collapsed;

    [ObservableProperty] private string _descriptionText = "";

    /// <summary>收藏按钮当前应显示的图标，随收藏状态变化。</summary>
    [ObservableProperty] private string _favoriteIcon = "lucide/heart";

    /// <summary>该条目对应的资源工程，由列表数据源填充。</summary>
    public Comp.CompProject? Project { get; set; }

    /// <summary>标签列表（本地化资源键）。</summary>
    public List<string>? Tags { get; set; }

    /// <summary>来源显示名，例如 CurseForge、Modrinth。</summary>
    public string SourceText { get; set; } = "";

    /// <summary>是否显示游戏版本 / 加载器描述列。</summary>
    public bool ShowVersion { get; set; } = true;

    /// <summary>是否显示更新时间列。</summary>
    public bool ShowTime { get; set; } = true;

    /// <summary>是否显示收藏按钮。</summary>
    public bool ShowFavoriteButton { get; set; } = true;

    /// <summary>是否显示快速下载按钮（仅搜索结果页）。</summary>
    public bool ShowDownloadButton { get; set; }

    /// <summary>版本描述文本，语言切换后自动刷新。</summary>
    public LocalizedText VersionText { get; set; } = LocalizedText.Create(() => "");

    /// <summary>更新时间文本，语言切换后自动刷新。</summary>
    public LocalizedText TimeText { get; set; } = LocalizedText.Create(() => "");

    /// <summary>下载量文本，语言切换后自动刷新。</summary>
    public LocalizedText DownloadText { get; set; } = LocalizedText.Create(() => "");

    /// <summary>根据当前工程刷新收藏图标。</summary>
    public void RefreshFavoriteStatus()
    {
        if (Project is null)
            return;
        FavoriteIcon = Comp.CompFavorites.IsFavourite(Project.Id) ? "lucide/heart-filled" : "lucide/heart";
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;
using System.Windows;

namespace PCL;

/// <summary>下载页收藏夹列表页的视图模型。</summary>
public partial class PageDownloadCompFavoritesViewModel : ViewModelBase
{
    [ObservableProperty] private string _selectedText = "";

    [ObservableProperty] private int _favTargetSelectedIndex = -1;

    [ObservableProperty] private string _searchText = "";

    [ObservableProperty] private bool _showHintGetFail;

    [ObservableProperty] private bool _showSearchBox = true;

    [ObservableProperty] private bool _showContentList = true;

    [ObservableProperty] private bool _showNoContent = true;

    /// <summary>内容列表底部留白，用于给底部操作栏让位。</summary>
    [ObservableProperty] private Thickness _contentListMargin = new(0d);

    /// <summary>批量下载按钮是否可用，在下载对话框期间置为不可用。</summary>
    [ObservableProperty] private bool _isBulkDownloadEnabled = true;

    public static string GetCategoryTitleFormat(int type) => type switch
    {
        -1 => Lang.Text("Download.Comp.Favorites.SearchResults.Title"),
        (int)Comp.CompType.Mod => Lang.Text("Download.Comp.Favorites.Category.Mod"),
        (int)Comp.CompType.ModPack => $"{Lang.Text("Download.Comp.Type.Modpack")} ({{0}})",
        (int)Comp.CompType.ResourcePack => $"{Lang.Text("Download.Comp.Type.ResourcePack")} ({{0}})",
        (int)Comp.CompType.Shader => $"{Lang.Text("Download.Comp.Type.Shader")} ({{0}})",
        (int)Comp.CompType.DataPack => $"{Lang.Text("Download.Comp.Type.DataPack")} ({{0}})",
        (int)Comp.CompType.Plugin => $"{Lang.Text("Download.Comp.Type.Plugin")} ({{0}})",
        (int)Comp.CompType.World => $"{Lang.Text("Download.Comp.Type.World")} ({{0}})",
        _ => $"{Lang.Text("Download.Comp.Favorites.UnknownType")} ({{0}})"
    };
}

using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;
using System.Collections.ObjectModel;
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

    /// <summary>
    ///     收藏夹分组列表。刷新时整体替换集合，只触发一次集合变更通知。
    /// </summary>
    [ObservableProperty] private ObservableCollection<FavoriteGroupViewModel> _groups = [];

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

/// <summary>
///     收藏夹列表中的一个分组（按工程类型划分），对应界面上的一张 MyCard。
/// </summary>
public sealed partial class FavoriteGroupViewModel : ObservableObject
{
    public FavoriteGroupViewModel(int compType)
    {
        CompType = compType;
        RefreshTitle();
    }

    /// <summary>分组对应的工程类型；-1 表示搜索结果分组。</summary>
    public int CompType { get; }

    /// <summary>分组卡片标题，含当前条目数。</summary>
    [ObservableProperty] private string _title = "";

    /// <summary>分组内的条目；整体替换以只触发一次集合变更通知。</summary>
    [ObservableProperty] private ObservableCollection<FavoriteItemViewModel> _items = [];

    /// <summary>空分组不参与显示。</summary>
    [ObservableProperty] private Visibility _visibility = Visibility.Collapsed;

    /// <summary>整体替换分组条目，并同步标题与可见性。</summary>
    public void SetItems(IReadOnlyList<FavoriteItemViewModel> items)
    {
        Items = new ObservableCollection<FavoriteItemViewModel>(items);
        Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshTitle();
    }

    /// <summary>按当前条目数与语言重新计算标题。</summary>
    public void RefreshTitle() =>
        Title = string.Format(PageDownloadCompFavoritesViewModel.GetCategoryTitleFormat(CompType), Items.Count);
}

/// <summary>
///     收藏夹列表中的一个条目。界面上的 MyListItem 由 DataTemplate 生成，
///     其中无法绑定的部分（备注 Run、编辑/删除按钮、右键菜单）在 code-behind 里做一次性映射。
/// </summary>
public sealed partial class FavoriteItemViewModel : ObservableObject
{
    public FavoriteItemViewModel(Comp.CompProject project, string? note)
    {
        Project = project;
        SetNote(note);
    }

    public Comp.CompProject Project { get; }

    /// <summary>工程类型，与 <see cref="FavoriteGroupViewModel.CompType" /> 对应。</summary>
    public int Type => (int)Project.Type;

    public string Title => Project.TranslatedName;

    public string Info => Project.Description.Replace("\r", "").Replace("\n", "");

    public string Logo => string.IsNullOrEmpty(Project.LogoUrl)
        ? $"{LauncherPaths.ImageRoot}Icons/NoIcon.png"
        : Project.LogoUrl;

    public object Tags => Project.Tags;

    /// <summary>备注原文（不含括号与空格），用于编辑对话框的默认值。</summary>
    public string Note { get; private set; } = "";

    /// <summary>渲染在标题后的备注文本；空备注时为空字符串。</summary>
    [ObservableProperty] private string _noteText = "";

    /// <summary>是否被勾选，双向绑定到 MyListItem.Checked。</summary>
    [ObservableProperty] private bool _isChecked;

    public void SetNote(string? note)
    {
        Note = note ?? "";
        NoteText = string.IsNullOrWhiteSpace(Note) ? "" : $" ({Note})";
    }
}

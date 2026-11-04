using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;
using System.Collections.ObjectModel;
using System.Windows;

namespace PCL;

/// <summary>资源详情页的视图模型。</summary>
public partial class PageDownloadCompDetailViewModel : ViewModelBase
{
    [ObservableProperty] private string _introWebText = "CurseForge";

    [ObservableProperty] private Visibility _introWikiVisibility = Visibility.Visible;

    [ObservableProperty] private Visibility _translateVisibility = Visibility.Visible;

    [ObservableProperty] private Visibility _filterVisibility = Visibility.Visible;

    [ObservableProperty] private bool _isFavorite;

    /// <summary>文件列表的版本分组，整体替换以只触发一次集合变更通知。</summary>
    [ObservableProperty] private ObservableCollection<CompFileGroupViewModel> _fileGroups = [];

    public ObservableCollection<CompFilterChip> InstanceFilterLabelChips { get; } = [];

    public ObservableCollection<CompFilterChip> ModLoaderFilterLabelChips { get; } = [];

    public ObservableCollection<CompFilterOption> InstanceFilterOptions { get; } = [];

    public ObservableCollection<CompFilterOption> ModLoaderFilterOptions { get; } = [];

    public Action? FilterSelectionChanged { get; set; }

    public CompFilterOption? SelectedInstanceFilter => InstanceFilterOptions.FirstOrDefault(option => option.IsSelected);

    public CompFilterOption? SelectedModLoaderFilter => ModLoaderFilterOptions.FirstOrDefault(option => option.IsSelected);

    public string? InstanceFilterValue => SelectedInstanceFilter?.Text == Lang.Text("Common.Option.All")
        ? null
        : SelectedInstanceFilter?.Text;

    public string? ModLoaderFilterValue => SelectedModLoaderFilter?.Text == Lang.Text("Common.Option.All")
        ? null
        : SelectedModLoaderFilter?.Text;

    public void SetInstanceFilter(CompFilterOption? option)
    {
        foreach (var item in InstanceFilterOptions)
            item.IsSelected = ReferenceEquals(item, option);
        OnPropertyChanged(nameof(SelectedInstanceFilter));
    }

    public void SetModLoaderFilter(CompFilterOption? option)
    {
        foreach (var item in ModLoaderFilterOptions)
            item.IsSelected = ReferenceEquals(item, option);
        OnPropertyChanged(nameof(SelectedModLoaderFilter));
    }

    [RelayCommand]
    private void SelectInstanceFilter(CompFilterOption? option)
    {
        SetInstanceFilter(option);
        FilterSelectionChanged?.Invoke();
    }

    [RelayCommand]
    private void SelectModLoaderFilter(CompFilterOption? option)
    {
        SetModLoaderFilter(option);
        FilterSelectionChanged?.Invoke();
    }

    public void ApplyProjectState(Comp.CompProject project)
    {
        IntroWebText = project.FromCurseForge ? "CurseForge" : "Modrinth";
        IntroWikiVisibility = Lang.IsChineseMainland && project.WikiId != 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        TranslateVisibility = Lang.IsChineseMainland
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public void SetFilterVisibility(bool visible)
    {
        FilterVisibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public void RefreshFavorite(string projectId)
    {
        IsFavorite = Comp.CompFavorites.IsFavourite(projectId);
    }

    /// <summary>
    /// 筛选类型相同的结果（Modrinth 会返回 Mod、服务端插件、数据包混合的列表）
    /// </summary>
    /// <param name="results"></param>
    /// <param name="pageType"></param>
    /// <returns></returns>
    public static List<Comp.CompFile> FilterResults(List<Comp.CompFile> results, Comp.CompType pageType)
    {
        if (pageType == Comp.CompType.Any)
        {
            results = results.Where(r => r.Type != Comp.CompType.Plugin).ToList();
        }
        else if (pageType is Comp.CompType.Shader or Comp.CompType.ResourcePack)
        {
        }
        // 不筛选光影和资源包，否则原版光影会因为是资源包格式而被过滤（Meloong-Git/#6473）
        else
        {
            results = results.Where(r => r.Type == pageType).ToList();
        }

        return results;
    }

    public sealed class CardSorter(string topmost = "") : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            var sx = x ?? "";
            var sy = y ?? "";
            // 相同
            if (sx == sy)
                return 0;
            // 置顶
            if (sx == topmost)
                return -1;
            if (sy == topmost)
                return 1;
            // 特殊版本
            var isXSpecial = !sx.Contains('.');
            var isYSpecial = !sy.Contains('.');
            if (isXSpecial && isYSpecial)
                return string.Compare(sx, sy, StringComparison.Ordinal);
            if (isXSpecial)
                return 1;
            if (isYSpecial)
                return -1;
            // 比较版本号
            var versionCodeSort = -McVersionComparer.CompareVersion(sx.Replace(sx.BeforeFirst(" ") + " ", ""),
                sy.Replace(sy.BeforeFirst(" ") + " ", ""));
            if (versionCodeSort != 0)
                return versionCodeSort;
            // 比较全部
            return -McVersionComparer.CompareVersion(sx, sy);
        }
    }
}

/// <summary>资源详情页筛选器中的单个选项。</summary>
public sealed partial class CompFilterOption(string text) : ObservableObject
{
    public string Text { get; } = text;

    [ObservableProperty] private bool _isSelected;
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>
///     游戏下载 - 版本列表页的视图模型：承载按版本分类分组的列表数据。<br />
///     界面上的分类卡片与版本条目全部由 DataTemplate 生成，code-behind 只负责把加载结果换算成这里的数据。
/// </summary>
public partial class PageDownloadClientViewModel : ViewModelBase
{
    /// <summary>版本分类的展示顺序，与改造前依次添加卡片的顺序一致。</summary>
    public static readonly IReadOnlyList<McVersionCategory> CategoryOrder =
    [
        McVersionCategory.Release,
        McVersionCategory.Snapshot,
        McVersionCategory.BeforeRelease,
        McVersionCategory.AprilFools
    ];

    /// <summary>版本分组列表。刷新时整体替换集合，只触发一次集合变更通知。</summary>
    [ObservableProperty] private ObservableCollection<GameVersionGroupViewModel> _groups = [];

    /// <summary>
    ///     把加载结果按分类拆分，并在每个分类内按发布时间倒序排列。<br />
    ///     返回值按 <see cref="CategoryOrder" /> 的顺序插入，可直接用于遍历。
    /// </summary>
    public static Dictionary<McVersionCategory, List<JsonObject>> ClassifyVersions(JsonArray versions)
    {
        var dict = CategoryOrder.ToDictionary(category => category, _ => new List<JsonObject>());
        foreach (var version in versions.OfType<JsonObject>())
            dict[McVersionClassifier.ClassifyVersion(version)].Add(version);
        foreach (var category in CategoryOrder)
            dict[category] = dict[category]
                .OrderByDescending(McVersionClassifier.GetReleaseTime)
                .ToList();
        return dict;
    }

    /// <summary>
    ///     用分类结果整体替换现有分组；空分类不生成卡片。
    /// </summary>
    public void SetGroups(IReadOnlyDictionary<McVersionCategory, List<JsonObject>> classified)
    {
        var groups = new List<GameVersionGroupViewModel>();
        foreach (var category in CategoryOrder)
            if (classified.TryGetValue(category, out var versions) && versions.Count > 0)
                groups.Add(new GameVersionGroupViewModel(category, versions));
        Groups = new ObservableCollection<GameVersionGroupViewModel>(groups);
    }
}

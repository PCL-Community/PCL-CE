using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;

namespace PCL;

/// <summary>
///     版本列表中的一个分组（一个版本分类），对应界面上的一张可折叠 <see cref="MyCard" />。
/// </summary>
public sealed partial class GameVersionGroupViewModel : ObservableObject
{
    /// <param name="category">该分组对应的版本分类。</param>
    /// <param name="versions">已按发布时间倒序排列的版本数据。</param>
    public GameVersionGroupViewModel(McVersionCategory category, IReadOnlyList<JsonObject> versions)
    {
        Category = category;
        Title = LocalizedText.Create(
            () => $"{Lang.Text(McVersionClassifier.GetCategoryKey(category))} ({versions.Count})");
        foreach (var version in versions)
            Items.Add(GameVersionItemViewModel.FromEntry(version));
    }

    /// <summary>该分组对应的版本分类。</summary>
    public McVersionCategory Category { get; }

    /// <summary>卡片标题（分类名 + 条目数）；随语言切换自动刷新，在 XAML 中绑定 <c>Title.Value</c>。</summary>
    public LocalizedText Title { get; }

    /// <summary>分组内的版本，由 DataTemplate 生成列表项。</summary>
    public ObservableCollection<GameVersionItemViewModel> Items { get; } = [];

    /// <summary>
    ///     分组是否展开。改造前所有分类卡片默认折叠（<c>MyCard.IsSwapped = true</c>），因此默认值为
    ///     <see langword="false" />；XAML 中与 <c>IsSwapped</c> 双向绑定。
    /// </summary>
    [ObservableProperty] private bool _isExpanded;
}

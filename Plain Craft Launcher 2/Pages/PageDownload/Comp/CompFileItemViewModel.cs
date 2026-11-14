using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PCL;

/// <summary>资源详情页文件列表中的一个文件条目。</summary>
/// <remarks>
///     这里没有直接暴露 Title/Info/Logo 等可绑定属性，而是保留 <see cref="CompFileUiExtensions.ToListItem" />
///     产出的惰性元素，原因有两个：
///     1. 该元素的 Info 文案依赖 <c>LocalizedText.TryBind</c> 才能在语言切换时刷新，纯 XAML 绑定无法复刻；
///     2. 该元素是 <see cref="MyVirtualizingElement{T}" />，只有在可见时才真正创建 MyListItem，避免一次构建上千个控件。
///     因此由 DataTemplate 里的宿主 StackPanel 在 Loaded 时把它挂上去（一次性代码映射）。
/// </remarks>
public sealed class CompFileItemViewModel
{
    public CompFileItemViewModel(Comp.CompFile file, MyListItem.ClickEventHandler onClick,
        MyIconButton.ClickEventHandler? onSaveClick, bool badDisplayName)
    {
        File = file;
        Element = file.ToListItem(onClick, onSaveClick, badDisplayName);
    }

    public Comp.CompFile File { get; }

    /// <summary>惰性创建的文件条目元素，父级必须是一个 Panel。</summary>
    public MyVirtualizingElement<MyListItem> Element { get; }
}

/// <summary>资源详情页中的一个版本分组，对应界面上的一个 MyCard。</summary>
public sealed partial class CompFileGroupViewModel : ObservableObject
{
    public CompFileGroupViewModel(string title, List<Comp.CompFile> files)
    {
        Title = title;
        Files = files;
    }

    /// <summary>卡片标题，即版本分组的名称。</summary>
    public string Title { get; }

    /// <summary>分组内的文件，已按发布时间倒序排列（供依赖栏使用）。</summary>
    public List<Comp.CompFile> Files { get; }

    /// <summary>卡片是否处于折叠状态。</summary>
    [ObservableProperty] private bool _isSwapped;

    /// <summary>是否显示「版本识别可能延迟」提示，仅 Other 分组可见。</summary>
    [ObservableProperty] private Visibility _recognitionHintVisibility = Visibility.Collapsed;

    public ObservableCollection<CompFileItemViewModel> Items { get; } = [];

    /// <summary>
    ///     构建一个版本分组，并把「普通资源 / 整合包 / 世界」三种点击行为合并到这里。
    /// </summary>
    public static CompFileGroupViewModel Create(string title, List<Comp.CompFile> files, Comp.CompType type,
        bool isSwapped, bool showRecognitionHint,
        MyListItem.ClickEventHandler installClick, MyListItem.ClickEventHandler worldClick,
        MyIconButton.ClickEventHandler saveClick)
    {
        files.Sort((a, b) => b.ReleaseDate.CompareTo(a.ReleaseDate));
        var distinctCount = files.Select(f => f.DisplayName).Distinct().Count();
        var badDisplayName = distinctCount != files.Count;

        var group = new CompFileGroupViewModel(title, files)
        {
            IsSwapped = isSwapped,
            RecognitionHintVisibility = showRecognitionHint ? Visibility.Visible : Visibility.Collapsed
        };

        foreach (var file in files)
            group.Items.Add(type switch
            {
                Comp.CompType.ModPack => new CompFileItemViewModel(file, installClick, saveClick, badDisplayName),
                Comp.CompType.World => new CompFileItemViewModel(file, worldClick, saveClick, badDisplayName),
                // 普通资源：点击整行即为「另存为」，且不显示单独的保存按钮（与改造前一致）
                _ => new CompFileItemViewModel(file, (sender, e) => saveClick(sender, e), null, badDisplayName)
            });

        return group;
    }
}

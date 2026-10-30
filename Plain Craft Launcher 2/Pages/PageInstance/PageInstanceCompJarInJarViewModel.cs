using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>内嵌模组（Jar-in-Jar）页的视图模型。</summary>
public partial class PageInstanceCompJarInJarViewModel : ViewModelBase
{
    public ObservableCollection<JijNoticeRow> WarningRows { get; } = [];

    public ObservableCollection<JijConflictRow> ConflictRows { get; } = [];

    [ObservableProperty] private Visibility _panLoadVisibility = Visibility.Visible;

    [ObservableProperty] private Visibility _cardWarningsVisibility = Visibility.Visible;

    [ObservableProperty] private Visibility _cardConflictsVisibility = Visibility.Visible;

    [ObservableProperty] private Visibility _sectionRelationsVisibility = Visibility.Visible;

    [ObservableProperty] private Visibility _sectionBundledVisibility = Visibility.Visible;

    [ObservableProperty] private Visibility _panEmptyVisibility = Visibility.Collapsed;

    public void HideLoading()
    {
        PanLoadVisibility = Visibility.Collapsed;
    }

    public void ApplySectionVisibility(bool hasWarning, int conflictCount, int relationCount, int bundledCount,
        bool hasSearch)
    {
        CardWarningsVisibility = hasWarning ? Visibility.Visible : Visibility.Collapsed;
        CardConflictsVisibility = conflictCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        SectionRelationsVisibility = relationCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        SectionBundledVisibility = bundledCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        PanEmptyVisibility = hasWarning || conflictCount > 0 || relationCount > 0 || bundledCount > 0 || hasSearch
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}

/// <summary>内嵌模组页的提示行。</summary>
public sealed class JijNoticeRow
{
    public required string Text { get; init; }

    public required Brush Foreground { get; init; }
}

/// <summary>内嵌模组页的冲突行。</summary>
public sealed class JijConflictRow
{
    public required string Severity { get; init; }

    public required Brush SeverityBrush { get; init; }

    public required string Pair { get; init; }
}

/// <summary>内嵌模组页的内嵌模组树行。</summary>
public sealed class JijTreeRow
{
    public required Thickness Margin { get; init; }

    public required string Name { get; init; }

    public string Version { get; init; } = "";

    public string Loader { get; init; } = "";

    public string McVersion { get; init; } = "";

    public string Versions { get; init; } = "";

    public string? VersionsTooltip { get; init; }

    public string Status { get; init; } = "";

    public Brush? StatusBrush { get; init; }
}

/// <summary>内嵌模组页的依赖行。</summary>
public sealed class JijDependencyRow
{
    public required string Primary { get; init; }

    public string Optional { get; init; } = "";

    public string GrayLabel { get; init; } = "";

    public string ColorLabel { get; init; } = "";

    public Brush? LabelBrush { get; init; }
}

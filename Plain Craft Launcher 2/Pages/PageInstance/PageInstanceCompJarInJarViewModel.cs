using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>内嵌模组（Jar-in-Jar）页的视图模型。</summary>
public partial class PageInstanceCompJarInJarViewModel : ViewModelBase
{
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

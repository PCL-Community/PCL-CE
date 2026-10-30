using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>实例截图页的视图模型。</summary>
public partial class PageInstanceScreenshotViewModel : ViewModelBase
{
    [ObservableProperty] private bool _showContent = true;

    public void RefreshTip(int fileCount)
    {
        ShowContent = fileCount != 0;
    }
}

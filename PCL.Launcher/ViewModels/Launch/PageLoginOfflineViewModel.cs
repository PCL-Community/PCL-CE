using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>离线档案创建页的输入状态。</summary>
public partial class PageLoginOfflineViewModel : ViewModelBase
{
    [ObservableProperty] private string _userName = string.Empty;

    [ObservableProperty] private string _uuid = string.Empty;
}
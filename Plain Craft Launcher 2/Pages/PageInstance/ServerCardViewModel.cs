using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>服务器卡片的视图模型。</summary>
public partial class ServerCardViewModel : ViewModelBase
{
    [ObservableProperty] private string _nameText = "";

    [ObservableProperty] private string _playerText = "";

    [ObservableProperty] private string _motdText = "";

    [ObservableProperty] private Visibility _motdVisibility = Visibility.Visible;
}

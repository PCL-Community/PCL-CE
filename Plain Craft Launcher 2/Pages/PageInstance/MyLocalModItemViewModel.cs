using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>实例资源列表条目的视图模型。</summary>
public partial class MyLocalModItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _titleText = "";

    [ObservableProperty] private string _subTitleText = "";

    [ObservableProperty] private Visibility _subTitleVisibility = Visibility.Collapsed;

    [ObservableProperty] private string _descriptionText = "";

    [ObservableProperty] private Visibility _updateVisibility = Visibility.Collapsed;

    [ObservableProperty] private string? _updateToolTip;
}

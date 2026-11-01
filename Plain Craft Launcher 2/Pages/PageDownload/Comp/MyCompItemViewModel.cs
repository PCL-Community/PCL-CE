using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>资源列表条目的视图模型。</summary>
public partial class MyCompItemViewModel : ViewModelBase
{
[ObservableProperty] private string _titleText = "";

[ObservableProperty] private string _subTitleText = "";

[ObservableProperty] private Visibility _subTitleVisibility = Visibility.Collapsed;

[ObservableProperty] private string _descriptionText = "";
}

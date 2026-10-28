using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>实例资源管理页（模组、资源包、光影、投影原理图）的视图模型。</summary>
public partial class PageInstanceCompResourceViewModel : ViewModelBase
{
    public enum SortMethod
    {
        FileName,
        CompName,
        TagNums,
        CreateTime,
        ModFileSize
    }

    [ObservableProperty] private string _filterAllText = "";
    [ObservableProperty] private string _filterEnabledText = "";
    [ObservableProperty] private string _filterDisabledText = "";
    [ObservableProperty] private string _filterCanUpdateText = "";
    [ObservableProperty] private string _filterErrorText = "";
    [ObservableProperty] private string _filterDuplicateText = "";
    [ObservableProperty] private bool _showFilterEnabled = true;
    [ObservableProperty] private bool _showFilterDisabled = true;
    [ObservableProperty] private bool _showFilterCanUpdate = true;
    [ObservableProperty] private bool _showFilterError = true;
    [ObservableProperty] private bool _showFilterDuplicate = true;
    [ObservableProperty] private bool _showManageBack;
    [ObservableProperty] private string _sortText = "";

    [ObservableProperty] private bool _showList = true;
    [ObservableProperty] private bool _showEmpty = true;
    [ObservableProperty] private bool _showSchematicEmpty;
    [ObservableProperty] private string _emptyTitle = "";
    [ObservableProperty] private string _emptyDescription = "";
    [ObservableProperty] private bool _showHintBack;

    [ObservableProperty] private string _selectedText = "";
    [ObservableProperty] private bool _canEnable = true;
    [ObservableProperty] private bool _canDisable = true;
    [ObservableProperty] private bool _canUpdate = true;
    [ObservableProperty] private bool _canFavoriteAndShare = true;
    [ObservableProperty] private bool _showSelectUpdate = true;
    [ObservableProperty] private bool _showSelectEnable = true;
    [ObservableProperty] private bool _showSelectDisable = true;
    [ObservableProperty] private bool _showSelectFavorites = true;
    [ObservableProperty] private bool _showSelectShare = true;

    [ObservableProperty] private bool _showManageDownload = true;
    [ObservableProperty] private bool _showHintDownload = true;
    [ObservableProperty] private bool _showManageJarInJar = true;

    public static string GetSortName(SortMethod method)
    {
        switch (method)
        {
            case SortMethod.FileName:
            {
                return Lang.Text("Instance.Resource.Sort.FileName");
            }
            case SortMethod.CompName:
            {
                return Lang.Text("Instance.Resource.Sort.ResourceName");
            }
            case SortMethod.TagNums:
            {
                return Lang.Text("Instance.Resource.Sort.TagCount");
            }
            case SortMethod.CreateTime:
            {
                return Lang.Text("Instance.Resource.Sort.AddTime");
            }
            case SortMethod.ModFileSize:
            {
                return Lang.Text("Instance.Resource.Sort.FileSize");
            }

            default:
            {
                return Lang.Text("Instance.Resource.Sort.ResourceName");
            }
        }
    }

    public void ShowListPanel()
    {
        ShowList = true;
        ShowEmpty = false;
        ShowSchematicEmpty = false;
    }

    public void ShowSchematicEmptyPanel()
    {
        ShowList = false;
        ShowEmpty = false;
        ShowSchematicEmpty = true;
    }

    public void ShowEmptyPanel()
    {
        ShowList = false;
        ShowEmpty = true;
        ShowSchematicEmpty = false;
    }
}

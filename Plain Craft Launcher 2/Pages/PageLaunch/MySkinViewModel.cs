using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>皮肤预览控件的视图模型。</summary>
public partial class MySkinViewModel : ViewModelBase
{
    [ObservableProperty] private string _address = "";

    [ObservableProperty] private bool _canSave = true;

    [ObservableProperty] private Visibility _capeVisibility = Visibility.Visible;

    [ObservableProperty] private string? _toolTipText;

    partial void OnAddressChanged(string value)
    {
        ToolTipText = string.IsNullOrEmpty(value)
            ? Lang.Text("Common.State.Loading")
            : Lang.Text("Launch.Skin.Change.ToolTip");
    }

    public static string GetCapeDisplayName(string capeAlias)
    {
        var safeName = capeAlias.Replace("-", "").Replace(" ", "").Replace("'", "");
        var key = $"Launch.Skin.Cape.Name.{safeName}";
        var name = Lang.Text(key);
        return string.IsNullOrEmpty(name) || name == $"!{key}!" || name == key ? capeAlias : name;
    }
}

using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>第三方账号（Authlib-Injector）登录页面的视图模型。</summary>
public partial class PageLoginAuthViewModel : ViewModelBase
{
    [ObservableProperty] private bool _controlsEnabled = true;

    [ObservableProperty] private string _loginButtonText = Lang.Text("Launch.Account.Auth.Login");

    [ObservableProperty] private string _registerLinkContent = Lang.Text("Launch.Account.Auth.Register");

    [ObservableProperty] private Visibility _useOAuthVisibility = Visibility.Visible;
}

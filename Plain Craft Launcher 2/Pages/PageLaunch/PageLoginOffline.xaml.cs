using System;
using System.Windows.Input;

namespace PCL;

public partial class PageLoginOffline : IPageView
{
    private readonly PageLoginOfflineViewModel _vm = new();

    public PageLoginOffline()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    public void OnNavigatedTo(object? parameter) => Refresh();

    public void OnNavigatedFrom()
    {
    }

    private void Refresh()
    {
        _vm.ChangeUuidModeCommand.Execute(RadioUuidCustom.Checked
            ? PageLoginOfflineViewModel.UuidMode.Custom
            : RadioUuidLegacy.Checked
                ? PageLoginOfflineViewModel.UuidMode.Legacy
                : PageLoginOfflineViewModel.UuidMode.Standard);
    }

    private void BtnBack_Click(object sender, MouseButtonEventArgs e) => _vm.BackCommand.Execute(null);

    private void RadioUuid_Checked(object sender, Common.RouteEventArgs e) => Refresh();

    private void BtnLogin_Click(object sender, MouseButtonEventArgs e) => _vm.LoginCommand.Execute(null);
}

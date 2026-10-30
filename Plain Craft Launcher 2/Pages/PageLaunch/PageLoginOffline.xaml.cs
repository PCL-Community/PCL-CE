using System;

namespace PCL;

public partial class PageLoginOffline : IPageView
{
    private readonly PageLoginOfflineViewModel _vm = new();

    public PageLoginOffline()
    {
        InitializeComponent();
        DataContext = _vm;
        BtnBack.Click += BtnBack_Click;
        RadioUuidCustom.Check += RadioUuid_Checked;
        RadioUuidStandard.Check += RadioUuid_Checked;
        RadioUuidLegacy.Check += RadioUuid_Checked;
        BtnLogin.Click += BtnLogin_Click;
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

    private void BtnBack_Click(object sender, EventArgs e) => _vm.BackCommand.Execute(null);

    private void RadioUuid_Checked(object sender, Common.RouteEventArgs e) => Refresh();

    private void BtnLogin_Click(object sender, EventArgs e) => _vm.LoginCommand.Execute(null);
}

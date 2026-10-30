using System;

namespace PCL;

public partial class PageLoginMs : IPageView
{
    private readonly PageLoginMsViewModel _vm = new();

    public PageLoginMs()
    {
        InitializeComponent();
        DataContext = _vm;
        BtnBack.Click += BtnBack_Click;
        BtnLogin.Click += BtnLogin_Click;
    }

    public void OnNavigatedTo(object? parameter) => _vm.ResetLoginState();

    public void OnNavigatedFrom()
    {
    }

    private void BtnBack_Click(object sender, EventArgs e) => _vm.BackCommand.Execute(null);

    private void BtnLogin_Click(object sender, EventArgs e) => _vm.LoginCommand.Execute(null);
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupGameLink : IPageView
{
    private readonly PageSetupGameLinkViewModel _vm = new();
    private bool _refreshed;

    public PageSetupGameLink()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += PageSetupLink_Loaded;
        Unloaded += PageSetupLink_Unloaded;
    }

    public PageSetupGameLinkViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter) => Refresh();

    public void OnNavigatedFrom()
    {
    }

    private void PageSetupLink_Loaded(object sender, RoutedEventArgs e) => Refresh();

    private void PageSetupLink_Unloaded(object sender, RoutedEventArgs e) => _refreshed = false;

    private void Refresh()
    {
        PanBack.ScrollToHome();
        if (_refreshed)
            return;
        _refreshed = true;
        ModAnimation.AniBeginControl();
        Reload();
        ModAnimation.AniEndControl();
    }

    public void Reload() => _vm.Reload();

    public void Reset() => _vm.Reset();

    private void TextBoxChange(object senderRaw, TextChangedEventArgs e)
    {
        var sender = (MyTextBox)senderRaw;
        if (!ModAnimation.AniExitsAnyControl())
            _vm.SetConfigByTag(sender.Tag?.ToString(), sender.Text);
    }

    private void ComboBoxChange(MyComboBox sender, object e)
    {
        if (!ModAnimation.AniExitsAnyControl())
            _vm.SetConfigByTag(sender.Tag?.ToString(), sender.SelectedIndex);
    }

    private void CheckBoxChange(object senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw;
        if (!ModAnimation.AniExitsAnyControl())
            _vm.SetConfigByTag(sender.Tag?.ToString(), sender.Checked);
    }

    private void LinkProtocolPerferenceChange(object sender, SelectionChangedEventArgs e)
    {
        if (!ModAnimation.AniExitsAnyControl())
            _vm.SetProtocolPreference(((MyComboBox)sender).SelectedIndex);
    }

    private async void BtnNetTest_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            await _vm.RunNetworkTestAsync();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex,
                "[Link] 获取网络测试结果失败",
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Setup.GameLink.Error.NetworkTestFailed"));
        }
    }
}

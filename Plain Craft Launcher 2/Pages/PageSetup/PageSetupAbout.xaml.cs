using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;

namespace PCL;

public partial class PageSetupAbout : IPageView
{
    private readonly PageSetupAboutViewModel _vm = new();

    public PageSetupAbout()
    {
        InitializeComponent();
        Loaded += PageAbout_Loaded;
    }

    public PageSetupAboutViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter) => Refresh();

    public void OnNavigatedFrom()
    {
    }

    private void PageAbout_Loaded(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        PanBack.ScrollToHome();
        _vm.Initialize();
        if (!_vm.ShowMcmod) RowMcmod.Height = new GridLength(0);
    }

    private void ImgPCLCommunity_Click(object sender, MouseButtonEventArgs e)
    {
        _vm.RotateCommunityLogoCommand.Execute(sender);
    }

    private void ImgPCLLogo_Click(object sender, MouseButtonEventArgs e)
    {
        _vm.LogoClickCommand.Execute(sender);
    }

    public class GitHubContributor
    {
        [JsonPropertyName("login")] public string Login { get; set; }

        [JsonPropertyName("avatar_url")] public string AvatarUrl { get; set; }

        [JsonPropertyName("html_url")] public string HtmlUrl { get; set; }

        [JsonPropertyName("contributions")] public int Contributions { get; set; }
    }
}

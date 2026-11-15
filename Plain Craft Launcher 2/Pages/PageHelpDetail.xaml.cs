using System.ComponentModel;
using System.Windows;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;

namespace PCL;

public partial class PageHelpDetail : IRefreshable, IPageView
{
    internal sealed record HelpContent(string SourcePath, string Title, string Xaml);

    private readonly PageHelpDetailViewModel _vm;

    private bool _refreshed;

    public string Title => _vm.Title;

    internal PageHelpDetail(HelpContent content)
    {
        InitializeComponent();
        _vm = new PageHelpDetailViewModel(content);
        DataContext = _vm;
        _vm.PropertyChanged += VmOnPropertyChanged;
        Loaded += (_, _) => RefreshOnNavigate();
        Unloaded += (_, _) => _refreshed = false;
        ApplyContent(content);
    }

    internal static HelpContent LoadContent(string jsonPath) => PageHelpDetailViewModel.LoadContent(jsonPath);

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedFrom()
    {
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        PanBack.ScrollToHome();
    }

    private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PageHelpDetailViewModel.Content))
            return;
        var content = _vm.Content;
        if (content is not null)
            ApplyContent(content);
    }

    private void ApplyContent(HelpContent content)
    {
        // 修改时应同时修改 PageLaunchRight.LoadContent。
        var xaml = Launcher.ArgumentReplace(content.Xaml);
        while (xaml.Contains("xmlns"))
            xaml = xaml.RegexReplace("xmlns[^\"']*(\"|')[^\"']*(\"|')", "").Replace("xmlns", "");
        xaml =
            $"<StackPanel xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:local=\"clr-namespace:PCL;assembly=Plain Craft Launcher 2\">{xaml}</StackPanel>";

        var element = (UIElement)Common.GetObjectFromXml(xaml, out var sanitizeResult);
        foreach (var unsupported in sanitizeResult.UnsupportedTypesFound)
            HintService.Show(Hints.Error(Lang.Text("Event.Sanitize.UnsupportedTypeHint", unsupported)));
        foreach (var unknown in sanitizeResult.UnrecognizedTypes)
            HintService.Show(Hints.Error(Lang.Text("Event.Sanitize.UnknownTypeHint", unknown)));

        PanCustom.Children.Clear();
        PanCustom.Children.Add(element);
        if (Launcher.frmMain?.pageCurrent.page == FormMain.PageType.HelpDetail)
            Launcher.frmMain.PageNameRefresh();
    }

    public void Refresh() => _vm.RefreshCommand.Execute(null);
}

using System.ComponentModel;
using System.Windows;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageHelpDetail : IRefreshable
{
    internal sealed record HelpContent(string SourcePath, string Title, string Xaml);

    private readonly PageHelpDetailViewModel _vm;

    public string Title => _vm.Title;

    internal PageHelpDetail(HelpContent content)
    {
        InitializeComponent();
        _vm = new PageHelpDetailViewModel(content);
        DataContext = _vm;
        _vm.PropertyChanged += VmOnPropertyChanged;
        Loaded += (_, _) => PanBack.ScrollToHome();
        ApplyContent(content);
    }

    internal static HelpContent LoadContent(string jsonPath) => PageHelpDetailViewModel.LoadContent(jsonPath);

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
            HintService.Hint(Lang.Text("Event.Sanitize.UnsupportedTypeHint", unsupported), HintType.Error);
        foreach (var unknown in sanitizeResult.UnrecognizedTypes)
            HintService.Hint(Lang.Text("Event.Sanitize.UnknownTypeHint", unknown), HintType.Error);

        PanCustom.Children.Clear();
        PanCustom.Children.Add(element);
        if (Launcher.frmMain?.pageCurrent.page == FormMain.PageType.HelpDetail)
            Launcher.frmMain.PageNameRefresh();
    }

    public void Refresh() => _vm.RefreshCommand.Execute(null);
}

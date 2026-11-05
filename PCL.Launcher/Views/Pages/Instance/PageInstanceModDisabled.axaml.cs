using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Localization;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>实例不可安装 Mod 时的提示页：引导用户下载可安装 Mod 的实例或重新选择实例。</summary>
public class PageInstanceModDisabled : MyPageRight, IPageView
{
    private MyButton _btnDownload = null!;

    public PageInstanceModDisabled()
    {
        DataContext = ViewModel;
        BuildUi();
    }

    public PageInstanceModDisabledViewModel ViewModel { get; } = new();

    public override void OnNavigatedTo(object? parameter)
    {
        RefreshDownloadButton();
    }

    public override void OnNavigatedFrom()
    {
    }

    private void BuildUi()
    {
        var card = new MyCard
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(40d)
        };
        var title = new TextBlock
        {
            Text = Lang.Text("Instance.Resource.Mod.Disabled.Title"),
            FontSize = 19d,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0d, 0d, 0d, 9d)
        };
        ContainerTheme.BindDynamic(title, TextBlock.ForegroundProperty, "ColorBrush3");
        var divider = new Rectangle
        {
            Height = 2d,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ContainerTheme.BindDynamic(divider, Shape.FillProperty, "ColorBrush3");
        var message = new TextBlock
        {
            Text = Lang.Text("Instance.Resource.Mod.Disabled.Message"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10d, 15d, 10d, 5d)
        };
        _btnDownload = new MyButton
        {
            Text = Lang.Text("Instance.Resource.Mod.Disabled.GoDownload"),
            Height = 35d,
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            ColorType = MyButton.ColorState.Highlight
        };
        _btnDownload.Bind(
            IsVisibleProperty,
            new Binding(nameof(PageInstanceModDisabledViewModel.IsDownloadVisible)));
        _btnDownload.Click += (_, _) => PageInstanceModDisabledViewModel.GoDownload();
        var btnVersion = new MyButton
        {
            Text = Lang.Text("Instance.Resource.Mod.Disabled.SelectInstance"),
            Height = 35d,
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d)
        };
        btnVersion.Click += (_, _) => PageInstanceModDisabledViewModel.SelectInstance();
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 20d,
            Margin = new Thickness(10d, 10d, 10d, 0d)
        };
        buttons.Children.Add(_btnDownload);
        buttons.Children.Add(btnVersion);
        var grid = new Grid
        {
            Margin = new Thickness(20d, 17d),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto")
        };
        grid.Children.Add(title);
        Grid.SetRow(divider, 1);
        grid.Children.Add(divider);
        Grid.SetRow(message, 2);
        grid.Children.Add(message);
        Grid.SetRow(buttons, 3);
        grid.Children.Add(buttons);
        card.BorderChild = grid;
        Child = card;
        RefreshDownloadButton();
    }

    protected override void RefreshLocalization()
    {
        BuildUi();
    }

    private void RefreshDownloadButton()
    {
        ViewModel.Refresh();
    }

    public void RefreshModDisabled()
    {
        RefreshDownloadButton();
    }
}
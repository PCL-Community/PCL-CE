using System.Windows;
using System.Windows.Input;
using PCL.Core.IO.Net.Http.Client;

namespace PCL;

public class PageHomepageMarket : IRefreshable
{
    public PageHomepageMarket()
    {
        this.Loaded += Page_Loaded;
    }

    public void Refresh()
    {
        this.Dispatcher.BeginInvoke(new Func<Task>(() => RefreshAsync()));
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        InitLoading();
    }

    private void InitLoading()
    {
        this.Load.Text = "正在加载主页市场";
        this.Load.TextError = "加载失败，点击重试";
        this.Load.State.LoadingState = MyLoading.MyLoadingState.Run;
        this.Load.Click += OnRetryClick;
        Refresh();
    }

    private void OnRetryClick(object sender, MouseButtonEventArgs e)
    {
        if (this.Load.State.LoadingState == MyLoading.MyLoadingState.Error) InitLoading();
    }

    private async Task RefreshAsync()
    {
        try
        {
            const string HomepageMarketUri =
                "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Homepage.Market/Custom.xaml";
            var content = await (await HttpRequestBuilder.Create(HomepageMarketUri).SendAsync(true)).AsStringAsync();
            content = content.Replace("EventType=\"刷新主页\"", "EventType=\"刷新主页市场\"");
            this.PanCustom.Children.Clear();
            this.PanCustom.Children.Add((UIElement)ModBase.GetObjectFromXML(
                $"<StackPanel xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' xmlns:local='clr-namespace:PCL;assembly=Plain Craft Launcher 2' xmlns:sys='clr-namespace:System;assembly=System.Runtime'>{content}</StackPanel>"));
            this.Load.State.LoadingState = MyLoading.MyLoadingState.Stop;
            this.PanMain.Visibility = Visibility.Visible;
        }
        catch
        {
            this.Load.Text = "加载失败，点击重试";
            this.Load.State.LoadingState = MyLoading.MyLoadingState.Error;
            this.PanMain.Visibility = Visibility.Visible;
        }
    }
}
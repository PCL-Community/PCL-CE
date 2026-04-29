using System;
using System.Windows;
using System.Windows.Input;
using PCL.Core.IO.Net.Http.Client.Request;

namespace PCL
{
    public partial class PageHomePageMarket : MyPageRight, IRefreshable
    {
        private ModLoader.LoaderTask<bool, string> Loader;

        public PageHomePageMarket()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Loader = new ModLoader.LoaderTask<bool, string>("HomepageMarket", HomepageMarketGet);
            PageLoaderInit(Load, PanLoad, PanMain, PanCustom, Loader, _ => Refresh());
        }

        public void Refresh()
        {
            Loader.Start();
        }

        private void HomepageMarketGet(ModLoader.LoaderTask<bool, string> Task)
        {
            try
            {
                const string HomepageMarketUri = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/JingHai-Lingyun/Custom.xaml";
                var content = HttpRequest.Create(HomepageMarketUri).SendAsync().Result.AsStringAsync().Result;
                content = content.Replace("EventType=\"刷新主页\"", "EventType=\"刷新主页市场\"");

                ModBase.RunInUi(() =>
                {
                    PanCustom.Children.Clear();
                    var element = ModBase.GetObjectFromXML($"<StackPanel xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' xmlns:local='clr-namespace:PCL;assembly=Plain Craft Launcher 2' xmlns:sys='clr-namespace:System;assembly=System.Runtime'>{content}</StackPanel>") as UIElement;

                    if (element != null)
                    {
                        PanCustom.Children.Add(element);
                    }
                });

                Task.Output = content;
            }
            catch (Exception ex)
            {
                throw new Exception("加载主页市场失败", ex);
            }
        }
    }
}
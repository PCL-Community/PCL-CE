using System;
using System.Windows;
using System.Windows.Input;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;

namespace PCL
{
    public partial class PageHomePageMarket : MyPageRight, IRefreshable
    {
        private ModLoader.LoaderTask<bool, string> loader;

        public PageHomePageMarket()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            loader = new ModLoader.LoaderTask<bool, string>("HomepageMarket", HomepageMarketGet);
            PageLoaderInit(Load, PanLoad, PanMain, PanCustom, loader, _ => Refresh());
        }

        public void Refresh()
        {
            loader.Start();
        }

        private void HomepageMarketGet(ModLoader.LoaderTask<bool, string> Task)
        {
            try
            {
                const string homepageMarketUri = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/JingHai-Lingyun/Custom.xaml";
                var content = HttpRequest.Create(homepageMarketUri).SendAsync().Result.AsStringAsync().Result;
                content = content.Replace("EventType=\"刷新主页\"", "EventType=\"刷新主页市场\"");

                ModBase.RunInUi(() =>
                {
                    PanCustom.Children.Clear();
                    var element = ModBase.GetObjectFromXML($"<StackPanel xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' xmlns:local='clr-namespace:PCL;assembly=Plain Craft Launcher 2' xmlns:sys='clr-namespace:System;assembly=System.Runtime'>{content}</StackPanel>") as UIElement;

                    if (element is not null)
                    {
                        PanCustom.Children.Add(element);
                    }
                });

                Task.output = content;
            }
            catch (Exception ex)
            {
                throw new Exception(Lang.Text("Setup.Ui.HomepageMarket.LoadFailed"), ex);
            }
        }
    }
}
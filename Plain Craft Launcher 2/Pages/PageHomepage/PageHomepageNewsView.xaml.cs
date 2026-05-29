using PCL.Core.App.Localization;
using PCL.Core.ViewModel.Homepage;

namespace PCL;

public partial class PageHomepageNewsView : MyPageRight
{
    public PageHomepageNewsView()
    {
        InitializeComponent();
        Load.Text = Lang.Text("Launch.Homepage.News.Loading");
        DataContext = new NewsViewModel();
    }
}
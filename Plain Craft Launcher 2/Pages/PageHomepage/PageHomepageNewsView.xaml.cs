using PCL.Core.ViewModel.Homepage;

namespace PCL;

public partial class PageHomepageNewsView : MyPageRight
{
    public PageHomepageNewsView()
    {
        InitializeComponent();
        DataContext = new NewsViewModel();
    }
}
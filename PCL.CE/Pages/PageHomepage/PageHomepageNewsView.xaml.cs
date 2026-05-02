using PCL.CE.Core.ViewModel.Homepage;

namespace PCL.CE
{
    public partial class PageHomepageNewsView : MyPageRight
    {
        public PageHomepageNewsView()
        {
            InitializeComponent();
            this.DataContext = new NewsViewModel();
        }
    }
}
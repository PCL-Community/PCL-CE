namespace PCL;

public partial class PageSpeedRight : IPageView
{
    public PageSpeedRight()
    {
        InitializeComponent();
    }

    public void OnNavigatedTo(object? parameter) => PanBack.ScrollToHome();

    public void OnNavigatedFrom()
    {
    }
}

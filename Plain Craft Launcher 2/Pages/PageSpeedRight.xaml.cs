namespace PCL;

public partial class PageSpeedRight
{
    public PageSpeedRight()
    {
        InitializeComponent();
        Loaded += (_, __) => Init();
    }

    private void Init()
    {
        PanBack.ScrollToHome();
    }
}
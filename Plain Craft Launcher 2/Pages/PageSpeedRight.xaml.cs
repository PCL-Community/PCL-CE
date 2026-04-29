namespace PCL;

public partial class PageSpeedRight
{
    public PageSpeedRight()
    {
        InitializeComponent();
        Loaded += (_, _) => Init();
    }

    private void Init()
    {
        PanBack.ScrollToHome();
    }
}
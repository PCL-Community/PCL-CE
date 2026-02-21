namespace PCL;

public partial class PageSpeedRight
{
    public PageSpeedRight()
    {
        Loaded += (_, __) => Init();
    }

    private void Init()
    {
        PanBack.ScrollToHome();
    }
}
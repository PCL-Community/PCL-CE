namespace PCL;

public partial class PageCrashLogs
{
    public PageCrashLogs()
    {
        InitializeComponent();
    }

    protected override void Render(MinecraftCrashSession? session)
    {
        PanMain.Children.Clear();
        if (session is null) return;

        PanMain.Children.Add(MinecraftCrashVisualFactory.CreateHint(
            MinecraftCrashUi.Text("Crash.Logs.Hint"),
            MyHint.Themes.Blue));

        foreach (var log in session.Presentation.Logs)
            PanMain.Children.Add(MinecraftCrashVisualFactory.CreateLogCard(log));
    }
}
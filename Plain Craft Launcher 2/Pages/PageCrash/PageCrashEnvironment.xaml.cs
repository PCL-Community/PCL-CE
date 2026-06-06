using System.Windows;
using System.Windows.Controls;
using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL;

public partial class PageCrashEnvironment
{
    private bool _showSensitive;

    public PageCrashEnvironment()
    {
        InitializeComponent();
    }

    protected override void Render(MinecraftCrashSession? session)
    {
        PanMain.Children.Clear();
        if (session is null) return;

        PanMain.Children.Add(MinecraftCrashVisualFactory.CreateHint(
            MinecraftCrashUi.Text(_showSensitive
                ? "Crash.Environment.HideSensitiveHint"
                : "Crash.Environment.ShowSensitiveHint"),
            _showSensitive
                ? MyHint.Themes.Yellow
                : MyHint.Themes.Blue));

        var privacy = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 10d) };
        var button = MinecraftCrashVisualFactory.IconButton(
            MinecraftCrashUi.Text(_showSensitive
                ? "Crash.Environment.HideSensitive"
                : "Crash.Environment.ShowSensitive"),
            _showSensitive ? "lucide/eye-off" : "lucide/eye",
            CrashActionPriority.Secondary);

        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.Click += (_, _) =>
        {
            _showSensitive = !_showSensitive;
            Refresh();
        };
        privacy.Children.Add(button);
        PanMain.Children.Add(privacy);

        foreach (var group in session.Presentation.Environment.GroupBy(static item => item.GroupKey))
            PanMain.Children.Add(MinecraftCrashVisualFactory.CreateEnvironmentGroup(group.Key, group, _showSensitive));
    }
}
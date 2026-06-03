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

        var privacy = new StackPanel();
        privacy.Children.Add(MinecraftCrashVisualFactory.CreateHint(
            MinecraftCrashUi.Text(_showSensitive
                ? "Crash.Environment.HideSensitiveHint"
                : "Crash.Environment.ShowSensitiveHint"),
            _showSensitive
                ? MyHint.Themes.Yellow
                : MyHint.Themes.Blue));
        var button = MinecraftCrashVisualFactory.IconButton(
            MinecraftCrashUi.Text(_showSensitive
                ? "Crash.Environment.HideSensitive"
                : "Crash.Environment.ShowSensitive"),
            "F1 M12 2a5 5 0 0 1 5 5v3h1a2 2 0 0 1 2 2v8H4v-8a2 2 0 0 1 2-2h1V7a5 5 0 0 1 5-5Zm-3 8h6V7a3 3 0 0 0-6 0v3Z",
            CrashActionPriority.Secondary);

        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.Click += (_, _) =>
        {
            _showSensitive = !_showSensitive;
            Refresh();
        };
        privacy.Children.Add(button);
        PanMain.Children.Add(MinecraftCrashUi.CreateCard("Crash.Environment.Card.Privacy", privacy));

        foreach (var group in session.Presentation.Environment.GroupBy(static item => item.GroupKey))
            PanMain.Children.Add(MinecraftCrashVisualFactory.CreateEnvironmentGroup(group.Key, group, _showSensitive));
    }
}
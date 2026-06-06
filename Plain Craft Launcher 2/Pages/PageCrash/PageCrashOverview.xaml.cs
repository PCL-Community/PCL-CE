using System.Windows.Controls;

namespace PCL;

public partial class PageCrashOverview
{
    public PageCrashOverview()
    {
        InitializeComponent();
    }

    protected override void Render(MinecraftCrashSession? session)
    {
        PanMain.Children.Clear();

        if (session is null)
        {
            PanMain.Children.Add(MinecraftCrashVisualFactory.CreateHint(
                MinecraftCrashUi.Text("Crash.Page.NoSession"),
                MyHint.Themes.Yellow));
            return;
        }

        var presentation = session.Presentation;
        PanMain.Children.Add(MinecraftCrashVisualFactory.CreateHeroCard(session));

        var top = presentation.Diagnoses.FirstOrDefault();
        if (top is not null)
            PanMain.Children.Add(MinecraftCrashVisualFactory.CreateDiagnosisCard(top, true));
        else
            PanMain.Children.Add(MinecraftCrashVisualFactory.CreateHint(
                MinecraftCrashUi.Text("Crash.Overview.NoTopDiagnosis"),
                MyHint.Themes.Yellow));

        if (presentation.Metrics.Count > 0)
            PanMain.Children.Add(MinecraftCrashVisualFactory.CreateMetricGrid(presentation.Metrics));

        var actionPanel = new StackPanel();
        var actionIndex = 1;
        foreach (var action in presentation.Actions.Take(6))
            actionPanel.Children.Add(MinecraftCrashVisualFactory.CreateActionListItem(action, actionIndex++));
        PanMain.Children.Add(MinecraftCrashUi.CreateCard("Crash.Overview.Card.Actions", actionPanel));

        var logs = new StackPanel();
        foreach (var log in presentation.Logs.Take(4))
            logs.Children.Add(MinecraftCrashVisualFactory.CreateLogSummaryItem(log));

        if (logs.Children.Count > 0)
            PanMain.Children.Add(MinecraftCrashUi.CreateCard("Crash.Overview.Card.RelatedLogs", logs));
    }
}
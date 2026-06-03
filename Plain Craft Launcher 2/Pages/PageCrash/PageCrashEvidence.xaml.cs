using System.Windows;
using System.Windows.Controls;

namespace PCL;

public partial class PageCrashEvidence : PageCrashRightBase
{
    public PageCrashEvidence()
    {
        InitializeComponent();
    }

    protected override void Render(MinecraftCrashSession? session)
    {
        PanMain.Children.Clear();
        if (session is null) return;

        var diagnostic = new StackPanel();
        diagnostic.Children.Add(MinecraftCrashVisualFactory.CreateHint(
            MinecraftCrashUi.Text("Crash.Evidence.DiagnosticsHint"),
            MyHint.Themes.Blue
        ));

        foreach (var diagnosis in session.Presentation.Diagnoses)
        {
            diagnostic.Children.Add(MinecraftCrashVisualFactory.Text(
                MinecraftCrashUi.Text(diagnosis.TitleKey, diagnosis.Parameters),
                15,
                FontWeights.SemiBold
            ));
            foreach (var evidence in diagnosis.Evidence)
                diagnostic.Children.Add(MinecraftCrashVisualFactory.CreateEvidenceItem(evidence));
        }

        PanMain.Children.Add(MinecraftCrashUi.CreateCard("Crash.Evidence.Diagnostics", diagnostic));

        var facts = new StackPanel();
        facts.Children.Add(MinecraftCrashVisualFactory.CreateHint(
            MinecraftCrashUi.Text("Crash.Evidence.RawFactsHint"),
            MyHint.Themes.Blue
        ));

        foreach (var fact in session.Presentation.Facts)
            facts.Children.Add(MinecraftCrashVisualFactory.CreateFactItem(fact));
        PanMain.Children.Add(MinecraftCrashUi.CreateCard("Crash.Evidence.RawFacts", facts));
    }
}
using System.Windows;
using System.Windows.Controls;

namespace PCL;

public partial class PageCrashDiagnoses
{
    public PageCrashDiagnoses()
    {
        InitializeComponent();
    }

    protected override void Render(MinecraftCrashSession? session)
    {
        PanMain.Children.Clear();
        if (session is null) return;

        PanMain.Children.Add(MinecraftCrashVisualFactory.CreateHint(
            MinecraftCrashUi.Text("Crash.Diagnoses.Hint"),
            MyHint.Themes.Blue));

        var diagnoses = session.Presentation.Diagnoses;
        var primary = diagnoses.FirstOrDefault();
        if (primary is not null)
            PanMain.Children.Add(MinecraftCrashVisualFactory.CreateDiagnosisCard(primary, true));

        var secondary = diagnoses.Skip(1).ToList();
        if (secondary.Count > 0)
        {
            var stack = new StackPanel();
            foreach (var diagnosis in secondary)
                stack.Children.Add(MinecraftCrashVisualFactory.CreateDiagnosisCard(diagnosis, false));

            PanMain.Children.Add(MinecraftCrashUi.CreateCard("Crash.Diagnoses.Secondary", stack));
        }

        var suppressed = diagnoses
            .Where(static diagnosis => diagnosis.Notes.Count > 0)
            .ToList();
        if (suppressed.Count == 0) return;

        var notes = new StackPanel();
        notes.Children.Add(MinecraftCrashVisualFactory.CreateHint(
            MinecraftCrashUi.Text("Crash.Diagnoses.SuppressedHint"),
            MyHint.Themes.Yellow));

        foreach (var diagnosis in suppressed)
        {
            notes.Children.Add(MinecraftCrashVisualFactory.Text(
                MinecraftCrashUi.Text(diagnosis.TitleKey, diagnosis.Parameters),
                14,
                FontWeights.SemiBold
            ));

            foreach (var note in diagnosis.Notes)
                notes.Children.Add(MinecraftCrashVisualFactory.CreateHint(
                    MinecraftCrashUi.Text(note.Key, note.Parameters),
                    MyHint.Themes.Yellow
                ));
        }

        PanMain.Children.Add(MinecraftCrashUi.CreateCard("Crash.Diagnoses.SuppressedSymptoms", notes));
    }
}
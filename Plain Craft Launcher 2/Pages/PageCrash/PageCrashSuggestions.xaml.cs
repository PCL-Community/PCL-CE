using System.Windows.Controls;

namespace PCL;

public partial class PageCrashSuggestions
{
    public PageCrashSuggestions()
    {
        InitializeComponent();
    }

    protected override void Render(MinecraftCrashSession? session)
    {
        PanMain.Children.Clear();
        if (session is null) return;

        PanMain.Children.Add(MinecraftCrashVisualFactory.CreateHint(
            MinecraftCrashUi.Text("Crash.Suggestions.Hint"),
            MyHint.Themes.Blue));

        foreach (var group in session.Presentation.Actions
                     .GroupBy(static action => action.Group)
                     .OrderBy(static group => group.Key))
        {
            var stack = new StackPanel();
            var index = 1;
            foreach (var action in group
                         .OrderBy(static action => action.Order)
                         .ThenBy(static action => action.Priority))
                stack.Children.Add(MinecraftCrashVisualFactory.CreateActionListItem(action, index++));
            PanMain.Children.Add(MinecraftCrashUi.CreateCard("Crash.Suggestions.Group." + group.Key, stack));
        }
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>实例设置各子页共用的取值、布局与提示辅助。</summary>
internal static class InstancePageHelper
{
    public static McInstance? ResolveInstance(object? parameter)
    {
        return parameter as McInstance ?? PageInstanceLeft.ResolveInstance();
    }

    public static string SavesPath(McInstance instance) => Path.Combine(instance.PathIndie, "saves");

    public static string ModsPath(McInstance instance) => Path.Combine(instance.PathIndie, "mods");

    public static string ScreenshotsPath(McInstance instance) => Path.Combine(instance.PathIndie, "screenshots");

    public static string DatapacksPath(string saveFolder) => Path.Combine(saveFolder, "datapacks");

    public static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    public static void OpenFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                HintService.Show(Hints.Warning(Lang.Text("Instance.Saves.FolderNotFound")));
                return;
            }

            Basics.OpenPath(path);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "打开文件夹失败", LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Error.OperationFailed"));
            HintService.Show(Hints.Error(Lang.Text("Instance.Error.OperationFailed")));
        }
    }

    public static (MyScrollViewer Scroll, StackPanel Panel) CreateScrollPage(double margin = 25d)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(margin),
            Spacing = 15d
        };
        var scroll = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };
        return (scroll, panel);
    }

    public static MyCard CreateCard(string titleKey, out StackPanel body)
    {
        var card = new MyCard
        {
            Title = Lang.Text(titleKey),
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
        body = new StackPanel
        {
            Margin = new Thickness(15d, 40d, 25d, 15d),
            Spacing = 8d
        };
        card.BorderChild = body;
        return card;
    }

    public static Grid Row(string labelText, Control content)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Height = 30d
        };
        var label = new TextBlock
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 25d, 0d)
        };
        grid.Children.Add(label);
        Grid.SetColumn(content, 1);
        content.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(content);
        return grid;
    }

    public static Grid InfoRow(string labelText, string value)
    {
        return Row(labelText, new TextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        });
    }

    public static MyButton Button(
        string textKey,
        Action onClick,
        bool enabled = true,
        MyButton.ColorState color = MyButton.ColorState.Normal)
    {
        var button = new MyButton
        {
            Text = Lang.Text(textKey),
            MinWidth = 140d,
            Height = 35d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(0d, 0d, 12d, 0d),
            IsEnabled = enabled,
            ColorType = color
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    public static WrapPanel ButtonRow(params Control[] controls)
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0d, 2d, 0d, 2d) };
        foreach (var control in controls)
            panel.Children.Add(control);
        return panel;
    }

    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)bytes;
        var unit = 0;
        while (size >= 1024d && unit < units.Length - 1)
        {
            size /= 1024d;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }

    public static string FormatTime(DateTime time)
    {
        return time.Kind == DateTimeKind.Utc ? time.ToLocalTime().ToString("g") : time.ToString("g");
    }

    public static void ReportError(string titleKey, Exception ex)
    {
        LauncherCommon.Log(ex, Lang.Text(titleKey), LauncherLogLevel.Feedback,
            userSummary: Lang.Text("Instance.Error.OperationFailed"));
        HintService.Show(Hints.Error(Lang.Text("Instance.Error.OperationFailed")));
    }
}

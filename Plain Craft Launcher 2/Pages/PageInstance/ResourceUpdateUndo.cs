using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.Utils;

namespace PCL;

internal static class ResourceUpdateUndo
{
    public static void RefreshSelectionButton(MyIconTextButton button, int count, bool busy)
    {
        button.Text = Lang.Text("Instance.Resource.Undo.SelectedCount", count);
        button.ToolTip = Lang.Text("Instance.Resource.Undo.SelectionToolTip");
        button.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        button.IsEnabled = count > 0 && !busy;
    }

    public static async Task UndoSelectedAsync(string gameDirectory, string[] paths)
    {
        var result = await Task.Run(() => new ResourceUpdateRecycleBin(gameDirectory).UndoMany(paths));
        foreach (var failure in result.Failures)
            ModBase.Log(failure.Value, "撤回资源更新失败：" + failure.Key, ModBase.LogLevel.Debug);
        HintService.Hint(Lang.Text("Instance.Resource.Undo.BatchResult",
            result.RestoredPaths.Count, result.SkippedPaths.Count, result.Failures.Count),
            result.Failures.Count > 0 ? HintType.Error : HintType.Success);
    }

    public static MyIconButton? CreateButton(MyLocalCompItem item, string gameDirectory, Action refresh)
    {
        var recycleBin = new ResourceUpdateRecycleBin(gameDirectory);
        if (item.Entry.IsFolder || !recycleBin.CanUndo(item.Entry.path)) return null;

        var button = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/rotate-ccw", Tag = item };
        button.ToolTip = Lang.Text("Instance.Resource.Item.UndoToolTip");
        ToolTipService.SetPlacement(button, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(button, 30d);
        ToolTipService.SetHorizontalOffset(button, 2d);
        button.Click += (_, _) =>
        {
            button.IsEnabled = false;
            try
            {
                recycleBin.Undo(item.Entry.path);
                item.Buttons = item.Buttons.Where(existing => existing != button).ToArray();
                HintService.Hint(Lang.Text("Instance.Resource.Undo.Success", item.Entry.FileName), HintType.Success);
                refresh();
            }
            catch (Exception ex)
            {
                button.IsEnabled = true;
                ModBase.Log(ex, "撤回资源更新失败", ModBase.LogLevel.Hint,
                    userSummary: Lang.Text("Instance.Resource.Undo.Failed", ex.Message));
                refresh();
            }
        };
        return button;
    }
}

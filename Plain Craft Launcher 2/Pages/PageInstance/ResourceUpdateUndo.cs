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
        if (item.Entry.IsFolder) return null;

        var button = new MyIconButton
        {
            LogoScale = 1d, SvgIcon = "lucide/rotate-ccw", Tag = item, Visibility = Visibility.Collapsed
        };
        button.ToolTip = Lang.Text("Instance.Resource.Item.UndoToolTip");
        ToolTipService.SetPlacement(button, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(button, 30d);
        ToolTipService.SetHorizontalOffset(button, 2d);
        var revision = 0;
        async Task RefreshVisibilityAsync()
        {
            var currentRevision = ++revision;
            var path = item.Entry.path;
            try
            {
                var available = await Task.Run(() => recycleBin.CanUndo(path));
                if (currentRevision == revision && path == item.Entry.path)
                    button.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "检查资源撤回状态失败");
            }
        }
        button.Loaded += async (_, _) => await RefreshVisibilityAsync();
        _ = RefreshVisibilityAsync();
        button.Click += async (_, _) =>
        {
            ++revision;
            button.IsEnabled = false;
            item.IsEnabled = false;
            var path = item.Entry.path;
            try
            {
                await Task.Run(() => recycleBin.Undo(path));
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
            finally
            {
                item.IsEnabled = true;
            }
        };
        return button;
    }
}

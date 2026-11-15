using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.UI.Hints;

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
            Common.Log(failure.Value, "撤回资源更新失败：" + failure.Key, Common.LogLevel.Debug);
        var message = Lang.Text("Instance.Resource.Undo.BatchResult",
            result.RestoredPaths.Count, result.SkippedPaths.Count, result.Failures.Count);
        HintService.Show(result.Failures.Count > 0 ? Hints.Error(message) : Hints.Success(message));
    }

    public static MyIconButton? CreateButton(MyLocalCompItem item, string gameDirectory, Action refresh)
    {
        if (item.Entry is not { IsFolder: false } entry) return null;
        var viewModel = item.ViewModel;
        var path = entry.path;
        var recycleBin = new ResourceUpdateRecycleBin(gameDirectory);

        var button = new MyIconButton
        {
            LogoScale = 1d, SvgIcon = "lucide/rotate-ccw", Tag = item, Visibility = Visibility.Collapsed
        };
        button.ToolTip = Lang.Text("Instance.Resource.Item.UndoToolTip");
        ToolTipService.SetPlacement(button, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(button, 30d);
        ToolTipService.SetHorizontalOffset(button, 2d);
        var revision = 0;
        bool IsCurrentRow() => ReferenceEquals(viewModel, item.ViewModel) && ReferenceEquals(entry, item.Entry);
        async Task RefreshVisibilityAsync()
        {
            var currentRevision = ++revision;
            try
            {
                var available = await Task.Run(() => recycleBin.CanUndo(path));
                if (currentRevision == revision && IsCurrentRow())
                    button.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Common.Log(ex, "检查资源撤回状态失败");
            }
        }
        button.Loaded += async (_, _) => await RefreshVisibilityAsync();
        _ = RefreshVisibilityAsync();
        button.Click += async (_, _) =>
        {
            if (!IsCurrentRow()) return;
            ++revision;
            button.IsEnabled = false;
            item.IsEnabled = false;
            try
            {
                await Task.Run(() => recycleBin.Undo(path));
                button.Visibility = Visibility.Collapsed;
                HintService.Show(Hints.Success(Lang.Text("Instance.Resource.Undo.Success", entry.FileName)));
            }
            catch (Exception ex)
            {
                button.IsEnabled = true;
                Common.Log(ex, "撤回资源更新失败", Common.LogLevel.Hint,
                    userSummary: Lang.Text("Instance.Resource.Undo.Failed", ex.Message));
            }
            finally
            {
                if (IsCurrentRow()) item.IsEnabled = true;
                refresh();
            }
        };
        return button;
    }
}

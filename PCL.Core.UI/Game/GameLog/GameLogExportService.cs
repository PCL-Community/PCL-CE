using System.Globalization;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Watcher;
using PCL.Core.UI.Hints;
using HintPresets = PCL.Core.UI.Hints.Hints;

namespace PCL.Core.UI.Game.Log;

/// <summary>游戏日志与运行栈的文件导出。与页面 ViewModel 分离，便于单独测试与复用。</summary>
internal static class GameLogExportService
{
    public static async Task ExportLogAsync(GameSession session)
    {
        var savePath = await SystemDialogs.SelectSaveFileAsync(
            Lang.Text("LogPage.Export.SelectLocation"),
            Lang.Text("LogPage.Export.GameLog.FileName", session.Metadata.InstanceName),
            Lang.Text("LogPage.Export.GameLog.Filter"));
        if (savePath.Length < 3)
            return;

        try
        {
            var lines = session.FullLog;
            await Task.Run(() => File.WriteAllLines(savePath, lines));
            HintService.Show(HintPresets.Success(Lang.Text("LogPage.Export.Success")));
            Basics.OpenPath(savePath);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Watcher", Lang.Text("LogPage.Error.OperationFailed"));
        }
    }

    public static async Task ExportStackDumpAsync(GameSession session)
    {
        var timestamp = DateTime
            .Now
            .ToString("G", CultureInfo.InvariantCulture)
            .Replace("/", "-", StringComparison.Ordinal)
            .Replace(":", ".", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);

        var savePath = await SystemDialogs.SelectSaveFileAsync(
            Lang.Text("LogPage.Export.SelectLocation"),
            Lang.Text("LogPage.ExportStack.FileName", timestamp),
            Lang.Text("LogPage.ExportStack.Filter"));
        if (savePath.Length < 3)
            return;

        HintService.Show(HintPresets.Info(Lang.Text("LogPage.ExportStack.Progress")));

        try
        {
            var dump = await session.CaptureStackDumpsAsync();
            await Task.Run(() => File.WriteAllLines(savePath, dump));
            HintService.Show(HintPresets.Success(Lang.Text("LogPage.ExportStack.Success")));
            Basics.OpenPath(savePath);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Watcher", Lang.Text("LogPage.Error.OperationFailed"));
        }
    }
}
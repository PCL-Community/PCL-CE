using System.Windows;
using PCL.Core.App.Localization;

namespace PCL;

public static partial class Comp
{
    public class CompClipboard
    {
        public static string? currentText;

        public static void GetClipboardResource()
        {
            string? text = null;
            Common.RunInUiWait(() => text = Clipboard.GetText());

            if (string.IsNullOrEmpty(text) || text == currentText) return;
            currentText = text;

            Common.RunInNewThread(
                () =>
                {
                    try
                    {
                        var projectId = ResolveLinkToProjectId(text);

                        if (string.IsNullOrEmpty(projectId)) return;
                        Common.Log($"[Clipboard] Found ProjectId: {projectId}");

                        System.Windows.Application.Current.Dispatcher.BeginInvoke(
                            new Func<Task>(async () =>
                            {
                                if (Launcher.MyMsgBox(
                                        Lang.Text(
                                            "Download.Comp.Detail.Clipboard.Detected.Message"),
                                        Lang.Text("Download.Comp.Detail.Clipboard.Detected.Title"),
                                        Lang.Text("Common.Action.Confirm"),
                                        Lang.Text("Common.Action.Cancel"),
                                        forceWait: true)
                                    == 1)
                                {
                                    HintService.Hint(
                                        Lang.Text("Download.Comp.Detail.Clipboard.Fetching"));

                                    var ids = new List<string>
                                    {
                                        projectId
                                    };
                                    var compProjects =
                                        await CompRequest.GetCompProjectsByIdsAsync(ids);

                                    if (compProjects.Count == 0)
                                    {
                                        HintService.Hint(
                                            Lang.Text(
                                                "Download.Comp.Detail.Clipboard.InvalidContent"),
                                            HintType.Error);
                                        return;
                                    }

                                    Launcher.frmMain!.PageChange(
                                        new FormMain.PageStackData
                                        {
                                            page = FormMain.PageType.CompDetail,
                                            additional = (
                                                compProjects.First(),
                                                [],
                                                string.Empty,
                                                CompLoaderType.Any,
                                                CompType.Any,
                                                string.Empty)
                                        });
                                }
                            }));
                    }
                    catch (Exception ex)
                    {
                        Common.Log(ex, "Error processing clipboard resource");
                    }
                },
                "Clipboard Resource Processing");
        }
    }
}

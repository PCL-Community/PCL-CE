using PCL.Core.App.Localization;
using PCL.Core.Platform;

namespace PCL;

public static partial class Comp
{
    public class CompClipboard
    {
        public static string? currentText;

        public static void GetClipboardResource()
        {
            string? text = null;
            LauncherCommon.RunInUiWait(() => text = PlatformServices.Clipboard?.GetText());

            if (string.IsNullOrEmpty(text) || text == currentText) return;
            currentText = text;

            LauncherCommon.RunInNewThread(
                () =>
                {
                    try
                    {
                        var projectId = ResolveLinkToProjectId(text);

                        if (string.IsNullOrEmpty(projectId)) return;
                        LauncherCommon.Log($"[Clipboard] Found ProjectId: {projectId}");

                        LauncherCommon.RunInUi(async () =>
                        {
                            if (LauncherUi.Interaction?.ShowConfirm(
                                    Lang.Text("Download.Comp.Detail.Clipboard.Detected.Message"),
                                    Lang.Text("Download.Comp.Detail.Clipboard.Detected.Title"),
                                    Lang.Text("Common.Action.Confirm"),
                                    Lang.Text("Common.Action.Cancel"))
                                != true)
                                return;

                            LauncherUi.Interaction?.ShowHint(
                                Lang.Text("Download.Comp.Detail.Clipboard.Fetching"));

                            var ids = new List<string>
                            {
                                projectId
                            };
                            var compProjects =
                                await CompRequest.GetCompProjectsByIdsAsync(ids);

                            if (compProjects.Count == 0)
                            {
                                LauncherUi.Interaction?.ShowHint(
                                    Lang.Text("Download.Comp.Detail.Clipboard.InvalidContent"),
                                    NotificationType.Error);
                                return;
                            }

                            CompNavigation.Current?.ShowCompDetail(compProjects.First());
                        });
                    }
                    catch (Exception ex)
                    {
                        LauncherCommon.Log(ex, "Error processing clipboard resource");
                    }
                },
                "Clipboard Resource Processing");
        }
    }
}
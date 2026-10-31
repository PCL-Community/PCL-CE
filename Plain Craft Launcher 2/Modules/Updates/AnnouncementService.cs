using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;

namespace PCL;

public static class AnnouncementService
{
    public static void Load()
    {
        if (States.System.AnnounceSolution > 1)
            return;

        var showedAnnounced = States.Hint.ShowedAnnouncements
            .Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var showAnnounce = UpdateManager.remoteServer.GetAnnouncementList().Content
            .Where(x => !showedAnnounced.Contains(x.Id))
            .ToList();

        Common.Log("[System] 需要展示的公告数量：" + showAnnounce.Count);

        Common.RunInNewThread(() =>
        {
            foreach (var item in showAnnounce)
            {
                _ = DialogService.Instance.ShowAsync(new TextDialogRequest
                {
                    Title = item.Title,
                    Content = item.Detail,
                    Buttons = DialogButtons.From(
                        DialogButton.Create(item.Btn1 is null ? "" : item.Btn1.Text,
                            () =>
                            {
                                if (EventTypeMapper.TryParse(
                                        item.Btn1.Command, out var eventType))
                                    CustomEvent.Raise(eventType, item.Btn1.CommandParameter);
                            }),
                        DialogButton.Create(item.Btn2 is null ? "" : item.Btn2.Text,
                            () =>
                            {
                                if (EventTypeMapper.TryParse(
                                        item.Btn2.Command, out var eventType))
                                    CustomEvent.Raise(eventType, item.Btn2.CommandParameter);
                            }),
                        DialogButton.Create(Lang.Text("Common.Action.Close"))),
                    ForceWait = true
                });
            }
        });

        showedAnnounced.AddRange(showAnnounce.Select(x => x.Id));
        showedAnnounced = showedAnnounced.Distinct().ToList();
        States.Hint.ShowedAnnouncements = showedAnnounced.Join("|");
    }
}

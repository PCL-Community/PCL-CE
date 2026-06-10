using PCL.Core.App;
using PCL.Core.App.Localization;

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

        ModBase.Log("[System] 需要展示的公告数量：" + showAnnounce.Count);

        ModBase.RunInNewThread(() =>
        {
            foreach (var item in showAnnounce)
            {
                ModMain.MyMsgBox(item.Detail, item.Title,
                    item.Btn1 is null ? "" : item.Btn1.Text,
                    item.Btn2 is null ? "" : item.Btn2.Text,
                    Lang.Text("Common.Action.Close"),
                    button1Action: () =>
                    {
                        if (TryParseEventType(item.Btn1.Command, out var eventType))
                            CustomEvent.Raise(eventType, item.Btn1.CommandParameter);
                    },
                    button2Action: () =>
                    {
                        if (TryParseEventType(item.Btn2.Command, out var eventType))
                            CustomEvent.Raise(eventType, item.Btn2.CommandParameter);
                    });
            }
        });

        showedAnnounced.AddRange(showAnnounce.Select(x => x.Id));
        showedAnnounced = showedAnnounced.Distinct().ToList();
        States.Hint.ShowedAnnouncements = showedAnnounced.Join("|");
    }

    /// <summary>解析事件类型，支持中英文（中文通过 <see cref="LegacyEventCompat"/> 映射）。</summary>
    private static bool TryParseEventType(string? command, out EventType eventType)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            eventType = EventType.None;
            return false;
        }
        var mapped = LegacyEventCompat.NameMap.GetValueOrDefault(command, command);
        return Enum.TryParse(mapped, true, out eventType);
    }
}

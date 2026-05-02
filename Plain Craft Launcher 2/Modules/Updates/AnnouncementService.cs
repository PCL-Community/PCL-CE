using Microsoft.VisualBasic.CompilerServices;
using PCL.Core.App;

namespace PCL;

public static class AnnouncementService
{
    public static void Load()
    {
        var announcementDesire = States.System.AnnounceSolution;
        if (!Conversions.ToBoolean(
                Operators.ConditionalCompareObjectLessEqual(announcementDesire, 1, false)))
            return;

        var showedAnnounced = States.Hint.ShowedAnnouncements.ToString()
            .Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var showAnnounce = UpdateManager.RemoteServer.GetAnnouncementList().content
            .Where(x => !showedAnnounced.Contains(x.id))
            .ToList();

        ModBase.Log("[System] 需要展示的公告数量：" + showAnnounce.Count);

        ModBase.RunInNewThread(() =>
        {
            foreach (var item in showAnnounce)
            {
                ModMain.MyMsgBox(item.detail, item.title,
                    item.btn1 is null ? "" : item.btn1.text,
                    item.btn2 is null ? "" : item.btn2.text,
                    "关闭",
                    Button1Action: () =>
                    {
                        if (Enum.TryParse<CustomEvent.EventType>(
                                item.btn1.command, true, out var eventType))
                            CustomEvent.Raise(eventType, item.btn1.command_paramter);
                    },
                    Button2Action: () =>
                    {
                        if (Enum.TryParse<CustomEvent.EventType>(
                                item.btn2.command, true, out var eventType))
                            CustomEvent.Raise(eventType, item.btn2.command_paramter);
                    });
            }
        });

        showedAnnounced.AddRange(showAnnounce.Select(x => x.id));
        showedAnnounced = showedAnnounced.Distinct().ToList();
        States.Hint.ShowedAnnouncements = showedAnnounced.Join("|");
    }
}
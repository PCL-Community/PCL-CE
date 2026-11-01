using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;
using PCL.Network;

namespace PCL;

/// <summary>设置页面中「反馈」页的视图模型。</summary>
public partial class PageSetupFeedbackViewModel : ViewModelBase
{
    [ObservableProperty] private bool _showCompleted;

    [ObservableProperty] private bool _showDecline;

    [ObservableProperty] private bool _showDuplicate;

    [ObservableProperty] private bool _showIgnored;

    [ObservableProperty] private bool _showPause;

    [ObservableProperty] private bool _showProcessing;

    [ObservableProperty] private bool _showUpnext;

    [ObservableProperty] private bool _showWait;

    [ObservableProperty] private bool _showWaitingProcess;

    public IReadOnlyList<SetupFeedbackEntry> Items { get; private set; } = [];

    public ObservableCollection<SetupFeedbackItemRow> ProcessingRows { get; } = [];

    public ObservableCollection<SetupFeedbackItemRow> WaitingProcessRows { get; } = [];

    public ObservableCollection<SetupFeedbackItemRow> WaitRows { get; } = [];

    public ObservableCollection<SetupFeedbackItemRow> PauseRows { get; } = [];

    public ObservableCollection<SetupFeedbackItemRow> UpnextRows { get; } = [];

    public ObservableCollection<SetupFeedbackItemRow> CompletedRows { get; } = [];

    public ObservableCollection<SetupFeedbackItemRow> DeclineRows { get; } = [];

    public ObservableCollection<SetupFeedbackItemRow> IgnoredRows { get; } = [];

    public ObservableCollection<SetupFeedbackItemRow> DuplicateRows { get; } = [];

    private Dictionary<string, (ObservableCollection<SetupFeedbackItemRow> Rows, string Icon)>? _groups;

    private Dictionary<string, (ObservableCollection<SetupFeedbackItemRow> Rows, string Icon)> Groups =>
        _groups ??= new Dictionary<string, (ObservableCollection<SetupFeedbackItemRow> Rows, string Icon)>
        {
            [((long)PageSetupFeedback.TagId.Processing).ToString()] = (ProcessingRows, "Blocks/CommandBlock.png"),
            [((long)PageSetupFeedback.TagId.WaitingProcess).ToString()] =
                (WaitingProcessRows, "Blocks/RedstoneBlock.png"),
            [((long)PageSetupFeedback.TagId.Wait).ToString()] = (WaitRows, "Blocks/Anvil.png"),
            [((long)PageSetupFeedback.TagId.Pause).ToString()] = (PauseRows, "Blocks/RedstoneLampOff.png"),
            [((long)PageSetupFeedback.TagId.Upnext).ToString()] = (UpnextRows, "Blocks/RedstoneLampOn.png"),
            [((long)PageSetupFeedback.TagId.Completed).ToString()] = (CompletedRows, "Blocks/Grass.png"),
            [((long)PageSetupFeedback.TagId.Decline).ToString()] = (DeclineRows, "Blocks/CobbleStone.png"),
            [((long)PageSetupFeedback.TagId.Ignored).ToString()] = (IgnoredRows, "Blocks/CobbleStone.png"),
            [((long)PageSetupFeedback.TagId.Duplicate).ToString()] = (DuplicateRows, "Blocks/CobbleStone.png")
        };

    public List<PageSetupFeedback.Feedback> FetchFeedbackList()
    {
        var list = Requester.FetchJson(
            "https://api.github.com/repos/PCL-Community/PCL2-CE/issues?state=all&sort=created&per_page=200",
            new RequestParam
            {
                Retries = 3,
                UseBrowserUserAgent = true
            }) as JsonArray;
        if (list is null)
            throw new Exception(Lang.Text("Setup.Feedback.LoadFailed"));
        var res = new List<PageSetupFeedback.Feedback>();
        foreach (var i in list)
        {
            if (i is not JsonObject issue) continue;
            var pullRequestToken = issue["pull_request"];
            if (pullRequestToken is not null && pullRequestToken.GetValueKind() != JsonValueKind.Null) continue;

            var item = issue.Deserialize<PageSetupFeedback.Feedback>()!;
            item.User = issue["user"]!["login"]!.ToString();
            item.Id = issue["number"]!.ToString();

            var issueType = "";
            var typeToken = issue["type"];
            if (typeToken is not null && typeToken.GetValueKind() == JsonValueKind.Object)
            {
                var typeNameToken = typeToken["name"];
                if (typeNameToken is not null) issueType = typeNameToken.ToString().ToLower();
            }

            item.Type = issueType;

            if (issue["labels"] is JsonArray thisTags)
                foreach (var thisTag in thisTags)
                    if (thisTag is JsonObject tagObj)
                        item.Tags.Add(tagObj["id"]!.ToString());

            res.Add(item);
        }

        return res;
    }

    public void SetItems(List<PageSetupFeedback.Feedback>? source)
    {
        var tagKeys = Enum.GetValues<PageSetupFeedback.TagId>().Select(id => ((long)id).ToString()).ToHashSet();
        foreach (var group in Groups.Values)
            group.Rows.Clear();
        var grouped = new List<SetupFeedbackEntry>();
        foreach (var item in source ?? [])
        {
            var tag = item.Tags.Find(tagKeys.Contains);
            if (tag is null) continue;
            grouped.Add(new SetupFeedbackEntry(tag, item));
            if (!Groups.TryGetValue(tag, out var rows)) continue;
            rows.Rows.Add(new SetupFeedbackItemRow(item.Title, $"{item.User} | {Lang.Date(item.Time)}",
                Common.PathImage + rows.Icon, item.Type, item));
        }

        Items = grouped;
        ShowProcessing = Has(PageSetupFeedback.TagId.Processing);
        ShowWaitingProcess = Has(PageSetupFeedback.TagId.WaitingProcess);
        ShowWait = Has(PageSetupFeedback.TagId.Wait);
        ShowPause = Has(PageSetupFeedback.TagId.Pause);
        ShowUpnext = Has(PageSetupFeedback.TagId.Upnext);
        ShowCompleted = Has(PageSetupFeedback.TagId.Completed);
        ShowDecline = Has(PageSetupFeedback.TagId.Decline);
        ShowIgnored = Has(PageSetupFeedback.TagId.Ignored);
        ShowDuplicate = Has(PageSetupFeedback.TagId.Duplicate);
    }

    private bool Has(PageSetupFeedback.TagId status)
    {
        var key = ((long)status).ToString();
        return Items.Any(entry => entry.Tag == key);
    }
}

/// <summary>一条已按状态归类的反馈条目。</summary>
public class SetupFeedbackEntry(string tag, PageSetupFeedback.Feedback feedback)
{
    public string Tag { get; } = tag;

    public PageSetupFeedback.Feedback Feedback { get; } = feedback;
}

/// <summary>反馈列表中的一行显示数据。</summary>
public class SetupFeedbackItemRow(
    string title,
    string info,
    string logo,
    string tags,
    PageSetupFeedback.Feedback feedback)
{
    public string Title { get; } = title;

    public string Info { get; } = info;

    public string Logo { get; } = logo;

    public string Tags { get; } = tags;

    public PageSetupFeedback.Feedback Feedback { get; } = feedback;
}

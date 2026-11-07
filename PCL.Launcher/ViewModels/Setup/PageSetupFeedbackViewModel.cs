using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;
using PCL.Network;

namespace PCL;

/// <summary>设置页面中「反馈」页的视图模型。</summary>
public partial class PageSetupFeedbackViewModel : ViewModelBase
{
    public enum TagId : long
    {
        Processing = 6820804544L,
        WaitingProcess = 6820804546L,
        Completed = 6820804547L,
        Decline = 6820804539L,
        Ignored = 8064650117L,
        Duplicate = 6820804541L,
        Wait = 8743070786L,
        Pause = 8558220235L,
        Upnext = 8550609020L
    }

    private Dictionary<string, (ObservableCollection<SetupFeedbackItemRow> Rows, string Icon)>?
        _groups;

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

    private Dictionary<string, (ObservableCollection<SetupFeedbackItemRow> Rows, string Icon)>
        Groups =>
        _groups ??=
            new Dictionary<string, (ObservableCollection<SetupFeedbackItemRow> Rows, string Icon)>
            {
                [((long)TagId.Processing).ToString()] = (ProcessingRows, "Blocks/CommandBlock.png"),
                [((long)TagId.WaitingProcess).ToString()] =
                    (WaitingProcessRows, "Blocks/RedstoneBlock.png"),
                [((long)TagId.Wait).ToString()] = (WaitRows, "Blocks/Anvil.png"),
                [((long)TagId.Pause).ToString()] = (PauseRows, "Blocks/RedstoneLampOff.png"),
                [((long)TagId.Upnext).ToString()] = (UpnextRows, "Blocks/RedstoneLampOn.png"),
                [((long)TagId.Completed).ToString()] = (CompletedRows, "Blocks/Grass.png"),
                [((long)TagId.Decline).ToString()] = (DeclineRows, "Blocks/CobbleStone.png"),
                [((long)TagId.Ignored).ToString()] = (IgnoredRows, "Blocks/CobbleStone.png"),
                [((long)TagId.Duplicate).ToString()] = (DuplicateRows, "Blocks/CobbleStone.png")
            };

    public static List<FeedbackItem> FetchFeedbackList()
    {
        var json = Requester.FetchJson(
            "https://api.github.com/repos/PCL-Community/PCL2-CE/issues?state=all&sort=created&per_page=200",
            new RequestParam
            {
                Retries = 3,
                UseBrowserUserAgent = true
            });
        if (json is not JsonArray list)
            throw new InvalidOperationException(Lang.Text("Setup.Feedback.LoadFailed"));

        var result = new List<FeedbackItem>();
        foreach (var node in list)
        {
            if (node is not JsonObject issue) continue;
            if (issue["pull_request"] is not null) continue;

            var issueType = string.Empty;
            if (issue["type"] is JsonObject typeObject)
                issueType = typeObject["name"]?.ToString().ToLowerInvariant() ?? string.Empty;

            var labels = new List<string>();
            if (issue["labels"] is JsonArray labelArray)
                foreach (var label in labelArray)
                    if (label is JsonObject labelObject)
                        labels.Add(labelObject["id"]?.ToString() ?? string.Empty);

            result.Add(
                new FeedbackItem
                {
                    Id = issue["number"]?.ToString() ?? "?",
                    Title = issue["title"]?.ToString() ?? string.Empty,
                    User = issue["user"]?["login"]?.ToString() ?? string.Empty,
                    CreatedAt = DateTime.TryParse(
                        issue["created_at"]?.ToString(),
                        out var createdAt)
                        ? createdAt
                        : default,
                    Content = issue["body"]?.ToString() ?? string.Empty,
                    Url = issue["html_url"]?.ToString() ?? string.Empty,
                    LabelIds = labels,
                    Type = issueType
                });
        }

        return result;
    }

    public void SetItems(List<FeedbackItem>? source)
    {
        var tagKeys = Enum.GetValues<TagId>().Select(id => ((long)id).ToString()).ToHashSet();
        foreach (var group in Groups.Values)
            group.Rows.Clear();
        var grouped = new List<SetupFeedbackEntry>();
        foreach (var item in source ?? [])
        {
            var tag = item.LabelIds.Find(tagKeys.Contains);
            if (tag is null) continue;
            grouped.Add(new SetupFeedbackEntry(tag, item));
            if (!Groups.TryGetValue(tag, out var rows)) continue;
            rows.Rows.Add(
                new SetupFeedbackItemRow(
                    $"#{item.Id} {item.Title}",
                    $"{item.User} | {Lang.Date(item.CreatedAt)}",
                    LauncherPaths.ImageRoot + rows.Icon,
                    item.Type,
                    item));
        }

        Items = grouped;
        ShowProcessing = Has(TagId.Processing);
        ShowWaitingProcess = Has(TagId.WaitingProcess);
        ShowWait = Has(TagId.Wait);
        ShowPause = Has(TagId.Pause);
        ShowUpnext = Has(TagId.Upnext);
        ShowCompleted = Has(TagId.Completed);
        ShowDecline = Has(TagId.Decline);
        ShowIgnored = Has(TagId.Ignored);
        ShowDuplicate = Has(TagId.Duplicate);
    }

    private bool Has(TagId status)
    {
        var key = ((long)status).ToString();
        return Items.Any(entry => entry.Tag == key);
    }
}

/// <summary>一条已按状态归类的反馈条目。</summary>
public sealed class SetupFeedbackEntry(string tag, FeedbackItem feedback)
{
    public string Tag { get; } = tag;

    public FeedbackItem Feedback { get; } = feedback;
}

/// <summary>反馈列表中的一行显示数据。</summary>
public sealed class SetupFeedbackItemRow(
    string title,
    string info,
    string logo,
    string tags,
    FeedbackItem feedback)
{
    public string Title { get; } = title;

    public string Info { get; } = info;

    public string Logo { get; } = logo;

    public string Tags { get; } = tags;

    public FeedbackItem Feedback { get; } = feedback;
}

/// <summary>一条 GitHub Issue 反馈。</summary>
public sealed class FeedbackItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string User { get; init; }
    public required string Content { get; init; }
    public required string Url { get; init; }
    public required List<string> LabelIds { get; init; }
    public required string Type { get; init; }
    public DateTime CreatedAt { get; init; }
}
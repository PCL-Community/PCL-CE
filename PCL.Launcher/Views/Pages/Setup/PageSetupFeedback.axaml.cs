using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Network;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Setup;

/// <summary>
///     反馈页：从 GitHub Issues 拉取社区反馈并按仓库标签状态分组展示；点击条目查看详情并可跳转原帖。
/// </summary>
public partial class PageSetupFeedback : MyPageRight, IRefreshable
{
    private const string ApiUrl =
        "https://api.github.com/repos/PCL-Community/PCL2-CE/issues?state=all&sort=created&per_page=100";

    private const string IssuesUrl = "https://github.com/PCL-Community/PCL2-CE/issues";

    private static readonly (string Tag, string TitleKey)[] StatusGroups =
    [
        ("6820804544", "Setup.Feedback.Status.Processing"),
        ("6820804546", "Setup.Feedback.Status.WaitingProcess"),
        ("8743070786", "Setup.Feedback.Status.Waiting"),
        ("8558220235", "Setup.Feedback.Status.Paused"),
        ("8550609020", "Setup.Feedback.Status.UpNext"),
        ("6820804547", "Setup.Feedback.Status.Completed"),
        ("6820804539", "Setup.Feedback.Status.Declined"),
        ("8064650117", "Setup.Feedback.Status.Ignored"),
        ("6820804541", "Setup.Feedback.Status.Duplicate")
    ];

    private readonly Loading.LoaderTask<bool, List<FeedbackItem>> _loader;
    private bool _loaded;

    public PageSetupFeedback()
    {
        InitializeComponent();
        PanScroll = PanBack;

        _loader = new Loading.LoaderTask<bool, List<FeedbackItem>>(
            "FeedbackList",
            LoadFeedbackList);
        BtnManageOpen.Click += (_, _) => Basics.OpenPath(IssuesUrl);

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            PageLoaderInit(Load, PanLoad, PanContent, null, _loader, _ => RefreshList());
        };
    }

    public void Refresh()
    {
        PageLoaderRestart();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        if (!_loaded) return;
        if (_loader.State is LoadState.Finished or LoadState.Failed)
            PageLoaderRestart(null, false);
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        if (_loaded && _loader.State == LoadState.Finished)
            RefreshList();
    }

    private static void LoadFeedbackList(Loading.LoaderTask<bool, List<FeedbackItem>> task)
    {
        task.output = FetchFeedbackList();
    }

    private static List<FeedbackItem> FetchFeedbackList()
    {
        var json = Requester.FetchJson(
            ApiUrl,
            new RequestParam
            {
                Retries = 2,
                UseBrowserUserAgent = true
            });
        if (json is not JsonArray list)
            throw new InvalidOperationException(Lang.Text("Setup.Feedback.LoadFailed"));

        var result = new List<FeedbackItem>();
        foreach (var node in list)
        {
            if (node is not JsonObject issue) continue;
            if (issue["pull_request"] is not null) continue;

            var labelIds = new List<string>();
            var labelNames = new List<string>();
            if (issue["labels"] is JsonArray labelArray)
                foreach (var label in labelArray)
                    if (label is JsonObject labelObject)
                    {
                        labelIds.Add(labelObject["id"]?.ToString() ?? string.Empty);
                        labelNames.Add(labelObject["name"]?.ToString() ?? string.Empty);
                    }

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
                    LabelIds = labelIds,
                    LabelNames = labelNames
                });
        }

        return result;
    }

    private void RefreshList()
    {
        PanContent.Children.Clear();
        var items = _loader.output ?? [];
        foreach (var (tag, titleKey) in StatusGroups)
        {
            var rows = items.Where(item => item.LabelIds.Contains(tag)).ToList();
            if (rows.Count == 0) continue;

            var card = new MyCard
            {
                Title = Lang.Text(titleKey),
                Margin = new Thickness(0, 0, 0, 15),
                CanSwap = true,
                IsSwapped = titleKey is "Setup.Feedback.Status.Completed"
                    or "Setup.Feedback.Status.Declined"
                    or "Setup.Feedback.Status.Ignored"
                    or "Setup.Feedback.Status.Duplicate"
            };
            var panel = new StackPanel
            {
                Margin = new Thickness(20, 36, 18, 18)
            };
            foreach (var item in rows)
            {
                // TODO(FIX-P3-15): WPF 的 FeedbackItemTemplate 还带 Logo（用户头像）与 Tag 结构（MyListItem
                // Title/Info/Logo/Tags/Clickable），
                // 当前只迁移了标题/副文本/标签，头像与标签配色待补。
                var row = new MyListItem
                {
                    Type = MyListItem.CheckType.Clickable,
                    Title = $"#{item.Id} {item.Title}",
                    Info = $"{item.User} | {Lang.Date(item.CreatedAt)}"
                };
                if (item.LabelNames.Count > 0)
                    row.PlainTags = item.LabelNames;
                row.Click += async (_, _) => await ShowDetailAsync(item);
                panel.Children.Add(row);
            }

            card.Children.Add(panel);
            PanContent.Children.Add(card);
        }
    }

    private static async Task ShowDetailAsync(FeedbackItem item)
    {
        var result = await DialogService.Instance.ShowAsync(
            new MarkdownDialogRequest
            {
                Title = $"#{item.Id} {item.Title}",
                Content =
                    Lang.Text("Setup.Feedback.Item.Submitter", item.User, Lang.Date(item.CreatedAt))
                    + "\n"
                    + Lang.Text("Setup.Feedback.Item.Type", string.Join(", ", item.LabelNames))
                    + "\n\n"
                    + item.Content,
                Buttons = DialogButtons.Create(
                    "Common.Action.Confirm",
                    "Setup.Feedback.Item.ViewDetail")
            });
        if (result.Button == 2)
            Basics.OpenPath(item.Url);
    }
}

/// <summary>一条 GitHub Issue 反馈。</summary>
internal sealed class FeedbackItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string User { get; init; }
    public required string Content { get; init; }
    public required string Url { get; init; }
    public required IReadOnlyList<string> LabelIds { get; init; }
    public required IReadOnlyList<string> LabelNames { get; init; }
    public DateTime CreatedAt { get; init; }
}
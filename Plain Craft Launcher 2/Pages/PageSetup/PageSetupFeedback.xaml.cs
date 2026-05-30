using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Network;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupFeedback
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

    private bool _isLoaded;

    public ModLoader.LoaderTask<bool, List<Feedback>> Loader;

    public PageSetupFeedback()
    {
        InitializeComponent();
        Loader = new ModLoader.LoaderTask<bool, List<Feedback>>("FeedbackList", FeedbackListGet);
        Loaded += PageOtherFeedback_Loaded;
    }

    private void PageOtherFeedback_Loaded(object sender, RoutedEventArgs e)
    {
        PageLoaderInit(Load, PanLoad, PanContent, PanInfo, Loader, _ => RefreshList());
        // 重复加载部分
        PanBack.ScrollToHome();
        // 非重复加载部分
        if (_isLoaded)
            return;
        _isLoaded = true;
    }

    public void FeedbackListGet(ModLoader.LoaderTask<bool, List<Feedback>> task)
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
        var res = new List<Feedback>();
        foreach (var i in list)
        {
            if (i is not JsonObject issue) continue;
            var pullRequestToken = issue["pull_request"];
            if (pullRequestToken is not null && pullRequestToken.GetValueKind() != JsonValueKind.Null) continue;

            var item = new Feedback
            {
                Title = issue["title"]!.ToString(),
                Url = issue["html_url"]!.ToString(),
                Content = issue["body"]?.ToString() ?? "",
                Time = DateTime.Parse(issue["created_at"]!.ToString()),
                User = issue["user"]!["login"]!.ToString(),
                Id = issue["number"]!.ToString(),
            };

            var issueType = Lang.Text("Setup.Feedback.Uncategorized");
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

        task.Output = res;
    }

    private MyListItem CreateFeedbackItem(Feedback item, string logo)
    {
        var li = new MyListItem
        {
            Title = item.Title,
            Type = MyListItem.CheckType.Clickable,
            Info = $"{item.User} | {Lang.Date(item.Time)}",
            Logo = ModBase.PathImage + logo,
            Tags = item.Type
        };

        li.Click += (_, _) => ShowFeedbackDetail(item);

        return li;
    }

    private void ShowFeedbackDetail(Feedback item)
    {
        var timeSpanText = Lang.TimeSpan(item.Time - DateTime.Now);
        switch (ModMain.MyMsgBoxMarkdown(
                    Lang.Text("Setup.Feedback.Item.Submitter", item.User, timeSpanText) + "\n" +
                    Lang.Text("Setup.Feedback.Item.Type", item.Type) + "\n\n" +
                    item.Content,
                    $"#{item.Id} {item.Title}", Button2: Lang.Text("Setup.Feedback.Item.ViewDetail")))
        {
            case 2:
            {
                ModBase.OpenWebsite(item.Url);
                break;
            }
        }
    }

    private void SetPanelVisibility(StackPanel panel, MyCard card)
    {
        card.Visibility = panel.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    public void RefreshList()
    {
        PanListProcessing.Children.Clear();
        PanListWaitingProcess.Children.Clear();
        PanListWait.Children.Clear();
        PanListPause.Children.Clear();
        PanListUpnext.Children.Clear();
        PanListCompleted.Children.Clear();
        PanListDecline.Children.Clear();
        PanListIgnored.Children.Clear();
        PanListDuplicate.Children.Clear();

        foreach (var item in Loader.Output)
        {
            if (item.Tags.Contains(((long)TagId.Processing).ToString()))
                PanListProcessing.Children.Add(CreateFeedbackItem(item, "Blocks/CommandBlock.png"));

            if (item.Tags.Contains(((long)TagId.WaitingProcess).ToString()))
                PanListWaitingProcess.Children.Add(CreateFeedbackItem(item, "Blocks/RedstoneBlock.png"));

            if (item.Tags.Contains(((long)TagId.Wait).ToString()))
                PanListWait.Children.Add(CreateFeedbackItem(item, "Blocks/Anvil.png"));

            if (item.Tags.Contains(((long)TagId.Pause).ToString()))
                PanListPause.Children.Add(CreateFeedbackItem(item, "Blocks/RedstoneLampOff.png"));

            if (item.Tags.Contains(((long)TagId.Upnext).ToString()))
                PanListUpnext.Children.Add(CreateFeedbackItem(item, "Blocks/RedstoneLampOn.png"));

            if (item.Tags.Contains(((long)TagId.Completed).ToString()))
                PanListCompleted.Children.Add(CreateFeedbackItem(item, "Blocks/Grass.png"));

            if (item.Tags.Contains(((long)TagId.Decline).ToString()))
                PanListDecline.Children.Add(CreateFeedbackItem(item, "Blocks/CobbleStone.png"));

            if (item.Tags.Contains(((long)TagId.Ignored).ToString()))
                PanListIgnored.Children.Add(CreateFeedbackItem(item, "Blocks/CobbleStone.png"));

            if (item.Tags.Contains(((long)TagId.Duplicate).ToString()))
                PanListDuplicate.Children.Add(CreateFeedbackItem(item, "Blocks/CobbleStone.png"));
        }

        SetPanelVisibility(PanListProcessing, PanContentProcessing);
        SetPanelVisibility(PanListWaitingProcess, PanContentWaitingProcess);
        SetPanelVisibility(PanListWait, PanContentWait);
        SetPanelVisibility(PanListPause, PanContentPause);
        SetPanelVisibility(PanListUpnext, PanContentUpnext);
        SetPanelVisibility(PanListCompleted, PanContentCompleted);
        SetPanelVisibility(PanListDecline, PanContentDecline);
        SetPanelVisibility(PanListIgnored, PanContentIgnored);
        SetPanelVisibility(PanListDuplicate, PanContentDuplicate);
    }

    private void Feedback_Click(object sender, MouseButtonEventArgs e)
    {
        PageSetupLeft.TryFeedback();
    }

    public class Feedback
    {
        public string User { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public DateTime Time { get; init; }
        public string Content { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string Id { get; init; } = string.Empty;
        public List<string> Tags { get; } = new();
        public string Type { get; set; } = string.Empty;
    }
}
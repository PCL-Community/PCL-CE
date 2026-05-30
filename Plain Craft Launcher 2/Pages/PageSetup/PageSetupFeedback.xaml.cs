using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupFeedback
{
    public enum TagID : long
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

    private new bool IsLoaded;

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
        if (IsLoaded)
            return;
        IsLoaded = true;
    }

    public void FeedbackListGet(ModLoader.LoaderTask<bool, List<Feedback>> Task)
    {
        JsonArray list;
        list = (JsonArray)Requester.FetchJson(
            "https://api.github.com/repos/PCL-Community/PCL2-CE/issues?state=all&sort=created&per_page=200",
            new RequestParam
            {
                Retries = 3,
                UseBrowserUserAgent = true
            }); // 获取近期 200 条数据就够了
        if (list is null)
            throw new Exception(Lang.Text("Setup.Feedback.LoadFailed"));
        var res = new List<Feedback>();
        foreach (JsonObject i in list)
        {
            var pullRequestToken = i["pull_request"];
            if (pullRequestToken is not null && pullRequestToken.GetValueKind() != JsonValueKind.Null) continue;

            var item = new Feedback
            {
                Title = i["title"].ToString(),
                Url = i["html_url"].ToString(),
                Content = i["body"].ToString(),
                Time = DateTime.Parse(i["created_at"].ToString()),
                User = i["user"]["login"].ToString(),
                ID = i["number"].ToString(),
                Open = i["state"].ToString().Equals("open"),
                IsPullRequest = false
            };

            var issueType = Lang.Text("Setup.Feedback.Uncategorized");
            var typeToken = i["type"];
            if (typeToken is not null && typeToken.GetValueKind() == JsonValueKind.Object)
            {
                var typeNameToken = typeToken["name"];
                if (typeNameToken is not null) issueType = typeNameToken.ToString().ToLower();
            }

            item.Type = issueType;

            var thisTags = (JsonArray)i["labels"];
            foreach (JsonObject thisTag in thisTags)
                item.Tags.Add(thisTag["id"].ToString());
            res.Add(item);
        }

        Task.Output = res;
    }

    private MyListItem CreateFeedbackItem(Feedback item, string logo)
    {
        var commonInfo = $"{item.User} | {Lang.Date(item.Time, "G")}";

        var li = new MyListItem();
        li.Title = item.Title;
        li.Type = MyListItem.CheckType.Clickable;
        li.Info = commonInfo;
        li.Logo = ModBase.PathImage + logo;
        li.Tags = item.Type;

        li.Click += (sender, e) => ShowFeedbackDetail(item);

        return li;
    }

    private void ShowFeedbackDetail(Feedback item)
    {
        var timeSpanText = Lang.TimeSpan(item.Time - DateTime.Now);
        switch (ModMain.MyMsgBoxMarkdown(
                    Lang.Text("Setup.Feedback.Item.Submitter", item.User, timeSpanText) + "\n" +
                    Lang.Text("Setup.Feedback.Item.Type", item.Type) + "\n\n" +
                    item.Content,
                    $"#{item.ID} {item.Title}", Button2: Lang.Text("Setup.Feedback.Item.ViewDetail")))
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
            if (item.Tags.Contains(((long)TagID.Processing).ToString()))
                PanListProcessing.Children.Add(CreateFeedbackItem(item, "Blocks/CommandBlock.png"));

            if (item.Tags.Contains(((long)TagID.WaitingProcess).ToString()))
                PanListWaitingProcess.Children.Add(CreateFeedbackItem(item, "Blocks/RedstoneBlock.png"));

            if (item.Tags.Contains(((long)TagID.Wait).ToString()))
                PanListWait.Children.Add(CreateFeedbackItem(item, "Blocks/Anvil.png"));

            if (item.Tags.Contains(((long)TagID.Pause).ToString()))
                PanListPause.Children.Add(CreateFeedbackItem(item, "Blocks/RedstoneLampOff.png"));

            if (item.Tags.Contains(((long)TagID.Upnext).ToString()))
                PanListUpnext.Children.Add(CreateFeedbackItem(item, "Blocks/RedstoneLampOn.png"));

            if (item.Tags.Contains(((long)TagID.Completed).ToString()))
                PanListCompleted.Children.Add(CreateFeedbackItem(item, "Blocks/Grass.png"));

            if (item.Tags.Contains(((long)TagID.Decline).ToString()))
                PanListDecline.Children.Add(CreateFeedbackItem(item, "Blocks/CobbleStone.png"));

            if (item.Tags.Contains(((long)TagID.Ignored).ToString()))
                PanListIgnored.Children.Add(CreateFeedbackItem(item, "Blocks/CobbleStone.png"));

            if (item.Tags.Contains(((long)TagID.Duplicate).ToString()))
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
        public string User { get; set; }
        public string Title { get; set; }
        public DateTime Time { get; set; }
        public string Content { get; set; }
        public string Url { get; set; }
        public string ID { get; set; }
        public List<string> Tags { get; set; } = new();
        public bool Open { get; set; } = true;
        public string Type { get; set; }
        public bool IsPullRequest { get; set; }
    }
}
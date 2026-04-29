using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json.Linq;
using PCL.Core.Utils;
using PCL.Network;

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
        JArray list;
        list = (JArray)Requester.FetchJson(
            "https://api.github.com/repos/PCL-Community/PCL2-CE/issues?state=all&sort=created&per_page=200",
            new RequestParam
            {
                Retries = 3,
                UseBrowserUserAgent = true
            }); // 获取近期 200 条数据就够了
        if (list is null)
            throw new Exception("无法获取到内容");
        var res = new List<Feedback>();
        foreach (JObject i in list)
        {
            var pullRequestToken = i["pull_request"];
            if (pullRequestToken is not null && pullRequestToken.Type != JTokenType.Null) continue;

            var item = new Feedback
            {
                Title = i["title"].ToString(),
                Url = i["html_url"].ToString(),
                Content = i["body"].ToString(),
                Time = DateTime.Parse(i["created_at"].ToString()),
                User = i["user"]["login"].ToString(),
                ID = (string)i["number"],
                Open = i["state"].ToString().Equals("open"),
                IsPullRequest = false
            };

            var issueType = "未分类";
            var typeToken = i["type"];
            if (typeToken is not null && typeToken.Type == JTokenType.Object)
            {
                var typeNameToken = typeToken["name"];
                if (typeNameToken is not null) issueType = typeNameToken.ToString().ToLower();
            }

            item.Type = issueType;

            var thisTags = (JArray)i["labels"];
            foreach (JObject thisTag in thisTags)
                item.Tags.Add((string)thisTag["id"]);
            res.Add(item);
        }

        Task.Output = res;
    }

    private MyListItem CreateFeedbackItem(Feedback item, string logo)
    {
        var commonInfo = $"{item.User} | {item.Time:yyyy-MM-dd HH:mm:ss}";

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
        var timeSpanText = TimeUtils.GetTimeSpanString(item.Time - DateTime.Now, false);
        switch (ModMain.MyMsgBoxMarkdown(
                    $"""
                     提交者：{item.User}（{timeSpanText}）
                     类型：{item.Type}

                     {item.Content}
                     """, $"#{item.ID} {item.Title}", Button2: "查看详情"))
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
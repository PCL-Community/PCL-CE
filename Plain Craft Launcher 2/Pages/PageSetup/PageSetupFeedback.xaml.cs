using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.Json.Serialization;
using PCL.Network;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupFeedback : IPageView
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

    public Loading.LoaderTask<bool, List<Feedback>> Loader;

    private readonly PageSetupFeedbackViewModel _vm = new();
    private bool _refreshed;

    private static readonly Dictionary<string, (Func<PageSetupFeedback, StackPanel> Panel, string Icon)> TagMap = new()
    {
        [((long)TagId.Processing).ToString()] = (p => p.PanListProcessing, "Blocks/CommandBlock.png"),
        [((long)TagId.WaitingProcess).ToString()] = (p => p.PanListWaitingProcess, "Blocks/RedstoneBlock.png"),
        [((long)TagId.Wait).ToString()] = (p => p.PanListWait, "Blocks/Anvil.png"),
        [((long)TagId.Pause).ToString()] = (p => p.PanListPause, "Blocks/RedstoneLampOff.png"),
        [((long)TagId.Upnext).ToString()] = (p => p.PanListUpnext, "Blocks/RedstoneLampOn.png"),
        [((long)TagId.Completed).ToString()] = (p => p.PanListCompleted, "Blocks/Grass.png"),
        [((long)TagId.Decline).ToString()] = (p => p.PanListDecline, "Blocks/CobbleStone.png"),
        [((long)TagId.Ignored).ToString()] = (p => p.PanListIgnored, "Blocks/CobbleStone.png"),
        [((long)TagId.Duplicate).ToString()] = (p => p.PanListDuplicate, "Blocks/CobbleStone.png"),
    };

    public PageSetupFeedback()
    {
        InitializeComponent();
        DataContext = _vm;
        Loader = new Loading.LoaderTask<bool, List<Feedback>>("FeedbackList", FeedbackListGet);
        Loaded += PageOtherFeedback_Loaded;
        Unloaded += PageOtherFeedback_Unloaded;
    }

    public PageSetupFeedbackViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter) => Refresh();

    public void OnNavigatedFrom()
    {
    }

    private void PageOtherFeedback_Loaded(object sender, RoutedEventArgs e) => Refresh();

    private void PageOtherFeedback_Unloaded(object sender, RoutedEventArgs e) => _refreshed = false;

    private void Refresh()
    {
        PanBack.ScrollToHome();
        if (_refreshed)
            return;
        _refreshed = true;
        PageLoaderInit(Load, PanLoad, PanContent, PanInfo, Loader, _ => RefreshList());
    }

    public void FeedbackListGet(Loading.LoaderTask<bool, List<Feedback>> task)
    {
        task.output = _vm.FetchFeedbackList();
    }

    private MyListItem CreateFeedbackItem(Feedback item, string logo)
    {
        var li = new MyListItem
        {
            Title = item.Title,
            Type = MyListItem.CheckType.Clickable,
            Logo = Common.PathImage + logo,
            PlainTags = [item.TypeDisplay]
        };
        // Info 含日期格式化，按当前语言重新求值
        LocalizedText.TryBind(
            li,
            "Info",
            () => $"{item.User} | {Lang.Date(item.Time)}");

        li.Click += (_, _) => ShowFeedbackDetail(item);

        return li;
    }

    private void ShowFeedbackDetail(Feedback item)
    {
        var timeSpanText = Lang.TimeSpan(item.Time - DateTime.Now);
        switch (Launcher.MyMsgBoxMarkdown(
                    Lang.Text("Setup.Feedback.Item.Submitter", item.User, timeSpanText) + "\n" +
                    Lang.Text("Setup.Feedback.Item.Type", item.TypeDisplay) + "\n\n" +
                    item.Content,
                    $"#{item.Id} {item.Title}", button2: Lang.Text("Setup.Feedback.Item.ViewDetail")))
        {
            case 2:
            {
                Common.OpenWebsite(item.Url);
                break;
            }
        }
    }

    protected override void RefreshLocalization()
    {
        if (Loader.State == Common.LoadState.Finished)
            RefreshList();
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

        _vm.SetItems(Loader.output);

        foreach (var entry in _vm.Items)
        {
            if (!TagMap.TryGetValue(entry.Tag, out var target))
                continue;
            target.Panel(this).Children.Add(CreateFeedbackItem(entry.Feedback, target.Icon));
        }
    }

    private void Feedback_Click(object sender, MouseButtonEventArgs e)
    {
        PageSetupLeft.TryFeedback();
    }

    public class Feedback
    {
        [JsonIgnore]
        public string User { get; set; } = string.Empty;
        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;
        [JsonPropertyName("created_at")]
        public DateTime Time { get; init; }
        [JsonPropertyName("body")]
        public string Content { get; init; } = string.Empty;
        [JsonPropertyName("html_url")]
        public string Url { get; init; } = string.Empty;
        [JsonIgnore]
        public string Id { get; set; } = string.Empty;
        public List<string> Tags { get; } = new();
        /// <summary>
        ///     服务端返回的原始类型标识（例如 <c>bug</c>）。为空表示未分类。
        ///     该字段不参与本地化。
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        ///     当前语言下显示的类型文本。
        /// </summary>
        [JsonIgnore]
        public string TypeDisplay =>
            string.IsNullOrEmpty(Type) ? Lang.Text("Setup.Feedback.Uncategorized") : Type;
    }
}

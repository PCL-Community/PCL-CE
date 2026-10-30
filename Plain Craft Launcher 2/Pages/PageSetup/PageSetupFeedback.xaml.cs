using System.Windows;
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

    private void ShowFeedbackDetail(Feedback item)
    {
        var timeSpanText = Lang.TimeSpan(item.Time - DateTime.Now);
        switch (Launcher.MyMsgBoxMarkdown(
                    Lang.Text("Setup.Feedback.Item.Submitter", item.User, timeSpanText) + "\n" +
                    Lang.Text("Setup.Feedback.Item.Type", item.Type) + "\n\n" +
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

    public void RefreshList()
    {
        _vm.SetItems(Loader.output);
    }

    private void FeedbackItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not MyListItem { Tag: SetupFeedbackItemRow row })
            return;
        ShowFeedbackDetail(row.Feedback);
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
        public string Type { get; set; } = string.Empty;
    }
}

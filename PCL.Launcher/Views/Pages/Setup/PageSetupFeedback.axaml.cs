using Avalonia.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Setup;

/// <summary>
///     反馈页：从 GitHub Issues 拉取社区反馈并按仓库标签状态分组展示；点击条目查看详情并可跳转原帖。
///     数据分组与行内容由 <see cref="PageSetupFeedbackViewModel" /> 负责。
/// </summary>
public partial class PageSetupFeedback : MyPageRight, IRefreshable
{
    private const string IssuesUrl = "https://github.com/PCL-Community/PCL2-CE/issues";

    private readonly Loading.LoaderTask<bool, List<FeedbackItem>> _loader;
    private bool _loaded;

    public PageSetupFeedback()
    {
        InitializeComponent();
        PanScroll = PanBack;
        DataContext = ViewModel;

        _loader = new Loading.LoaderTask<bool, List<FeedbackItem>>(
            "FeedbackList",
            LoadFeedbackList);
        BtnManageOpen.Click += (_, _) => Basics.OpenPath(IssuesUrl);

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            PageLoaderInit(Load, PanLoad, PanContent, PanInfo, _loader, _ => RefreshList());
        };
    }

    public PageSetupFeedbackViewModel ViewModel { get; } = new();

    public void Refresh()
    {
        PageLoaderRestart();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
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
        task.output = PageSetupFeedbackViewModel.FetchFeedbackList();
    }

    private void RefreshList()
    {
        ViewModel.SetItems(_loader.output);
    }

    private async void FeedbackItem_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not MyListItem { Tag: SetupFeedbackItemRow row }) return;
        await ShowFeedbackDetailAsync(row.Feedback);
    }

    private static async Task ShowFeedbackDetailAsync(FeedbackItem item)
    {
        var result = await DialogService.Instance.ShowAsync(
            new MarkdownDialogRequest
            {
                Title = $"#{item.Id} {item.Title}",
                Content =
                    $"""
                     {Lang.Text(
                         "Setup.Feedback.Item.Submitter",
                         item.User,
                         Lang.TimeSpan(item.CreatedAt - DateTime.Now))}
                     {Lang.Text("Setup.Feedback.Item.Type", item.Type)}

                     {item.Content}
                     """,
                Buttons = DialogButtons.Create(
                    "Common.Action.Confirm",
                    "Setup.Feedback.Item.ViewDetail"),
                ForceWait = true
            });
        if (result.Button == 2)
            Basics.OpenPath(item.Url);
    }
}
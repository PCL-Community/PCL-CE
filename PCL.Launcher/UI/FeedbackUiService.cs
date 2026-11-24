using System.Globalization;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;

namespace PCL.UI;

/// <summary>将业务层日志等级映射到启动器的提示、弹窗与反馈流程。</summary>
internal static class UiLogBridge
{
    private static bool _installed;

    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        LauncherCommon.UiLogHandler = Handle;
    }

    private static void Handle(
        Exception? exception,
        string text,
        LauncherLogLevel level,
        string? title,
        string? userSummary)
    {
        switch (level)
        {
            case LauncherLogLevel.Hint:
                HintService.Show(Hints.Error(userSummary ?? text));
                break;

            case LauncherLogLevel.Msgbox:
                DialogService.Instance.ShowNotification(
                    new TextDialogRequest
                    {
                        Title = string.IsNullOrWhiteSpace(title)
                            ? Lang.Text("SystemDialog.Error.Title")
                            : title,
                        Content = userSummary ?? text,
                        IsWarn = true
                    });
                break;

            case LauncherLogLevel.Feedback:
                FeedbackUiService.Show(exception, text, userSummary, title, false);
                break;

            case LauncherLogLevel.Critical:
                FeedbackUiService.Show(exception, text, userSummary, title, true);
                break;
        }
    }
}

/// <summary>异常反馈提示与反馈入口。</summary>
public static class FeedbackUiService
{
    private static int _criticalTriggered;

    public static void Show(
        Exception? exception,
        string text,
        string? userSummary,
        string? title,
        bool critical)
    {
        _ = ShowAsync(exception, text, userSummary, title, critical);
    }

    private static async Task ShowAsync(
        Exception? exception,
        string text,
        string? userSummary,
        string? title,
        bool critical)
    {
        try
        {
            if (critical && Interlocked.Exchange(ref _criticalTriggered, 1) != 0)
            {
                Environment.Exit((int)LauncherCommon.ProcessReturnValues.Exception);
                return;
            }

            var message = userSummary ?? text;
            if (exception is not null && userSummary is null)
                message += Environment.NewLine + Environment.NewLine + exception.Message;

            var status = await Task.Run(UpdateManager.GetVersionStatus).ConfigureAwait(false);
            if (status != UpdateEnums.VersionStatus.Latest)
            {
                var (messageKey, actionKey) = status == UpdateEnums.VersionStatus.NotLatest
                    ? (
                        "Setup.Feedback.Unavailable.NotLatest.Message",
                        "Setup.Feedback.Unavailable.NotLatest.Action")
                    : (
                        "Setup.Feedback.Unavailable.CheckFailed.Message",
                        "Setup.Feedback.Unavailable.CheckFailed.Action");

                var unavailable = await DialogService
                    .Instance.ShowAsync(
                        new TextDialogRequest
                        {
                            Title = Lang.Text("Setup.Feedback.Unavailable.Title"),
                            Content = Lang.Text(messageKey),
                            Buttons = DialogButtons.From(
                                DialogButton.Create(Lang.Text(actionKey)),
                                DialogButton.Create(Lang.Text("Common.Action.Cancel"))),
                            IsWarn = true
                        })
                    .ConfigureAwait(true);
                if (unavailable.IsConfirmed)
                    PageRouter.Navigate(PageIds.Setup, SubPageIds.SetupUpdate);
                return;
            }

            var prompt = await DialogService
                .Instance.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = string.IsNullOrWhiteSpace(title)
                            ? Lang.Text("SystemDialog.Error.Title")
                            : title,
                        Content = Lang.Text(
                            "Setup.Feedback.ErrorPrompt.Submit.Message",
                            message),
                        Buttons = DialogButtons.From(
                            DialogButton.Create(
                                Lang.Text("Setup.Feedback.ErrorPrompt.Submit.Action")),
                            DialogButton.Create(Lang.Text("Common.Action.Cancel"))),
                        IsWarn = true
                    })
                .ConfigureAwait(true);
            if (!prompt.IsConfirmed) return;

            var reminder = await DialogService
                .Instance.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Setup.Feedback.Reminder.Title"),
                        Content = Lang.Text(
                            "Setup.Feedback.Reminder.Message",
                            DateTime.Now.ToString("yyyy-M-dd", CultureInfo.InvariantCulture)),
                        Buttons = DialogButtons.From(
                            DialogButton.Create(Lang.Text("Common.Action.OpenFolder")),
                            DialogButton.Create(Lang.Text("Setup.Feedback.Reminder.NotNeeded")))
                    })
                .ConfigureAwait(true);

            if (reminder.IsConfirmed)
            {
                var logFolder = Path.Combine(Paths.Data, "Log");
                Directory.CreateDirectory(logFolder);
                Basics.OpenPath(logFolder);
            }

            PageRouter.Navigate(PageIds.Setup, SubPageIds.SetupFeedback);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "UI", "显示反馈提示失败");
            HintService.Show(Hints.Error(userSummary ?? text));
        }
    }
}
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;

namespace PCL;

/// <summary>旧对话框/提示 API 到新 DialogService/HintService 的适配。</summary>
public static class DialogCompat
{
    public static int ShowMessageBox(
        string text,
        string? title = null,
        string? button1 = null,
        string? button2 = "",
        string? button3 = "",
        bool isWarn = false,
        bool forceWait = false,
        Action? button1Action = null,
        Action? button2Action = null,
        Action? button3Action = null)
    {
        var texts = new List<string>();
        if (!string.IsNullOrEmpty(button1)) texts.Add(button1);
        if (!string.IsNullOrEmpty(button2)) texts.Add(button2);
        if (!string.IsNullOrEmpty(button3)) texts.Add(button3);
        if (texts.Count == 0) texts.Add(Lang.Text("Common.Action.Confirm"));

        var dialog = DialogService.Instance.Show(new TextDialogRequest
        {
            Title = title ?? "",
            Content = text,
            Buttons = DialogButtons.From(texts.ToArray()),
            IsWarn = isWarn,
            ForceWait = forceWait || texts.Count > 1
        });

        switch (dialog.Result?.Button)
        {
            case 1: button1Action?.Invoke(); break;
            case 2: button2Action?.Invoke(); break;
            case 3: button3Action?.Invoke(); break;
        }
        return dialog.Result?.Button ?? 0;
    }

    public static int? ShowSelect(
        string caption,
        string? title,
        IReadOnlyList<string> options,
        string? confirmText = null,
        string? cancelText = null)
    {
        var dialog = DialogService.Instance.Show(new SelectDialogRequest
        {
            Title = title ?? caption,
            Options = [.. options.Select(option => new DialogOption { Text = option })],
            Buttons = BuildButtons(confirmText, cancelText),
            ForceWait = true
        });
        return dialog.Result?.SelectedIndex;
    }

    public static List<int>? ShowMultiSelect(
        string caption,
        string? title,
        IReadOnlyList<string> options,
        string? confirmText = null,
        string? cancelText = null)
    {
        var dialog = DialogService.Instance.Show(new SelectDialogRequest
        {
            Title = title ?? caption,
            Options = [.. options.Select(option => new DialogOption { Text = option })],
            MultiSelect = true,
            Buttons = BuildButtons(confirmText, cancelText),
            ForceWait = true
        });
        return dialog.Result?.SelectedIndices?.ToList();
    }

    public static string? ShowInput(string title, string text = "", string defaultInput = "")
    {
        var dialog = DialogService.Instance.Show(new InputDialogRequest
        {
            Title = title,
            Content = text,
            DefaultValue = defaultInput,
            Buttons = DialogButtons.OkCancel,
            ForceWait = true
        });
        return dialog.Result?.InputText;
    }

    public static void ShowHint(string text, NotificationType type = NotificationType.Info)
    {
        HintService.Show(type switch
        {
            NotificationType.Success => Hints.Success(text),
            NotificationType.Warning => Hints.Warning(text),
            NotificationType.Error => Hints.Error(text),
            _ => Hints.Info(text)
        });
    }

    private static DialogButtons BuildButtons(string? confirmText, string? cancelText)
    {
        if (string.IsNullOrEmpty(confirmText)) return DialogButtons.Ok;
        return string.IsNullOrEmpty(cancelText)
            ? DialogButtons.From(confirmText)
            : DialogButtons.From(confirmText, cancelText);
    }
}

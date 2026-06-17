using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.VisualBasic;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

public class DialogManager
{
    public static DialogManager? Instance { get; private set; }

    private readonly FormMain _mainForm;

    public DialogManager(FormMain mainForm)
    {
        _mainForm = mainForm;
        Instance = this;
    }

    // -- new API --

    public int Show(DialogContext context)
    {
        if (context.Buttons.Count == 0)
            context.Buttons.Add(DialogButton.Confirm());
        context.Block = true;
        ShowOnUi(context);
        return context.Result;
    }

    public Task<int> ShowAsync(DialogContext context)
    {
        if (context.Buttons.Count == 0)
            context.Buttons.Add(DialogButton.Confirm());
        var tcs = new TaskCompletionSource<int>();
        context.Block = false;
        context.OnClosed = result => tcs.TrySetResult(result);
        ShowOnUi(context);
        return tcs.Task;
    }

    // -- convenience methods --

    public int ShowText(string caption, string? title = null,
        DialogTheme theme = DialogTheme.Info, bool block = true,
        params string[] buttons)
    {
        return Show(new DialogContext
        {
            Caption = caption,
            Title = title ?? Lang.Text("Common.Dialog.Title"),
            Theme = theme,
            Block = block,
            Buttons = _BuildButtons(buttons),
        });
    }

    public int ShowMarkdown(string markdown, string? title = null,
        DialogTheme theme = DialogTheme.Info, bool block = true,
        params string[] buttons)
    {
        var ctx = new DialogContext
        {
            Caption = markdown,
            Title = title ?? Lang.Text("Common.Dialog.Title"),
            Theme = theme,
            Block = block,
            Buttons = _BuildButtons(buttons),
            Content = new Markdig.Wpf.MarkdownViewer { Markdown = markdown },
        };
        return Show(ctx);
    }

    public string ShowInput(string title, string text = "", string defaultInput = "",
        Collection<FluentValidation.IValidator<string>>? validateRules = null,
        string hintText = "", string? button1 = null, string? button2 = null, bool isWarn = false)
    {
        button1 ??= Lang.Text("Common.Action.Confirm");
        button2 ??= Lang.Text("Common.Action.Cancel");
        string? result = null;

        Action showOnUi = () =>
        {
            var stack = new StackPanel();
            if (!string.IsNullOrEmpty(text))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 15,
                    Margin = new Thickness(0, 0, 0, 7),
                });
            }
            var textBox = new MyTextBox
            {
                Text = defaultInput, HintText = hintText,
                ValidateRules = validateRules ?? [], MinWidth = 450,
            };
            stack.Children.Add(textBox);

            var dialog = new DialogControl { Title = title, IsWarn = isWarn, DialogContent = stack };
            dialog.AddButton(button1!, onClick: () =>
            {
                textBox.Validate();
                if (!textBox.IsValidated) return;
                result = textBox.Text;
                dialog.Close(1);
            }, isPrimary: true, id: 1);
            if (!string.IsNullOrEmpty(button2))
            {
                dialog.AddButton(button2!, onClick: () =>
                {
                    result = null;
                    dialog.Close(2);
                }, id: 2);
            }

            _mainForm.PanMsg.Children.Add(dialog);
            try
            {
                _mainForm.DragStop();
                ComponentDispatcher.PushModal();
                Dispatcher.PushFrame(dialog.WaitFrame);
            }
            finally
            {
                ComponentDispatcher.PopModal();
            }
        };

        try { _mainForm.Dispatcher.Invoke(showOnUi); }
        catch (TaskCanceledException) { }
        return result ?? "";
    }

    public int? ShowSelect(List<IMyRadio> selections, string? title = null,
        string? button1 = null, string? button2 = "", bool isWarn = false)
    {
        title ??= Lang.Text("Common.Dialog.Title");
        button1 ??= Lang.Text("Common.Action.Confirm");
        button2 ??= "";
        int? result = null;

        Action showOnUi = () =>
        {
            var panel = new StackPanel();
            var dialog = new DialogControl { Title = title, IsWarn = isWarn, DialogContent = panel };

            var b1 = dialog.AddButton(button1!, onClick: () => { }, isPrimary: true, id: 1);
            b1.IsEnabled = false;

            if (!string.IsNullOrEmpty(button2))
            {
                dialog.AddButton(button2!, onClick: () =>
                {
                    result = null;
                    dialog.Close(2);
                }, id: 2);
            }

            int selectedIndex = -1;
            for (var i = 0; i < selections.Count; i++)
            {
                var raw = selections[i];
                var item = MyVirtualizingElement.TryInit((FrameworkElement)raw);
                if (item is IMyRadio radio)
                {
                    if (item is MyListItem listItem) { listItem.Type = MyListItem.CheckType.RadioBox; listItem.MinHeight = 24; }
                    else if (item is MyRadioBox radioBox) { radioBox.MinHeight = 24; }

                    var idx = i;
                    radio.Check += (_, _) => { selectedIndex = idx; b1.IsEnabled = true; };
                    panel.Children.Add((UIElement)radio);
                }
            }

            b1.Click += (_, _) =>
            {
                if (selectedIndex < 0) return;
                result = selectedIndex;
                dialog.Close(1);
            };

            _mainForm.PanMsg.Children.Add(dialog);
            try
            {
                if (_mainForm is not null) _mainForm.DragStop();
                ComponentDispatcher.PushModal();
                Dispatcher.PushFrame(dialog.WaitFrame);
            }
            finally
            {
                ComponentDispatcher.PopModal();
            }
        };

        try { _mainForm.Dispatcher.Invoke(showOnUi); }
        catch (TaskCanceledException) { }
        return result;
    }

    // -- legacy bridge: called by MsgBoxWrapper via ModMain.MsgBoxWrapper_OnShow --

    public int ShowLegacy(string message, string caption, ICollection<MsgBoxButtonInfo> buttons,
        MsgBoxTheme theme, bool block)
    {
        var ctx = new DialogContext
        {
            Caption = message,
            Title = caption,
            Theme = (DialogTheme)(int)theme,
            Block = block,
            Buttons = new Collection<DialogButton>(),
        };
        foreach (var btn in buttons)
            ctx.Buttons.Add(new DialogButton(btn.Context, btn.OnClick));
        return Show(ctx);
    }

    // -- tick: called from ModMain timer --

    public void Tick()
    {
        try
        {
            if (_mainForm is null || _mainForm.PanMsg is null) return;
            if (_mainForm.PanMsg.Children.Count > 0)
                _mainForm.PanMsgBackground.Visibility = Visibility.Visible;
            else if (_mainForm.PanMsgBackground.Visibility != Visibility.Collapsed)
                _mainForm.PanMsgBackground.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "对话框 Tick 失败", ModBase.LogLevel.Feedback);
        }
    }

    // -- internal show logic --

    private void ShowOnUi(DialogContext context)
    {
        if (_mainForm is null || _mainForm.Dispatcher.HasShutdownStarted)
        {
            Interaction.MsgBox(context.Caption, MsgBoxStyle.OkOnly, context.Title);
            context.Result = 1;
            context.OnClosed?.Invoke(1);
            return;
        }

        if (_mainForm.Dispatcher.CheckAccess())
            ShowDialogOnUiThread(context);
        else
            try { _mainForm.Dispatcher.Invoke(() => ShowDialogOnUiThread(context)); }
            catch (TaskCanceledException) { }
    }

    private void ShowDialogOnUiThread(DialogContext context)
    {
        if (_mainForm?.PanMsg is null) return;

        var isWarn = context.Theme == DialogTheme.Warning || context.Theme == DialogTheme.Error;

        var content = context.Content;
        if (content is null && !string.IsNullOrEmpty(context.Caption))
        {
            content = new TextBlock
            {
                Text = context.Caption,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15,
            };
        }

        var dialog = new DialogControl
        {
            Title = context.Title,
            IsWarn = isWarn,
            DialogContent = content as UIElement,
        };

        for (var i = 0; i < context.Buttons.Count && i < 3; i++)
        {
            var btn = context.Buttons[i];
            var isPrimary = i == 0 || btn.IsPrimary;
            var id = btn.Id > 0 ? btn.Id : i + 1;
            dialog.AddButton(btn.Text, btn.OnClick, isPrimary, id);
        }

        _mainForm.PanMsg.Children.Add(dialog);

        if (context.Block)
        {
            try
            {
                _mainForm.DragStop();
                ComponentDispatcher.PushModal();
                Dispatcher.PushFrame(dialog.WaitFrame);
            }
            finally
            {
                ComponentDispatcher.PopModal();
            }
            context.Result = dialog.Result;
            context.OnClosed?.Invoke(context.Result);
        }
        else
        {
            dialog.OnClosed += result =>
            {
                context.Result = result;
                context.OnClosed?.Invoke(result);
            };
        }
    }

    private static Collection<DialogButton> _BuildButtons(string[] buttonTexts)
    {
        var list = new Collection<DialogButton>();
        for (var i = 0; i < buttonTexts.Length; i++)
            list.Add(new DialogButton(buttonTexts[i], isPrimary: i == 0, id: i + 1));
        return list;
    }
}

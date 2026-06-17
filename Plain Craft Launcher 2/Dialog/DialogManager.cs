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
        return Show(new DialogContext
        {
            Caption = markdown,
            Title = title ?? Lang.Text("Common.Dialog.Title"),
            Theme = theme,
            Block = block,
            Buttons = _BuildButtons(buttons),
            // Markdown content is created in ShowOnUi via CreateDialogFromContext
        });
    }

    public string ShowInput(string title, string text = "", string defaultInput = "",
        Collection<FluentValidation.IValidator<string>>? validateRules = null,
        string hintText = "", string? button1 = null, string? button2 = null, bool isWarn = false)
    {
        button1 ??= Lang.Text("Common.Action.Confirm");
        button2 ??= Lang.Text("Common.Action.Cancel");
        var converter = new ModMain.MyMsgBoxConverter
        {
            Text = text,
            HintText = hintText,
            Type = ModMain.MyMsgBoxType.Input,
            ValidateRules = validateRules ?? [],
            Button1 = button1,
            Button2 = button2,
            Content = defaultInput,
            IsWarn = isWarn,
            Title = title,
        };
        ModMain.WaitingMyMsgBox.Add(converter);
        try
        {
            if (_mainForm is not null)
                _mainForm.DragStop();
            ComponentDispatcher.PushModal();
            Dispatcher.PushFrame(converter.WaitFrame);
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }
        return converter.Result?.ToString() ?? "";
    }

    public int? ShowSelect(List<IMyRadio> selections, string? title = null,
        string? button1 = null, string? button2 = "", bool isWarn = false)
    {
        title ??= Lang.Text("Common.Dialog.Title");
        button1 ??= Lang.Text("Common.Action.Confirm");
        button2 ??= "";
        var converter = new ModMain.MyMsgBoxConverter
        {
            Type = ModMain.MyMsgBoxType.Select,
            Button1 = button1,
            Button2 = button2,
            Content = selections,
            IsWarn = isWarn,
            Title = title,
        };
        ModMain.WaitingMyMsgBox.Add(converter);
        try
        {
            if (_mainForm is not null)
                _mainForm.DragStop();
            ComponentDispatcher.PushModal();
            Dispatcher.PushFrame(converter.WaitFrame);
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }
        return (int?)converter.Result;
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
            if (_mainForm is null || _mainForm.PanMsg is null || _mainForm.WindowState == WindowState.Minimized)
                return;
            if (_mainForm.PanMsg.Children.Count > 0)
            {
                _mainForm.PanMsgBackground.Visibility = Visibility.Visible;
            }
            else if (ModMain.WaitingMyMsgBox.Any())
            {
                _mainForm.PanMsgBackground.Visibility = Visibility.Visible;
                var converter = ModMain.WaitingMyMsgBox[0];
                var dialog = CreateDialogFromConverter(converter);
                if (dialog is not null)
                    _mainForm.PanMsg.Children.Add(dialog);
                ModMain.WaitingMyMsgBox.RemoveAt(0);
            }
            else if (_mainForm.PanMsgBackground.Visibility != Visibility.Collapsed)
            {
                _mainForm.PanMsgBackground.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "对话框 Tick 失败", ModBase.LogLevel.Feedback);
        }
    }

    // -- converter helpers (legacy queue → DialogControl) --

    private static DialogControl? CreateDialogFromConverter(ModMain.MyMsgBoxConverter conv)
    {
        switch (conv.Type)
        {
            case ModMain.MyMsgBoxType.Input:
                return CreateInputDialog(conv);
            case ModMain.MyMsgBoxType.Select:
                return CreateSelectDialog(conv);
            case ModMain.MyMsgBoxType.Login:
                Instance?._mainForm?.PanMsg.Children.Add(new MyMsgLogin(conv));
                return null;
            case ModMain.MyMsgBoxType.Markdown:
            {
                var mdViewer = new Markdig.Wpf.MarkdownViewer { Markdown = conv.Text };
                return CreateStandardDialog(conv, mdViewer);
            }
            default:
            {
                var content = conv.Content as UIElement
                    ?? new TextBlock { Text = conv.Text, TextWrapping = TextWrapping.Wrap, FontSize = 15 };
                return CreateStandardDialog(conv, content);
            }
        }
    }

    private static DialogControl CreateStandardDialog(ModMain.MyMsgBoxConverter conv, UIElement content)
    {
        var dialog = new DialogControl
        {
            Title = conv.Title,
            IsWarn = conv.IsWarn,
            DialogContent = content,
        };
        dialog.AddButton(conv.Button1, conv.Button1Action, isPrimary: true, id: conv.ButtonIds[0]);
        if (!string.IsNullOrEmpty(conv.Button2))
            dialog.AddButton(conv.Button2, conv.Button2Action, id: conv.ButtonIds[1]);
        if (!string.IsNullOrEmpty(conv.Button3))
            dialog.AddButton(conv.Button3, conv.Button3Action, id: conv.ButtonIds[2]);
        conv.WaitFrame = dialog.WaitFrame;
        dialog.OnClosed += result =>
        {
            conv.Result = result;
            conv.OnCloseCallback?.Invoke(result);
        };
        return dialog;
    }

    private static DialogControl CreateInputDialog(ModMain.MyMsgBoxConverter conv)
    {
        var stack = new StackPanel();
        if (!string.IsNullOrEmpty(conv.Text))
        {
            stack.Children.Add(new TextBlock
            {
                Text = conv.Text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15,
                Margin = new Thickness(0, 0, 0, 7),
            });
        }

        var textBox = new MyTextBox
        {
            Text = (string)(conv.Content ?? ""),
            HintText = conv.HintText,
            ValidateRules = conv.ValidateRules ?? [],
            MinWidth = 450,
        };
        stack.Children.Add(textBox);

        var dialog = new DialogControl
        {
            Title = conv.Title,
            IsWarn = conv.IsWarn,
            DialogContent = stack,
        };

        dialog.AddButton(conv.Button1, onClick: () =>
        {
            textBox.Validate();
            if (!textBox.IsValidated) return;
            conv.Result = textBox.Text;
            dialog.Close(conv.ButtonIds[0]);
        }, isPrimary: true, id: conv.ButtonIds[0]);

        if (!string.IsNullOrEmpty(conv.Button2))
        {
            dialog.AddButton(conv.Button2, onClick: () =>
            {
                conv.Result = null;
                dialog.Close(conv.ButtonIds[1]);
            }, id: conv.ButtonIds[1]);
        }

        conv.WaitFrame = dialog.WaitFrame;
        return dialog;
    }

    private static DialogControl CreateSelectDialog(ModMain.MyMsgBoxConverter conv)
    {
        var panel = new StackPanel();
        var dialog = new DialogControl
        {
            Title = conv.Title,
            IsWarn = conv.IsWarn,
            DialogContent = panel,
        };

        var b1 = dialog.AddButton(conv.Button1, onClick: () =>
        {
            // handled by extra Click below
        }, isPrimary: true, id: conv.ButtonIds[0]);
        b1.IsEnabled = false;

        if (!string.IsNullOrEmpty(conv.Button2))
        {
            dialog.AddButton(conv.Button2, onClick: () =>
            {
                conv.Result = null;
                dialog.Close(conv.ButtonIds[1]);
            }, id: conv.ButtonIds[1]);
        }

        var selectedIndex = -1;
        var index = 0;
        foreach (var raw in (IEnumerable)conv.Content!)
        {
            var item = MyVirtualizingElement.TryInit((FrameworkElement)raw);
            if (item is IMyRadio radio)
            {
                if (item is MyListItem listItem)
                {
                    listItem.Type = MyListItem.CheckType.RadioBox;
                    listItem.MinHeight = 24;
                }
                else if (item is MyRadioBox radioBox)
                {
                    radioBox.MinHeight = 24;
                }

                var currentIndex = index;
                radio.Check += (_, _) =>
                {
                    selectedIndex = currentIndex;
                    b1.IsEnabled = true;
                };
                panel.Children.Add((UIElement)radio);
                index++;
            }
        }

        // Override Btn1's dummy onClick with real logic
        b1.Click += (_, _) =>
        {
            if (selectedIndex < 0) return;
            conv.Result = selectedIndex;
            dialog.Close(conv.ButtonIds[0]);
        };

        conv.WaitFrame = dialog.WaitFrame;
        return dialog;
    }

    // -- internal show logic --

    private void ShowOnUi(DialogContext context)
    {
        if (_mainForm is null)
        {
            Interaction.MsgBox(context.Caption, MsgBoxStyle.OkOnly, context.Title);
            context.Result = 1;
            context.OnClosed?.Invoke(1);
            return;
        }

        if (_mainForm.Dispatcher.CheckAccess())
            ShowDialogOnUiThread(context);
        else
            _mainForm.Dispatcher.Invoke(() => ShowDialogOnUiThread(context));
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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualBasic;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;

namespace PCL;

/// <summary>将 Core 的弹窗状态呈现为界面控件。仅负责显示，不参与结果判定。</summary>
public sealed class DialogPresenter : IDialogPresenter
{
    public static DialogPresenter Instance { get; } = new();

    public static void Install() => PCL.Core.UI.Dialogs.DialogService.Instance.Presenter = Instance;

    public bool IsAvailable
    {
        get
        {
            var main = Launcher.frmMain;
            return main?.PanMsg is not null
                   && main.WindowState != WindowState.Minimized
                   && main.PanMsg.Children.Count == 0
                   && Launcher.WaitingMyMsgBox.Count == 0;
        }
    }

    public void Present(DialogViewModel dialog)
    {
        try
        {
            var converter = BuildConverter(dialog);
            UIElement? view = dialog.Kind switch
            {
                DialogKind.Input => new MyMsgInput(converter),
                DialogKind.Select => new MyMsgSelect(converter),
                DialogKind.AuthServer => new MyMsgAuthServer(converter),
                DialogKind.Text => new MyMsgText(converter),
                DialogKind.Login => new MyMsgLogin(converter),
                DialogKind.Markdown => new MyMsgMarkdown(converter),
                _ => null
            };

            if (view is null)
            {
                Common.Log("[Dialog] 未知的弹窗类型：" + dialog.Kind, Common.LogLevel.Debug);
                dialog.Close(DialogResult.None);
                return;
            }

            var main = Launcher.frmMain!;
            main.PanMsgBackground.Visibility = Visibility.Visible;
            main.PanMsg.Children.Add(view);

            converter.Completion.Task.ContinueWith(_ => OnConverterClosed(dialog, converter), TaskScheduler.Default);
            converter.CompletionObject.Task.ContinueWith(_ => OnConverterClosed(dialog, converter), TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "弹窗呈现失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Application.Control.MessageBox.Error.OperationFailed"));
            dialog.Close(DialogResult.None);
        }
    }

    public void Dismiss(DialogViewModel dialog)
    {
        // 控件在关闭动画结束后自行从 PanMsg 移除，此处无需额外处理。
    }

    public void ShowFallback(DialogViewModel dialog)
    {
        try
        {
            var request = dialog.Request;
            var isWarn = request.IsWarn;
            var icon = isWarn ? MsgBoxStyle.Critical : MsgBoxStyle.Question;
            var length = request.Buttons.Length;

            if (length <= 1)
            {
                Interaction.MsgBox(ContentText(request), (MsgBoxStyle)((int)MsgBoxStyle.OkOnly + (int)icon),
                    request.Title);
                dialog.Close(new DialogResult(1));
                return;
            }

            var style = length > 2 ? MsgBoxStyle.YesNoCancel : MsgBoxStyle.YesNo;
            var raw = Interaction.MsgBox(ContentText(request), (MsgBoxStyle)((int)style + (int)icon), request.Title);
            var button = raw switch
            {
                MsgBoxResult.Yes => 1,
                MsgBoxResult.No => 2,
                MsgBoxResult.Cancel => 3,
                _ => 0
            };
            dialog.Close(new DialogResult(button));
        }
        catch (Exception ex)
        {
            Common.Log(ex, "弹窗兜底显示失败", Common.LogLevel.Debug);
            dialog.Close(DialogResult.None);
        }
    }

    private static string ContentText(DialogRequest request) => request switch
    {
        TextDialogRequest text => text.Content,
        MarkdownDialogRequest markdown => markdown.Content,
        InputDialogRequest input => input.Content,
        _ => ""
    };

    private static void OnConverterClosed(DialogViewModel dialog, Launcher.MyMsgBoxConverter converter)
    {
        if (dialog.IsClosed) return;
        dialog.Close(MapResult(dialog, converter));
    }

    private static DialogResult MapResult(DialogViewModel dialog, Launcher.MyMsgBoxConverter converter)
    {
        var result = converter.Result;
        switch (dialog.Kind)
        {
            case DialogKind.Input:
                return result is string input
                    ? new DialogResult(1, InputText: input)
                    : new DialogResult(2);

            case DialogKind.Select when dialog.MultiSelect:
                return result is List<int> indices
                    ? new DialogResult(1, SelectedIndices: indices)
                    : new DialogResult(2);

            case DialogKind.Select:
                return result is int index
                    ? new DialogResult(1, SelectedIndex: index)
                    : new DialogResult(2);

            case DialogKind.AuthServer:
                return result is string server
                    ? new DialogResult(1, InputText: server)
                    : new DialogResult(2);

            default:
                return new DialogResult(result is int button ? button : 0);
        }
    }

    private static Launcher.MyMsgBoxConverter BuildConverter(DialogViewModel dialog)
    {
        var request = dialog.Request;
        var buttons = request.Buttons;
        var converter = new Launcher.MyMsgBoxConverter
        {
            Type = request.Kind switch
            {
                DialogKind.Markdown => Launcher.MyMsgBoxType.Markdown,
                DialogKind.Input => Launcher.MyMsgBoxType.Input,
                DialogKind.Select => Launcher.MyMsgBoxType.Select,
                DialogKind.AuthServer => Launcher.MyMsgBoxType.AuthServer,
                DialogKind.Login => Launcher.MyMsgBoxType.Login,
                _ => Launcher.MyMsgBoxType.Text
            },
            Title = request.Title,
            IsWarn = request.IsWarn,
            HighLight = request.HighLight,
            ForceWait = request.ForceWait,
            Button1 = buttons[0].Text ?? "",
            Button2 = buttons[1].Text ?? "",
            Button3 = buttons[2].Text ?? "",
            Button1Action = buttons[0].OnClick,
            Button2Action = buttons[1].OnClick,
            Button3Action = buttons[2].OnClick,
            WaitFrame = dialog.WaitFrame
        };

        switch (request)
        {
            case TextDialogRequest text:
                converter.Text = text.Content;
                break;
            case MarkdownDialogRequest markdown:
                converter.Text = markdown.Content;
                break;
            case InputDialogRequest input:
                converter.Text = input.Content;
                converter.Content = input.DefaultValue;
                converter.HintText = input.HintText;
                converter.ValidateRules = new System.Collections.ObjectModel.Collection<FluentValidation.IValidator<string>>(
                    new List<FluentValidation.IValidator<string>>(input.ValidateRules));
                break;
            case SelectDialogRequest select:
                converter.MultiSelect = select.MultiSelect;
                converter.Content = BuildSelectionControls(select);
                break;
            case AuthServerDialogRequest authServer:
                converter.AuthServerDefault = authServer.DefaultServer;
                converter.AuthServerPresets = authServer.Presets;
                break;
            case LoginDialogRequest login:
                converter.Content = login.Data;
                converter.DeviceCodePoll = login.DeviceCodePoll;
                converter.LoginResultHandler = login.LoginResultHandler;
                converter.CompletionHandler = login.CompletionHandler;
                break;
        }

        return converter;
    }

    private static List<IMyRadio> BuildSelectionControls(SelectDialogRequest request)
    {
        var list = new List<IMyRadio>(request.Options.Count);
        foreach (var option in request.Options)
        {
            if (request.MultiSelect || option.Description is not null || option.Image is not null)
            {
                list.Add(new MyListItem
                {
                    Title = option.Text,
                    Info = option.Description ?? "",
                    Logo = option.Image ?? "",
                    LogoScale = option.ImageScale,
                    Checked = option.IsChecked,
                    Type = request.MultiSelect ? MyListItem.CheckType.CheckBox : MyListItem.CheckType.RadioBox,
                    Tag = option.Tag
                });
            }
            else
            {
                list.Add(new MyRadioBox { Text = option.Text, Tag = option.Tag });
            }
        }

        return list;
    }
}

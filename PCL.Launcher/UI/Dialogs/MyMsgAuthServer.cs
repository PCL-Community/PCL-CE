using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils;
using PCL.UI.Controls;

namespace PCL.UI.Dialogs;

/// <summary>
///     验证服务器弹窗：可编辑下拉框给出预设服务器，也允许直接输入地址。
///     主按钮只接受 http/https 地址，地址无效时给出提示并保留弹窗。
/// </summary>
public sealed class MyMsgAuthServer : MyMsgDialogBase
{
    private readonly MyComboBox _server;

    public MyMsgAuthServer(DialogViewModel dialog) : base(dialog)
    {
        _server = new MyComboBox
        {
            IsEditable = true,
            MinHeight = 28d,
            Margin = new Thickness(7d, 0d, 7d, 12d),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = dialog.Presets.Keys.ToList(),
            Text = dialog.DefaultServer
        };
        _server.SelectionChanged += OnServerSelectionChanged;

        HideBody();
        SetFooter(_server);
    }

    private void OnServerSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_server.SelectedItem is not string key) return;
        if (!Dialog.Presets.TryGetValue(key, out var value)) return;

        Dispatcher.UIThread.Post(() =>
        {
            _server.SelectedItem = null;
            _server.Text = value;
        });
    }

    /// <inheritdoc />
    public override bool FocusInitial()
    {
        return _server.Focus() || base.FocusInitial();
    }

    /// <summary>主按钮校验服务器地址，取消按钮直接以取消结果收尾。</summary>
    protected override void HandleButton(int number)
    {
        switch (number)
        {
            case 1:
            {
                var server = _server.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(server) || !RegexPatterns.HttpUri.IsMatch(server))
                {
                    HintService.Show(Hints.Error(Lang.Text("Launch.Account.Auth.InvalidServer")));
                    return;
                }

                Dialog.InputText = server;
                Dialog.CloseByButton(1);
                return;
            }
            case 2:
                CloseAsCancelled();
                return;
            default:
                InvokeActionOrClose(number);
                return;
        }
    }
}
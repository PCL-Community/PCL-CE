using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PCL.Core.App.Localization;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     服务器卡片：内嵌 <see cref="MinecraftServer" /> 显示服务器图标、MotD、在线人数与延迟，
///     右侧提供加入、刷新、复制地址、编辑与移除按钮。
/// </summary>
public class ServerCard : MyCard
{
    private readonly MinecraftServer _server;
    private readonly TextBlock _nameText;
    private string _address;

    public ServerCard(string name, string address)
    {
        Name = name;
        _address = address;
        Margin = new Thickness(0d, 8d, 0d, 8d);

        _server = new MinecraftServer { Address = address };

        var textPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 12d, 0d)
        };
        _nameText = new TextBlock
        {
            Text = name,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
        };
        textPanel.Children.Add(_nameText);
        var addressText = new TextBlock
        {
            Text = address,
            Opacity = 0.6d,
            FontSize = 12d
        };
        textPanel.Children.Add(addressText);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 2d
        };
        buttons.Children.Add(CreateButton("lucide/play", "Instance.Server.Card.Connect", () => ConnectRequested?.Invoke(this)));
        buttons.Children.Add(CreateButton("lucide/refresh-cw", "Common.Action.Refresh", () => _ = RefreshAsync()));
        buttons.Children.Add(CreateButton("lucide/copy", "Instance.Server.Card.CopyAddress", () => CopyRequested?.Invoke(this)));
        buttons.Children.Add(CreateButton("lucide/pencil", "Instance.Server.Card.Edit", () => EditRequested?.Invoke(this)));
        buttons.Children.Add(CreateButton("lucide/trash-2", "Instance.Server.Card.Remove", () => RemoveRequested?.Invoke(this)));

        var root = new StackPanel();
        root.Children.Add(_server);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        footer.Children.Add(textPanel);
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);
        root.Children.Add(footer);
        BorderChild = root;
    }

    public new string Name { get; private set; }

    public string Address => _address;

    public event Action<ServerCard>? ConnectRequested;

    public event Action<ServerCard>? CopyRequested;

    public event Action<ServerCard>? EditRequested;

    public event Action<ServerCard>? RemoveRequested;

    private static MyIconButton CreateButton(string icon, string tooltipKey, Action onClick)
    {
        var button = new MyIconButton { SvgIcon = icon, LogoScale = 0.85d };
        ToolTip.SetTip(button, Lang.Text(tooltipKey));
        button.Click += (_, _) => onClick();
        return button;
    }

    public void UpdateInfo(string name, string address)
    {
        Name = name;
        _address = address;
        _nameText.Text = string.IsNullOrEmpty(name) ? address : name;
        _server.Address = address;
    }

    public Task RefreshAsync()
    {
        return _server.UpdateServerInfoAsync(_address);
    }
}

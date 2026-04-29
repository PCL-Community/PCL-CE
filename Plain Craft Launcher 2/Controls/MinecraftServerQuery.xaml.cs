using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public partial class MinecraftServerQuery : Grid
{
    public MinecraftServerQuery()
    {
        InitializeComponent();
        BtnServerQuery.Click += BtnServerQuery_Click;
    }
    private void BtnServerQuery_Click(object sender, MouseButtonEventArgs e)
    {
        Dispatcher.BeginInvoke(new Func<Task>(() => ServerQueryAsync()));
    }

    private async Task ServerQueryAsync()
    {
        await PanMcServer.UpdateServerInfoAsync(LabServerIp.Text);
        ServerInfo.Visibility = Visibility.Visible;
    }
}
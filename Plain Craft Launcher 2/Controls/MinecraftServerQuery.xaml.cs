using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public partial class MinecraftServerQuery : Grid
{
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
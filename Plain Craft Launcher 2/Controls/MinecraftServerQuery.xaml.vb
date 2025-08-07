Imports System.Net
Imports System.Net.Sockets
Imports System.Security.Policy
Imports System.Threading.Tasks
Imports PCL.Core.Link

Class MinecraftServerQuery
    Inherits Grid
    Private Async Sub BtnServerQuery_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnServerQuery.Click
        Await PanMcServer.UpdateServerInfoAsync(LabServerIp.Text)
        ServerInfo.Visibility = Visibility.Visible
    End Sub
End Class
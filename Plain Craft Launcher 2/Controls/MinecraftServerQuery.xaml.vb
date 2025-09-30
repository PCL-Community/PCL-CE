

Class MinecraftServerQuery
    Inherits Grid
    Private Async Function BtnServerQuery_Click(sender As Object, e As MouseButtonEventArgs) As Task Handles BtnServerQuery.Click
        Await PanMcServer.UpdateServerInfoAsync(LabServerIp.Text)
        ServerInfo.Visibility = Visibility.Visible
    End Function
End Class
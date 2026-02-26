

Class MinecraftServerQuery
    Inherits Grid
    Private Sub BtnServerQuery_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnServerQuery.Click
        Dispatcher.BeginInvoke(Function() ServerQueryAsync())
    End Sub
    Private Async Function ServerQueryAsync As Task
        Await PanMcServer.UpdateServerInfoAsync(LabServerIp.Text)
        ServerInfo.Visibility = Visibility.Visible
    End Function
End Class
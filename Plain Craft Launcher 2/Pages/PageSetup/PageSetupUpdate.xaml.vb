Public Class PageSetupUpdate

    Private IsLoad As Boolean = False

    Private Sub PageSetupUpdate_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '非重复加载部分
        If IsLoad Then Return
        IsLoad = True

        AniControlEnabled += 1
        AniControlEnabled -= 1

    End Sub
    
    Private Sub BtnUpdate_Click(sender As Object, e As EventArgs) Handles BtnUpdate.Click
        FrmMain.BtnExtraUpdateRestart.ShowRefresh()
        FrmMain.BtnExtraUpdateRestart.Ribble()
    End Sub
    
End Class

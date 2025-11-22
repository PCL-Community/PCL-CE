Imports PCL.Core.App

Public Class PageSetupUpdate
    Private Sub PageSetupUpdate_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ComboSystemUpdateChannel.SelectedIndex = Config.System.UpdateBranch
        ComboSystemUpdateMode.SelectedIndex = Config.System.UpdateSolution
    End Sub
    
    Private Sub BtnUpdate_Click(sender As Object, e As EventArgs) Handles BtnUpdate.Click
        FrmMain.BtnExtraUpdateRestart.ShowRefresh()
        FrmMain.BtnExtraUpdateRestart.Ribble()
    End Sub
    
    Private Sub BtnChangelogDetail_Click(sender As Object, e As EventArgs) Handles BtnChangelogDetail.Click
        
    End Sub
    
End Class

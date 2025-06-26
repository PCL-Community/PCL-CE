Class PageVersionSavesBackup

    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        PanMain.Children.Add(New TextBlock With {.Text = $"目前管理： {PageVersionSavesLeft.CurrentSave}"})
    End Sub

End Class

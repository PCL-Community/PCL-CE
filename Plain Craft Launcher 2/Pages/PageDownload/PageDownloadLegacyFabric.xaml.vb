Public Class PageDownloadLegacyFabric

    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, CardVersions, CardTip, DlLegacyFabricListLoader, AddressOf Load_OnFinish)
    End Sub
    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
    End Sub

    Private Sub Load_OnFinish()
        '结果数据化
        Try
            Dim Versions As JArray = DlLegacyFabricListLoader.Output.Value("installer")
            PanVersions.Children.Clear()
            For Each Version In Versions
                PanVersions.Children.Add(LegacyFabricDownloadListItem(Version, AddressOf LegacyFabric_Selected))
            Next
            CardVersions.Title = "版本列表 (" & Versions.Count & ")"
        Catch ex As Exception
            Log(ex, "可视化 LegacyFabric 版本列表出错", LogLevel.Feedback)
        End Try
    End Sub

    Private Sub LegacyFabric_Selected(sender As MyListItem, e As EventArgs)
        McDownloadLegacyFabricLoaderSave(sender.Tag)
    End Sub

    Private Sub BtnWeb_Click(sender As Object, e As EventArgs) Handles BtnWeb.Click
        OpenWebsite("https://legacyfabric.net/")
    End Sub

End Class

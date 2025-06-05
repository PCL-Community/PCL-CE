Imports PCL.Core.Helper

Public Class PageSetupJava

    Private IsLoad As Boolean = False

    Private Async Sub PageSetupLaunch_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PageLoaderInit(PanLoad, CardLoad, PanMain, Nothing, JavaSearchLoader, AddressOf OnLoadFinished)
        '小测试
        'Dim javas As New JavaManage()
        'Await javas.ScanJava()
        'MsgBox(javas.JavaList.Select(Function(x) $"{x.JavaFolder} - {x.Version.ToString()} - {x.Is64Bit}").Join(vbCrLf))
    End Sub

    Private Sub OnLoadFinished()
        Dim ItemBuilder = Function(J As JavaEntry) As MyListItem
                              Dim Item As New MyListItem
                              Dim VersionTypeDesc = If(J.IsJre, "JRE", "JDK")
                              Dim VersionNameDesc = J.VersionCode.ToString()
                              Item.Title = $"{VersionTypeDesc} {VersionNameDesc}"
                              Item.Info = J.PathJava
                              Return Item
                          End Function
        PanContent.Children.Clear()
        Dim DisplayJavaList = JavaList.Sort(Function(a, b) a.VersionCode > b.VersionCode)
        For Each J In DisplayJavaList
            PanContent.Children.Add(ItemBuilder(J))
        Next
    End Sub

    Private Sub BtnRefresh_Click(sender As Object, e As RouteEventArgs) Handles BtnRefresh.Click
        JavaSearchLoader.Start(IsForceRestart:=True)
    End Sub

End Class

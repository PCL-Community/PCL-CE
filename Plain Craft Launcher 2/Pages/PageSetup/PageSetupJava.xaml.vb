Imports PCL.Core.Helper.Java

Public Class PageSetupJava

    Private IsLoad As Boolean = False

    Private Async Sub PageSetupLaunch_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PageLoaderInit(PanLoad, CardLoad, PanMain, Nothing, JavaSearchLoader, AddressOf OnLoadFinished)
        Dim ret = Await JavaHelper.ScanJava()
        MsgBox(ret.Select(Function(x) $"{x.Path} - {x.Version.ToString()} - {x.Brand.ToString()}").Join(vbCrLf))
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

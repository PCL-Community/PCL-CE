Imports PCL.Core.Model

Public Class PageSetupJava

    Private IsLoad As Boolean = False

    Private JavaPageLoader As New LoaderTask(Of Integer, List(Of Java))("JavaPageLoader", AddressOf Load_GetJavaList)
    Private Sub PageSetupLaunch_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PageLoaderInit(PanLoad, CardLoad, PanMain, Nothing, JavaPageLoader, AddressOf OnLoadFinished)
    End Sub

    Private Sub Load_GetJavaList(loader As LoaderTask(Of Integer, List(Of Java)))
        loader.Output = Javas.JavaList
    End Sub

    Private Sub OnLoadFinished()
        Dim ItemBuilder = Function(J As Java) As MyListItem
                              Dim Item As New MyListItem
                              Dim VersionTypeDesc = If(J.IsJre, "JRE", "JDK")
                              Dim VersionNameDesc = J.JavaMajorVersion.ToString()
                              Item.Title = $"{VersionTypeDesc} {VersionNameDesc}"
                              Item.Info = J.JavaFolder
                              Dim displayTags As New List(Of String)
                              displayTags.Add(If(J.Is64Bit, "64 Bit", "32 Bit"))
                              displayTags.Add(J.Brand.ToString())
                              Item.Tags = displayTags

                              Item.Type = MyListItem.CheckType.RadioBox
                              If J.JavaExePath = Setup.Get("LaunchArgumentJavaSelect") Then
                                  Item.SetChecked(True, False, False)
                              End If
                              AddHandler Item.Check, Sub()
                                                         Setup.Set("LaunchArgumentJavaSelect", J.JavaExePath)
                                                     End Sub
                              Return Item
                          End Function
        PanContent.Children.Clear()
        For Each J In Javas.JavaList
            PanContent.Children.Add(ItemBuilder(J))
        Next
    End Sub

    Private Sub BtnRefresh_Click(sender As Object, e As RouteEventArgs) Handles BtnRefresh.Click
        JavaPageLoader.Start(IsForceRestart:=True)
    End Sub

End Class

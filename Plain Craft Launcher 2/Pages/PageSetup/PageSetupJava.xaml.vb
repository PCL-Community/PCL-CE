Imports PCL.Core.Minecraft
Imports PCL.Core.UI

Public Class PageSetupJava

    Private IsLoad As Boolean = False

    Public Loader As New LoaderTask(Of Boolean, List(Of JavaEntry))("JavaPageLoader", AddressOf Load_GetJavaList)
    Private Sub PageSetupLaunch_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PageLoaderInit(PanLoad, CardLoad, PanMain, Nothing, Loader, AddressOf OnLoadFinished, AddressOf Load_Input)
    End Sub

    Private Function Load_Input()
        Return False
    End Function
    Private Sub Load_GetJavaList(loader As LoaderTask(Of Boolean, List(Of JavaEntry)))
        If loader.Input Then
            JavaService.JavaManager.ScanJavaAsync().GetAwaiter().GetResult()
        End If
        loader.Output = Javas.GetSortedJavaList()
    End Sub

    Private Sub OnLoadFinished()
        PanContent.Children.Clear()
        Dim ItemAuto As New MyListItem With {
            .Type = MyListItem.CheckType.RadioBox,
            .Title = "自动选择",
            .Info = "Java 选择自动挡，依据游戏需要自动选择合适的 Java"
        }
        AddHandler ItemAuto.Check,
            Sub()
                Setup.Set("LaunchArgumentJavaSelect", "")
            End Sub

        PanContent.Children.Add(ItemAuto)
        Dim CurrentSetJava = Setup.Get("LaunchArgumentJavaSelect")
        For Each J In Javas.GetSortedJavaList()
            Dim item = ItemBuild(J)
            PanContent.Children.Add(item)
            If J.Installation.JavaExePath = CurrentSetJava Then item.SetChecked(True, False, False)
        Next

        If String.IsNullOrEmpty(CurrentSetJava) Then ItemAuto.SetChecked(True, False, False)
    End Sub

    Private Function ItemBuild(J As JavaEntry) As MyListItem
        Dim Item As New MyListItem
        Dim VersionTypeDesc = If(J.Installation.IsJre, "JRE", "JDK")
        Dim VersionNameDesc = J.Installation.MajorVersion.ToString()
        Item.Title = $"{VersionTypeDesc} {VersionNameDesc}"

        Item.Info = J.Installation.JavaFolder
        Dim displayTags As New List(Of String)
        Dim DisplayBits = If(J.Installation.Is64Bit, "64 Bit", "32 Bit")
        displayTags.Add(DisplayBits)
        Dim DisplayBrand = J.Installation.Brand.ToString()
        displayTags.Add(DisplayBrand)
        Item.Tags = displayTags

        Item.Type = MyListItem.CheckType.RadioBox
        AddHandler Item.Check,
                              Sub(sender As Object, e As RouteEventArgs)
                                  If Not J.Installation.IsStillAvailable Then
                                      Hint("此 Java 不可用，请刷新列表")
                                      Return
                                  End If

                                  If J.IsEnabled Then
                                      Setup.Set("LaunchArgumentJavaSelect", J.Installation.JavaExePath)
                                  Else
                                      Hint("请先启用此 Java 后再选择其作为默认 Java")
                                      e.Handled = True
                                  End If
                              End Sub

        Dim BtnOpenFolder As New MyIconButton
        BtnOpenFolder.Logo = Logo.IconButtonOpen
        BtnOpenFolder.ToolTip = "打开"
        AddHandler BtnOpenFolder.Click,
                              Sub()
                                  If Not J.Installation.IsStillAvailable Then
                                      Hint("此 Java 不可用，请刷新列表")
                                      Return
                                  End If

                                  OpenExplorer(J.Installation.JavaFolder)
                              End Sub

        Dim BtnInfo As New MyIconButton
        BtnInfo.Logo = Logo.IconButtonInfo
        BtnInfo.ToolTip = "详细信息"
        AddHandler BtnInfo.Click,
                              Sub()
                                  If Not J.Installation.IsStillAvailable Then
                                      Hint("此 Java 不可用，请刷新列表")
                                      Return
                                  End If

                                  MyMsgBox($"类型: {VersionTypeDesc}" & vbCrLf &
                                                                     $"版本: {J.Installation.Version.ToString()}" & vbCrLf &
                                                                     $"架构: {J.Installation.Architecture.ToString()} ({DisplayBits})" & vbCrLf &
                                                                     $"品牌: {DisplayBrand}" & vbCrLf &
                                                                     $"位置: {J.Installation.JavaFolder}", "Java 信息")
                              End Sub

        Dim BtnEnableSwitch As New MyIconButton


        Item.Buttons = {BtnOpenFolder, BtnInfo, BtnEnableSwitch}

        Dim UpdateEnableStyle = Sub(IsCurEnable As Boolean)
                                    If Not J.Installation.IsStillAvailable Then
                                        Hint("此 Java 不可用，请刷新列表")
                                        Return
                                    End If

                                    If IsCurEnable Then
                                        Item.LabTitle.TextDecorations = Nothing
                                        Item.LabTitle.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
                                        BtnEnableSwitch.Logo = Logo.IconButtonDisable
                                        BtnEnableSwitch.ToolTip = "禁用此 Java"
                                    Else
                                        Item.LabTitle.TextDecorations = TextDecorations.Strikethrough
                                        Item.LabTitle.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4")
                                        BtnEnableSwitch.Logo = Logo.IconButtonEnable
                                        BtnEnableSwitch.ToolTip = "启用此 Java"
                                    End If
                                End Sub

        AddHandler BtnEnableSwitch.Click,
                              Sub()
                                  Try
                                      Dim target = Javas.AddOrGet(J.Installation.JavaExePath)
                                      If target Is Nothing Then
                                          Hint("此 Java 不可用，请刷新列表")
                                          Return
                                      End If

                                      If target.IsEnabled AndAlso Setup.Get("LaunchArgumentJavaSelect") = target.Installation.JavaExePath Then
                                          Hint("请先取消选择此 Java 作为默认 Java 后再禁用")
                                          Return
                                      End If

                                      target.IsEnabled = Not target.IsEnabled
                                      UpdateEnableStyle(target.IsEnabled)
                                      Javas.SaveConfig()
                                  Catch ex As Exception
                                      Log(ex, "调整 Java 启用状态失败", LogLevel.Hint)
                                  End Try
                              End Sub

        UpdateEnableStyle(J.IsEnabled)

        Return Item
    End Function

    Private Sub BtnAdd_Click(sender As Object, e As RouteEventArgs) Handles BtnAdd.Click
        Dim ret = SystemDialogs.SelectFile("Java 程序(java.exe)|java.exe", "选择 Java 程序")
        If String.IsNullOrEmpty(ret) OrElse Not File.Exists(ret) Then Return
        If Javas.Exist(ret) Then
            Hint("Java 已经存在，不用再次添加……")
        Else
            Dispatcher.BeginInvoke(
                Async Function() As Task
                    Await Task.Run(Sub()
                                       Dim ignore = Javas.AddOrGet(ret)
                                       Javas.SaveConfig()
                                   End Sub)
                    If Javas.Exist(ret) Then
                        Hint("已添加 Java！", HintType.Finish)
                        Loader.Start(True, True)
                    Else
                        Hint("未能成功将 Java 加入列表中", HintType.Critical)
                    End If
                End Function)
        End If
    End Sub

End Class

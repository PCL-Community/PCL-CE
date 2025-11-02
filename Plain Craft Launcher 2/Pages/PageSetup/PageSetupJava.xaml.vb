Imports PCL.Core.Minecraft
Imports PCL.Core.UI

Public Class PageSetupJava

    Private IsLoad As Boolean = False

    Private JavaPageLoader As New LoaderTask(Of Boolean, List(Of JavaInfo))("JavaPageLoader", AddressOf Load_GetJavaList)
    Private Sub PageSetupLaunch_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PageLoaderInit(PanLoad, CardLoad, PanMain, Nothing, JavaPageLoader, AddressOf OnLoadFinished, AddressOf Load_Input)
    End Sub

    Private Function Load_Input()
        Return False
    End Function
    Private Sub Load_GetJavaList(loader As LoaderTask(Of Boolean, List(Of JavaInfo)))
        If loader.Input Then
            JavaService.JavaManager.ScanJavaAsync().GetAwaiter().GetResult()
        End If
        loader.Output = Javas.JavaList
    End Sub

    Private Sub OnLoadFinished()
        Dim ItemBuilder = Function(J As JavaInfo) As MyListItem
                              Dim Item As New MyListItem
                              Dim VersionTypeDesc = If(J.IsJre, "JRE", "JDK")
                              Dim VersionNameDesc = J.JavaMajorVersion.ToString()
                              Item.Title = $"{VersionTypeDesc} {VersionNameDesc}"

                              Item.Info = J.JavaFolder
                              Dim displayTags As New List(Of String)
                              Dim DisplayBits = If(J.Is64Bit, "64 Bit", "32 Bit")
                              displayTags.Add(DisplayBits)
                              Dim DisplayBrand = J.Brand.ToString()
                              displayTags.Add(DisplayBrand)
                              Item.Tags = displayTags

                              Item.Type = MyListItem.CheckType.RadioBox
                              AddHandler Item.Check, Sub(sender As Object, e As RouteEventArgs)
                                                         If J.IsEnabled Then
                                                             Setup.Set("LaunchArgumentJavaSelect", J.JavaExePath)
                                                         Else
                                                             Hint("请先启用此 Java 后再选择其作为默认 Java")
                                                             e.Handled = True
                                                         End If
                                                     End Sub
                              Dim BtnOpenFolder As New MyIconButton
                              BtnOpenFolder.Logo = Logo.IconButtonOpen
                              BtnOpenFolder.ToolTip = "打开"
                              AddHandler BtnOpenFolder.Click, Sub()
                                                                  OpenExplorer(J.JavaFolder)
                                                              End Sub
                              Dim BtnInfo As New MyIconButton
                              BtnInfo.Logo = Logo.IconButtonInfo
                              BtnInfo.ToolTip = "详细信息"
                              AddHandler BtnInfo.Click, Sub()
                                                            MyMsgBox($"类型: {VersionTypeDesc}" & vbCrLf &
                                                                     $"版本: {J.Version.ToString()}" & vbCrLf &
                                                                     $"架构: {J.JavaArch.ToString()} ({DisplayBits})" & vbCrLf &
                                                                     $"品牌: {DisplayBrand}" & vbCrLf &
                                                                     $"位置: {J.JavaFolder}", "Java 信息")
                                                        End Sub
                              Dim BtnEnableSwitch As New MyIconButton


                              Item.Buttons = {BtnOpenFolder, BtnInfo, BtnEnableSwitch}

                              Dim UpdateEnableStyle = Sub(IsCurEnable As Boolean)
                                                          If IsCurEnable Then
                                                              Item.LabTitle.TextDecorations = Nothing
                                                              Item.LabTitle.Foreground = DynamicColors.Color1Brush
                                                              BtnEnableSwitch.Logo = Logo.IconButtonDisable
                                                              BtnEnableSwitch.ToolTip = "禁用此 Java"
                                                          Else
                                                              Item.LabTitle.TextDecorations = TextDecorations.Strikethrough
                                                              Item.LabTitle.Foreground = ColorGray4
                                                              BtnEnableSwitch.Logo = Logo.IconButtonEnable
                                                              BtnEnableSwitch.ToolTip = "启用此 Java"
                                                          End If
                                                      End Sub
                              AddHandler BtnEnableSwitch.Click, Sub()
                                                                    Try
                                                                        Dim target = Javas.JavaList.Where(Function(x) x.JavaExePath = J.JavaExePath).First()
                                                                        If target.IsEnabled AndAlso Setup.Get("LaunchArgumentJavaSelect") = target.JavaExePath Then
                                                                            Hint("请先取消选择此 Java 作为默认 Java 后再禁用")
                                                                            Return
                                                                        End If
                                                                        target.IsEnabled = Not target.IsEnabled
                                                                        UpdateEnableStyle(target.IsEnabled)
                                                                        JavaService.SaveToConfig()
                                                                    Catch ex As Exception
                                                                        Log(ex, "调整 Java 启用状态失败", LogLevel.Hint)
                                                                    End Try
                                                                End Sub
                              UpdateEnableStyle(J.IsEnabled)

                              Return Item
                          End Function
        PanContent.Children.Clear()
        Dim ItemAuto As New MyListItem With {
            .Type = MyListItem.CheckType.RadioBox,
            .Title = "自动选择",
            .Info = "Java 选择自动挡，依据游戏需要自动选择合适的 Java"
        }
        AddHandler ItemAuto.Check, Sub()
                                       Setup.Set("LaunchArgumentJavaSelect", "")
                                   End Sub
        PanContent.Children.Add(ItemAuto)
        Dim CurrentSetJava = Setup.Get("LaunchArgumentJavaSelect")
        For Each J In Javas.JavaList
            Dim item = ItemBuilder(J)
            PanContent.Children.Add(item)
            If J.JavaExePath = CurrentSetJava Then item.SetChecked(True, False, False)
        Next
        If String.IsNullOrEmpty(CurrentSetJava) Then ItemAuto.SetChecked(True, False, False)
    End Sub

    Private Sub BtnRefresh_Click(sender As Object, e As RouteEventArgs) Handles BtnRefresh.Click
        JavaPageLoader.Start(True, True)
    End Sub

    Private Async Function BtnAdd_Click(sender As Object, e As RouteEventArgs) As Task Handles BtnAdd.Click
        Dim ret = SystemDialogs.SelectFile("Java 程序(java.exe)|java.exe", "选择 Java 程序")
        If String.IsNullOrEmpty(ret) OrElse Not File.Exists(ret) Then Return
        If JavaService.JavaManager.HasJava(ret) Then
            Hint("Java 已经存在，不用再次添加……")
        Else
            Await Task.Run(Sub()
                               JavaService.JavaManager.Add(ret)
                               JavaService.SaveToConfig()
                           End Sub)
            If JavaService.JavaManager.HasJava(ret) Then
                Hint("已添加 Java！", HintType.Finish)
                JavaPageLoader.Start(True, True)
            Else
                Hint("未能成功将 Java 加入列表中", HintType.Critical)
            End If
        End If
    End Function

End Class

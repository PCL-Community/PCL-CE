Class PageHomepageSetup
    Public Shadows IsLoaded As Boolean = False

    Private Sub PageSetupUI_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()
        ThemeCheckAll(True)

        AniControlEnabled += 1
        Reload() '#4826，在每次进入页面时都刷新一下
        AniControlEnabled -= 1

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True

    End Sub
    Public Sub Reload()
        Try

            '主页
            Try
                ComboCustomPreset.SelectedIndex = Setup.Get("UiCustomPreset")
            Catch
                Setup.Reset("UiCustomPreset")
            End Try
            CType(FindName("RadioCustomType" & Setup.Load("UiCustomType", ForceReload:=True)), MyRadioBox).Checked = True
            TextCustomNet.Text = Setup.Get("UiCustomNet")
        Catch ex As NullReferenceException
            Log(ex, "个性化设置项存在异常，已被自动重置", LogLevel.Msgbox)
            Reset()
        Catch ex As Exception
            Log(ex, "重载个性化设置时出错", LogLevel.Feedback)
        End Try
    End Sub

    '初始化
    Public Sub Reset()
        Try
            Setup.Reset("UiLauncherTransparent")
            Setup.Reset("UiLauncherTheme")
            Setup.Reset("UiLauncherLogo")
            Setup.Reset("UiLauncherHue")
            Setup.Reset("UiLauncherSat")
            Setup.Reset("UiLauncherDelta")
            Setup.Reset("UiLauncherLight")
            Setup.Reset("UiBlur")
            Setup.Reset("UiBlurValue")
            Setup.Reset("UiBackgroundColorful")
            Setup.Reset("UiBackgroundOpacity")
            Setup.Reset("UiBackgroundBlur")
            Setup.Reset("UiBackgroundSuit")
            Setup.Reset("UiDarkMode")
            Setup.Reset("UiFont")
            Setup.Reset("UiLogoType")
            Setup.Reset("UiLogoText")
            Setup.Reset("UiLogoLeft")
            Setup.Reset("UiMusicVolume")
            Setup.Reset("UiMusicStop")
            Setup.Reset("UiMusicStart")
            Setup.Reset("UiMusicRandom")
            Setup.Reset("UiMusicSMTC")
            Setup.Reset("UiMusicAuto")
            Setup.Reset("UiCustomType")
            Setup.Reset("UiCustomPreset")
            Setup.Reset("UiCustomNet")
            Setup.Reset("UiHiddenPageDownload")
            Setup.Reset("UiHiddenPageLink")
            Setup.Reset("UiHiddenPageSetup")
            Setup.Reset("UiHiddenPageOther")
            Setup.Reset("UiHiddenFunctionSelect")
            Setup.Reset("UiHiddenFunctionModUpdate")
            Setup.Reset("UiHiddenFunctionHidden")
            Setup.Reset("UiHiddenSetupLaunch")
            Setup.Reset("UiHiddenSetupUi")
            Setup.Reset("UiHiddenSetupLink")
            Setup.Reset("UiHiddenSetupSystem")
            Setup.Reset("UiHiddenOtherAbout")
            Setup.Reset("UiHiddenOtherFeedback")
            Setup.Reset("UiHiddenOtherVote")
            Setup.Reset("UiHiddenOtherHelp")
            Setup.Reset("UiHiddenOtherTest")
            Setup.Reset("UiHiddenVersionEdit")
            Setup.Reset("UiHiddenVersionExport")
            Setup.Reset("UiHiddenVersionSave")
            Setup.Reset("UiHiddenVersionScreenshot")
            Setup.Reset("UiHiddenVersionMod")
            Setup.Reset("UiHiddenVersionResourcePack")
            Setup.Reset("UiHiddenVersionShader")

            Log("[Setup] 已初始化个性化设置！")
            Hint("已初始化个性化设置", HintType.Finish, False)
        Catch ex As Exception
            Log(ex, "初始化个性化设置失败", LogLevel.Msgbox)
        End Try

        Reload()
    End Sub

    '将控件改变路由到设置改变
    Private Shared Sub ComboChange(sender As MyComboBox, e As Object) Handles ComboCustomPreset.SelectionChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.SelectedIndex)
    End Sub
    Private Shared Sub TextBoxChange(sender As MyTextBox, e As Object) Handles TextCustomNet.ValidatedTextChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Text)
    End Sub
    Private Shared Sub RadioBoxChange(sender As MyRadioBox, e As Object) Handles RadioCustomType0.Check, RadioCustomType1.Check, RadioCustomType2.Check, RadioCustomType3.Check
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag.ToString.Split("/")(0), Val(sender.Tag.ToString.Split("/")(1)))
    End Sub
    '主页
    Private Sub BtnCustomFile_Click(sender As Object, e As EventArgs) Handles BtnCustomFile.Click
        Try
            If File.Exists(Path & "PCL\Custom.xaml") Then
                If MyMsgBox("当前已存在布局文件，继续生成教学文件将会覆盖现有布局文件！", "覆盖确认", "继续", "取消", IsWarn:=True) = 2 Then Return
            End If
            WriteFile(Path & "PCL\Custom.xaml", GetResources("Custom"))
            Hint("教学文件已生成！", HintType.Finish)
            OpenExplorer(Path & "PCL\Custom.xaml")
        Catch ex As Exception
            Log(ex, "生成教学文件失败", LogLevel.Feedback)
        End Try
    End Sub
    Private Sub BtnCustomRefresh_Click() Handles BtnCustomRefresh.Click
        FrmLaunchRight.ForceRefresh()
        Hint("已刷新主页！", HintType.Finish)
    End Sub
    Private Sub BtnCustomTutorial_Click(sender As Object, e As EventArgs) Handles BtnCustomTutorial.Click
        MyMsgBox("1. 点击 生成教学文件 按钮，这会在 PCL 文件夹下生成 Custom.xaml 布局文件。" & vbCrLf &
                 "2. 使用记事本等工具打开这个文件并进行修改，修改完记得保存。" & vbCrLf &
                 "3. 点击 刷新主页 按钮，查看主页现在长啥样了。" & vbCrLf &
                 vbCrLf &
                 "你可以在生成教学文件后直接刷新主页，对照着进行修改，更有助于理解。" & vbCrLf &
                 "直接将主页文件拖进 PCL 窗口也可以快捷加载。", "主页自定义教程")
    End Sub










    Private Sub BtnHomepageMarket_Click(sender As Object, e As EventArgs) Handles BtnGotoHomepageMarket.Click
        FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.HomepageMarket}）
    End Sub
End Class

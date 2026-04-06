Imports PCL.Core.App
Imports PCL.Core.UI
Imports PCL.Core.Utils

Public Class PageSetupUI

    Public Shadows IsLoaded As Boolean = False

    Public ReadOnly Property ThemeColors As String() =
        If(Basics.IsAprilFool, {"天空蓝", "龙猫蓝", "死机蓝", "HMCL"}, {"天空蓝", "龙猫蓝", "死机蓝"})

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub PageSetupUI_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()
        ThemeCheckAll(True)

        If ThemeDontClick <> 0 Then
            Dim NewText As String
            Select Case ThemeDontClick
                Case 1
                    NewText = "眼瞎白"
                Case 2
                    NewText = "真·滑稽彩"
                Case Else
                    NewText = "？？？"
            End Select
            For Each Control In PanLauncherTheme.Children
                If (TypeOf Control Is MyRadioBox) AndAlso CType(Control, MyRadioBox).IsEnabled Then
                    CType(Control, MyRadioBox).Text = NewText
                End If
            Next
        End If

#If DEBUG Then
        If EnableCustomTheme Then
            LabLauncherDelta.Visibility = Visibility.Visible
            SliderLauncherDelta.Visibility = Visibility.Visible
            LabLauncherLight.Visibility = Visibility.Visible
            SliderLauncherLight.Visibility = Visibility.Visible
        End If
#End If

        AniControlEnabled += 1
        Reload() '#4826，在每次进入页面时都刷新一下
        AniControlEnabled -= 1

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True

        SliderLoad()

        PanLauncherHide.Visibility = Visibility.Visible

        '设置解锁

        If Not RadioLauncherTheme8.IsEnabled Then LabLauncherTheme8Copy.ToolTip = "社区版不包含主题功能，请使用官方快照版"
        RadioLauncherTheme8.ToolTip = "社区版不包含主题功能，请使用官方快照版"
        If Not RadioLauncherTheme9.IsEnabled Then LabLauncherTheme9Copy.ToolTip = "社区版不包含主题功能，请使用官方快照版"
        RadioLauncherTheme9.ToolTip = "社区版不包含主题功能，请使用官方快照版"
        '极客蓝的处理在 ThemeCheck 中

    End Sub
    Public Sub Reload()
        Try
            '启动器
            SliderLauncherOpacity.Value = Setup.Get("UiLauncherTransparent")
            SliderLauncherHue.Value = Setup.Get("UiLauncherHue")
            SliderLauncherSat.Value = Setup.Get("UiLauncherSat")
            SliderLauncherDelta.Value = Setup.Get("UiLauncherDelta")
            SliderLauncherLight.Value = Setup.Get("UiLauncherLight")
            'If Setup.Get("UiLauncherTheme") <= 14 Then CType(FindName("RadioLauncherTheme" & Setup.Get("UiLauncherTheme")), MyRadioBox).Checked = True
            CheckLauncherLogo.Checked = Setup.Get("UiLauncherLogo")
            ComboDarkMode.SelectedIndex = Setup.Get("UiDarkMode")
            If Not Basics.IsAprilFool Then
                'fix ui state error
                If Setup.Get("UiDarkColor") = ColorTheme.HmclBlue Then Setup.Set("UiDarkColor", ColorTheme.CatBlue)
                If Setup.Get("UiLightColor") = ColorTheme.HmclBlue Then Setup.Set("UiLightColor", ColorTheme.CatBlue)
            End If
            ComboDarkColor.SelectedIndex = Setup.Get("UiDarkColor")
            ComboLightColor.SelectedIndex = Setup.Get("UiLightColor")
            CheckShowLaunchingHint.Checked = Setup.Get("UiShowLaunchingHint")

            '字体设置
            ComboUiFont.SelectedFontTag = Setup.Get("UiFont")
            ComboUiMotdFont.SelectedFontTag = Setup.Get("UiMotdFont")
            
            CheckBlur.Checked = Setup.Get("UiBlur")
            SliderBlurValue.Value = Setup.Get("UiBlurValue")
            SliderBlurSamplingRate.Value = Setup.Get("UiBlurSamplingRate")
            ComboBlurType.SelectedIndex = Setup.Get("UiBlurType")
            PanBlurValue.Visibility = If(CheckBlur.Checked, Visibility.Visible, Visibility.Collapsed)
            CheckLockWindowSize.Checked = Setup.Get("UiLockWindowSize")

            '背景图片
            SliderBackgroundOpacity.Value = Setup.Get("UiBackgroundOpacity")
            SliderBackgroundBlur.Value = Setup.Get("UiBackgroundBlur")
            ComboBackgroundSuit.SelectedIndex = Setup.Get("UiBackgroundSuit")
            CheckBackgroundColorful.Checked = Setup.Get("UiBackgroundColorful")
            Dim autoPauseVideo = Setup.Get("UiAutoPauseVideo")
            CheckAutoPauseVideo.Checked = autoPauseVideo
            If ModVideoBack.IsGaming = True Then
                If autoPauseVideo = True Then
                    BtnBackgroundRefresh.IsEnabled = False
                End If
            End If
            BackgroundRefresh(False, False)

            '标题栏
            CType(FindName("RadioLogoType" & Setup.Get("UiLogoType")), MyRadioBox).Checked = True
            CheckLogoLeft.Visibility = If(RadioLogoType0.Checked, Visibility.Visible, Visibility.Collapsed)
            PanLogoText.Visibility = If(RadioLogoType2.Checked, Visibility.Visible, Visibility.Collapsed)
            PanLogoChange.Visibility = If(RadioLogoType3.Checked, Visibility.Visible, Visibility.Collapsed)
            TextLogoText.Text = Setup.Get("UiLogoText")
            CheckLogoLeft.Checked = Setup.Get("UiLogoLeft")

            '背景音乐
            CheckMusicRandom.Checked = Setup.Get("UiMusicRandom")
            CheckMusicAuto.Checked = Setup.Get("UiMusicAuto")
            CheckMusicStop.Checked = Setup.Get("UiMusicStop")
            CheckMusicStart.Checked = Setup.Get("UiMusicStart")
            CheckMusicSMTC.Checked = Setup.Get("UiMusicSMTC")
            SliderMusicVolume.Value = Setup.Get("UiMusicVolume")
            MusicRefreshUI()

            '主页
            Try
                ComboCustomPreset.SelectedIndex = Setup.Get("UiCustomPreset")
            Catch
                Setup.Reset("UiCustomPreset")
            End Try
            CType(FindName("RadioCustomType" & Setup.Load("UiCustomType", ForceReload:=True)), MyRadioBox).Checked = True
            TextCustomNet.Text = Setup.Get("UiCustomNet")

            '功能隐藏
            ' 获取配置组引用
            Dim uiHidden = Config.Preference.Hide

            ' 主页面
            CheckHiddenPageDownload.Checked = uiHidden.PageDownload
            CheckHiddenPageSetup.Checked = uiHidden.PageSetup
            CheckHiddenPageTools.Checked = uiHidden.PageTools

            ' 子页面 设置
            CheckHiddenSetupLaunch.Checked = uiHidden.SetupLaunch
            CheckHiddenSetupUI.Checked = uiHidden.SetupUi
            CheckHiddenSetupGameManage.Checked = uiHidden.SetupGameManage
            CheckHiddenSetupJava.Checked = uiHidden.SetupJava
            CheckHiddenLauncherMisc.Checked = uiHidden.SetupLauncherMisc
            CheckHiddenSetupUpdate.Checked = uiHidden.SetupUpdate
            CheckHiddenSetupGameLink.Checked = uiHidden.SetupGameLink
            CheckHiddenSetupAbout.Checked = uiHidden.SetupAbout
            CheckHiddenSetupFeedback.Checked = uiHidden.SetupFeedback
            CheckHiddenSetupLog.Checked = uiHidden.SetupLog

            ' 子页面 工具
            CheckHiddenToolsGameLink.Checked = uiHidden.ToolsGameLink
            CheckHiddenToolsHelp.Checked = uiHidden.ToolsHelp
            CheckHiddenToolsTest.Checked = uiHidden.ToolsTest

            ' 子页面 实例设置
            CheckHiddenVersionEdit.Checked = uiHidden.InstanceEdit
            CheckHiddenVersionExport.Checked = uiHidden.InstanceExport
            CheckHiddenVersionSave.Checked = uiHidden.InstanceSave
            CheckHiddenVersionScreenshot.Checked = uiHidden.InstanceScreenshot
            CheckHiddenVersionMod.Checked = uiHidden.InstanceMod
            CheckHiddenVersionResourcePack.Checked = uiHidden.InstanceResourcePack
            CheckHiddenVersionShader.Checked = uiHidden.InstanceShader
            CheckHiddenVersionSchematic.Checked = uiHidden.InstanceSchematic
            CheckHiddenVersionServer.Checked = uiHidden.InstanceServer

            ' 特定功能
            CheckHiddenFunctionSelect.Checked = uiHidden.FunctionSelect
            CheckHiddenFunctionModUpdate.Checked = uiHidden.FunctionModUpdate
            CheckHiddenFunctionHidden.Checked = uiHidden.FunctionHidden
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
            Config.Preference.Reset()
            Log("[Setup] 已初始化个性化设置！")
            Hint("已初始化个性化设置", HintType.Finish, False)
        Catch ex As Exception
            Log(ex, "初始化个性化设置失败", LogLevel.Msgbox)
        End Try

        Reload()
    End Sub

    '将控件改变路由到设置改变
    Private Shared Sub SliderChange(sender As MySlider, e As Object) Handles SliderBackgroundOpacity.Change, SliderBlurValue.Change, SliderBlurSamplingRate.Change, SliderBackgroundBlur.Change, SliderLauncherOpacity.Change, SliderMusicVolume.Change ', SliderLauncherHue.Change, SliderLauncherLight.Change, SliderLauncherSat.Change, SliderLauncherDelta.Change
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Value)
    End Sub
    Private Shared Sub ComboChange(sender As MyComboBox, e As Object) Handles ComboDarkMode.SelectionChanged, ComboBackgroundSuit.SelectionChanged, ComboCustomPreset.SelectionChanged, ComboBlurType.SelectionChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.SelectedIndex)
    End Sub
    Private Shared Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles _
    CheckLockWindowSize.Change, CheckBlur.Change, CheckAutoPauseVideo.Change,
    CheckMusicStop.Change, CheckMusicRandom.Change, CheckMusicAuto.Change, CheckMusicStart.Change, CheckMusicSMTC.Change,
    CheckBackgroundColorful.Change, CheckLogoLeft.Change, CheckLauncherLogo.Change,
    CheckHiddenFunctionHidden.Change, CheckHiddenFunctionSelect.Change, CheckHiddenFunctionModUpdate.Change,
    CheckHiddenPageDownload.Change, CheckHiddenPageSetup.Change, CheckHiddenPageTools.Change,
    CheckHiddenSetupLaunch.Change, CheckHiddenSetupUI.Change, CheckHiddenLauncherMisc.Change, CheckHiddenSetupUpdate.Change, CheckHiddenSetupGameLink.Change,
    CheckHiddenSetupAbout.Change, CheckHiddenSetupFeedback.Change, CheckHiddenSetupLog.Change, CheckHiddenSetupGameManage.Change,
    CheckHiddenToolsGameLink.Change, CheckHiddenToolsHelp.Change, CheckHiddenToolsTest.Change, CheckHiddenSetupJava.Change,
    CheckHiddenVersionEdit.Change, CheckHiddenVersionExport.Change, CheckHiddenVersionSave.Change,
    CheckHiddenVersionScreenshot.Change, CheckHiddenVersionMod.Change, CheckHiddenVersionResourcePack.Change,
    CheckHiddenVersionShader.Change, CheckHiddenVersionSchematic.Change, CheckHiddenVersionServer.Change, CheckShowLaunchingHint.Change

        ' 仅在动画未运行或初始化完成时保存设置，防止初始化时的触发导致重复写入
        If AniControlEnabled = 0 Then
            Setup.Set(sender.Tag, sender.Checked)
        End If

    End Sub
    Private Shared Sub TextBoxChange(sender As MyTextBox, e As Object) Handles TextLogoText.ValidatedTextChanged, TextCustomNet.ValidatedTextChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Text)
    End Sub
    Private Shared Sub RadioBoxChange(sender As MyRadioBox, e As Object) Handles RadioLogoType0.Check, RadioLogoType1.Check, RadioLogoType2.Check, RadioLogoType3.Check, RadioLauncherTheme0.Check, RadioLauncherTheme1.Check, RadioLauncherTheme2.Check, RadioLauncherTheme3.Check, RadioLauncherTheme4.Check, RadioLauncherTheme5.Check, RadioLauncherTheme6.Check, RadioLauncherTheme7.Check, RadioLauncherTheme8.Check, RadioLauncherTheme9.Check, RadioLauncherTheme10.Check, RadioLauncherTheme11.Check, RadioLauncherTheme12.Check, RadioLauncherTheme13.Check, RadioLauncherTheme14.Check, RadioCustomType0.Check, RadioCustomType1.Check, RadioCustomType2.Check, RadioCustomType3.Check
        Dim gotCfg = sender.Tag.ToString.Split("/")
        If AniControlEnabled = 0 Then Setup.Set(gotCfg(0), Integer.Parse(gotCfg(1)))
    End Sub

    Private Sub ComboFontChange(sender As Object, e As SelectionChangedEventArgs) Handles ComboUiFont.SelectionChanged
        If AniControlEnabled = 0 Then
            Setup.Set("UiFont", ComboUiFont.SelectedFontTag)
        End If
    End Sub
    
    Private Sub ComboMotdFontChange(sender As Object, e As SelectionChangedEventArgs) Handles ComboUiMotdFont.SelectionChanged
        If AniControlEnabled = 0 Then
            Setup.Set("UiMotdFont", ComboUiMotdFont.SelectedFontTag)
        End If
    End Sub

    '背景图片
    Private Sub BtnUIBgOpen_Click(sender As Object, e As EventArgs) Handles BtnBackgroundOpen.Click
        OpenExplorer(ExePath & "PCL\Pictures\")
    End Sub
    Private Sub BtnBackgroundRefresh_Click(sender As Object, e As EventArgs) Handles BtnBackgroundRefresh.Click
        BackgroundRefresh(True, True)
    End Sub
    Public Sub BackgroundRefreshUI(Show As Boolean, Count As Integer)
        If IsNothing(PanBackgroundOpacity) Then Return
        If Show Then
            PanBackgroundOpacity.Visibility = Visibility.Visible
            PanBackgroundBlur.Visibility = Visibility.Visible
            PanBackgroundSuit.Visibility = Visibility.Visible
            BtnBackgroundClear.Visibility = Visibility.Visible
            CheckAutoPauseVideo.Visibility = Visibility.Visible
            CardBackground.Title = "背景图片/视频（" & Count & " 张）"
        Else
            PanBackgroundOpacity.Visibility = Visibility.Collapsed
            PanBackgroundBlur.Visibility = Visibility.Collapsed
            PanBackgroundSuit.Visibility = Visibility.Collapsed
            BtnBackgroundClear.Visibility = Visibility.Collapsed
            CheckAutoPauseVideo.Visibility = Visibility.Collapsed
            CardBackground.Title = "背景图片/视频"
        End If
        CardBackground.TriggerForceResize()
    End Sub
    Private Sub BtnBackgroundClear_Click(sender As Object, e As EventArgs) Handles BtnBackgroundClear.Click
        If MyMsgBox("即将删除背景内容文件夹中的所有文件。" & vbCrLf & "此操作不可撤销，是否确定？", "警告",, "取消", IsWarn:=True) = 1 Then
            DeleteDirectory(ExePath & "PCL\Pictures")
            BackgroundRefresh(False, True)
            Hint("背景内容已清空！", HintType.Finish)
        End If
    End Sub
    ''' <summary>
    ''' 刷新背景图片及设置页 UI。
    ''' </summary>
    ''' <param name="IsHint">是否显示刷新提示。</param>
    ''' <param name="Refresh">是否刷新图片显示。</param>
    Public Shared Sub BackgroundRefresh(IsHint As Boolean, Refresh As Boolean)
        Try

            '获取可用的图片文件
            Directory.CreateDirectory(ExePath & "PCL\Pictures\")
            Dim Pic As List(Of String) = EnumerateFiles(ExePath & "PCL\Pictures\").
                    Where(Function(file) Not (file.Extension.Equals(".ini", StringComparison.OrdinalIgnoreCase) OrElse 
                                       file.Extension.Equals(".db", StringComparison.OrdinalIgnoreCase))).
                    Select(Function(file) file.FullName).
                    ToList() 
            '视频加载异常处理

            Dim videoHandler As EventHandler(Of ExceptionRoutedEventArgs) = 
                    Sub(sender, e)
                        Dim videoEx = e.ErrorException
                        Dim videoAddress As String = FrmMain.VideoBack.Source.ToString()
                        If FrmMain.VideoBack.Source IsNot Nothing Then
                            VideoStop()
                            
                            If videoEx.Message.Contains("0xC00D109B") Then
                                Log("刷新背景内容失败，该视频文件可能并非 H.264（AVC） 格式。" & vbCrLf &
                                    "你可以尝试使用视频转码工具打开视频文件并设定目标格式为 H.264（AVC） ，然后转码该视频。" & vbCrLf &
                                    "文件：" & videoAddress, LogLevel.Msgbox)
                            Else
                                Log(videoEx, "刷新背景内容失败（" & videoAddress & "）", LogLevel.Msgbox)
                            End If
                        End If
                    End Sub
            RemoveHandler FrmMain.VideoBack.MediaFailed, videoHandler
            RemoveHandler ModVideoBack.GamingStateChanged, AddressOf OnGamingStateChanged
            RemoveHandler ModVideoBack.ForcePlayChanged, AddressOf OnForcePlayChanged
            AddHandler ModVideoBack.GamingStateChanged, AddressOf OnGamingStateChanged
            AddHandler ModVideoBack.ForcePlayChanged, AddressOf OnForcePlayChanged
            If Setup.Get("UiAutoPauseVideo") = False Then ModVideoBack.ForcePlay = True
            '加载
            If Pic.Count = 0 Then
                If Refresh Then
                    If FrmMain.ImgBack.Visibility = Visibility.Collapsed Then
                        If IsHint Then Hint("未检测到可用背景内容！", HintType.Critical)
                    Else
                        FrmMain.ImgBack.Visibility = Visibility.Collapsed
                        If IsHint Then Hint("背景内容已清除！", HintType.Finish)
                    End If
                End If
                If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BackgroundRefreshUI(False, 0)
            Else
                If Refresh Then
                    Dim Address As String = RandomUtils.PickRandom(Pic)
                    Try
                        FrmMain.ImgBack.Background = Nothing
                        VideoStop()
                        Log("[UI] 加载背景内容：" & Address)
                        FrmMain.ImgBack.Background = New MyBitmap(Address)
                        Setup.Load("UiBackgroundSuit", True)
                        FrmMain.ImgBack.Visibility = Visibility.Visible
                        If IsHint Then Hint("背景内容已刷新：" & GetFileNameFromPath(Address), HintType.Finish, False)
                    Catch ex As Exception
                        Try
                            AddHandler FrmMain.VideoBack.MediaFailed, videoHandler
                            Log(ex,"[UI] 加载背景图片失败" & Address)
                            If ModeDebug Then Hint("图片加载失败，尝试将文件作为视频播放：" & Address)
                            FrmMain.ImgBack.Visibility = Visibility.Visible
                            FrmMain.VideoBack.Source = New Uri(Address, UriKind.Absolute)
                            VideoPlay()
                            If IsHint Then Hint("背景内容已刷新：" & GetFileNameFromPath(Address), HintType.Finish, False)
                        Catch playEx As Exception
                            Log(playEx,"播放背景内容时出现未知错误：")
                        End Try
                    End Try
                End If
                If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BackgroundRefreshUI(True, Pic.Count)
            End If

        Catch ex As Exception
            Log(ex, "刷新背景内容时出现未知错误", LogLevel.Feedback)
        End Try
    End Sub

    '顶部栏
    Private Sub BtnLogoChange_Click(sender As Object, e As EventArgs) Handles BtnLogoChange.Click
        Dim FileName As String = SystemDialogs.SelectFile("常用图片文件(*.png;*.jpg;*.gif;*.webp)|*.png;*.jpg;*.gif;*.webp", "选择图片")
        If FileName = "" Then Return
        Try
            '拷贝文件
            File.Delete(ExePath & "PCL\Logo.png")
            CopyFile(FileName, ExePath & "PCL\Logo.png")
            '设置当前显示
            FrmMain.ImageTitleLogo.Source = Nothing '防止因为 Source 属性前后的值相同而不更新 (#5628)
            FrmMain.ImageTitleLogo.Source = ExePath & "PCL\Logo.png"
        Catch ex As Exception
            If ex.Message.Contains("参数无效") Then
                Log("改变标题栏图片失败，该图片文件可能并非标准格式。" & vbCrLf &
                    "你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。", LogLevel.Msgbox)
            Else
                Log(ex, "设置标题栏图片失败", LogLevel.Msgbox)
            End If
            FrmMain.ImageTitleLogo.Source = Nothing
        End Try
    End Sub
    Private Sub RadioLogoType3_Check(sender As Object, e As RouteEventArgs) Handles RadioLogoType3.PreviewCheck
        If Not (AniControlEnabled = 0 AndAlso e.RaiseByMouse) Then Return
Refresh:
        '已有图片则不再选择
        If File.Exists(ExePath & "PCL\Logo.png") Then
            Try
                FrmMain.ImageTitleLogo.Source = Nothing '防止因为 Source 属性前后的值相同而不更新 (#5628)
                FrmMain.ImageTitleLogo.Source = ExePath & "PCL\Logo.png"
            Catch ex As Exception
                If ex.Message.Contains("参数无效") Then
                    Log("调整标题栏图片失败，该图片文件可能并非标准格式。" & vbCrLf &
                    "你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。", LogLevel.Msgbox)
                Else
                    Log(ex, "调整标题栏图片失败", LogLevel.Msgbox)
                End If
                FrmMain.ImageTitleLogo.Source = Nothing
                e.Handled = True
                Try
                    File.Delete(ExePath & "PCL\Logo.png")
                Catch exx As Exception
                    Log(exx, "清理错误的标题栏图片失败", LogLevel.Msgbox)
                End Try
            End Try
            Return
        End If
        '没有图片则要求选择
        Dim FileName As String = SystemDialogs.SelectFile("常用图片文件(*.png;*.jpg;*.gif;*.webp)|*.png;*.jpg;*.gif;*.webp", "选择图片")
        If FileName = "" Then
            FrmMain.ImageTitleLogo.Source = Nothing
            e.Handled = True
        Else
            Try
                '拷贝文件
                File.Delete(ExePath & "PCL\Logo.png")
                CopyFile(FileName, ExePath & "PCL\Logo.png")
                GoTo Refresh
            Catch ex As Exception
                Log(ex, "复制标题栏图片失败", LogLevel.Msgbox)
            End Try
        End If
    End Sub
    Private Sub BtnLogoDelete_Click(sender As Object, e As EventArgs) Handles BtnLogoDelete.Click
        Try
            File.Delete(ExePath & "PCL\Logo.png")
            RadioLogoType1.SetChecked(True, True)
            Hint("标题栏图片已清空！", HintType.Finish)
        Catch ex As Exception
            Log(ex, "清空标题栏图片失败", LogLevel.Msgbox)
        End Try
    End Sub

    '背景音乐
    Private Sub BtnMusicOpen_Click(sender As Object, e As EventArgs) Handles BtnMusicOpen.Click
        OpenExplorer(ExePath & "PCL\Musics\")
    End Sub
    Private Sub BtnMusicRefresh_Click(sender As Object, e As EventArgs) Handles BtnMusicRefresh.Click
        MusicRefreshPlay(True)
    End Sub
    Public Sub MusicRefreshUI()
        If PanBackgroundOpacity Is Nothing Then Return
        If MusicAllList.Any Then
            PanMusicVolume.Visibility = Visibility.Visible
            PanMusicDetail.Visibility = Visibility.Visible
            BtnMusicClear.Visibility = Visibility.Visible
            CardMusic.Title = "背景音乐（" & EnumerateFiles(ExePath & "PCL\Musics\").Count & " 首）"
        Else
            PanMusicVolume.Visibility = Visibility.Collapsed
            PanMusicDetail.Visibility = Visibility.Collapsed
            BtnMusicClear.Visibility = Visibility.Collapsed
            CardMusic.Title = "背景音乐"
        End If
        CardMusic.TriggerForceResize()
    End Sub
    Private Sub BtnMusicClear_Click(sender As Object, e As EventArgs) Handles BtnMusicClear.Click
        If MyMsgBox("即将删除背景音乐文件夹中的所有文件。" & vbCrLf & "此操作不可撤销，是否确定？", "警告",, "取消", IsWarn:=True) = 1 Then
            RunInThread(
            Sub()
                Hint("正在删除背景音乐……")
                '停止播放音乐
                MusicNAudio = Nothing
                MusicWaitingList = New List(Of String)
                MusicAllList = New List(Of String)
                Thread.Sleep(200)
                '删除文件
                Try
                    DeleteDirectory(ExePath & "PCL\Musics")
                    'DisableSMTCSupport()
                    Hint("背景音乐已删除！", HintType.Finish)
                Catch ex As Exception
                    Log(ex, "删除背景音乐失败", LogLevel.Msgbox)
                End Try
                Try
                    Directory.CreateDirectory(ExePath & "PCL\Musics")
                    RunInUi(Sub() MusicRefreshPlay(False))
                Catch ex As Exception
                    Log(ex, "重建背景音乐文件夹失败", LogLevel.Msgbox)
                End Try
            End Sub)
        End If
    End Sub
    Private Sub CheckMusicStart_Change() Handles CheckMusicStart.Change
        If AniControlEnabled <> 0 Then Return
        If CheckMusicStart.Checked Then CheckMusicStop.Checked = False
    End Sub
    Private Sub CheckMusicStop_Change() Handles CheckMusicStop.Change
        If AniControlEnabled <> 0 Then Return
        If CheckMusicStop.Checked Then CheckMusicStart.Checked = False
    End Sub

    '主页
    Private Sub BtnCustomFile_Click(sender As Object, e As EventArgs) Handles BtnCustomFile.Click
        Try
            If File.Exists(ExePath & "PCL\Custom.xaml") Then
                If MyMsgBox("当前已存在布局文件，继续生成教学文件将会覆盖现有布局文件！", "覆盖确认", "继续", "取消", IsWarn:=True) = 2 Then Return
            End If
            WriteFile(ExePath & "PCL\Custom.xaml", GetResourceStream("Resources/Custom.xml"))
            Hint("教学文件已生成！", HintType.Finish)
            OpenExplorer(ExePath & "PCL\Custom.xaml")
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

    '主题
    Private Sub ThemeColor_Change(sender As MyComboBox, e As EventArgs) Handles ComboDarkColor.SelectionChanged, ComboLightColor.SelectionChanged
        Setup.Set(sender.Tag, sender.SelectedIndex)
        ThemeRefresh()
    End Sub

    '主题自定义
    Private Sub RadioLauncherTheme14_Change(sender As Object, e As RouteEventArgs) Handles RadioLauncherTheme14.Changed
        'If RadioLauncherTheme14.Checked Then
        '    If LabLauncherHue.Visibility = Visibility.Visible Then Exit Sub
        '    LabLauncherHue.Visibility = Visibility.Visible
        '    SliderLauncherHue.Visibility = Visibility.Visible
        '    LabLauncherSat.Visibility = Visibility.Visible
        '    SliderLauncherSat.Visibility = Visibility.Visible
        '    LabLauncherDelta.Visibility = Visibility.Visible
        '    SliderLauncherDelta.Visibility = Visibility.Visible
        '    LabLauncherLight.Visibility = Visibility.Visible
        '    SliderLauncherLight.Visibility = Visibility.Visible
        'Else
        If LabLauncherHue.Visibility = Visibility.Collapsed Then Return
        LabLauncherHue.Visibility = Visibility.Collapsed
        SliderLauncherHue.Visibility = Visibility.Collapsed
        LabLauncherSat.Visibility = Visibility.Collapsed
        SliderLauncherSat.Visibility = Visibility.Collapsed
        LabLauncherDelta.Visibility = Visibility.Collapsed
        SliderLauncherDelta.Visibility = Visibility.Collapsed
        LabLauncherLight.Visibility = Visibility.Collapsed
        SliderLauncherLight.Visibility = Visibility.Collapsed
        'End If
        CardLauncher.TriggerForceResize()
    End Sub
    Private Sub HSL_Change() Handles SliderLauncherHue.Change, SliderLauncherLight.Change, SliderLauncherSat.Change, SliderLauncherDelta.Change
        If AniControlEnabled <> 0 OrElse SliderLauncherSat Is Nothing OrElse Not SliderLauncherSat.IsLoaded Then Return
#If False
        If EnableCustomTheme Then
            ColorHueTopbarDelta = SliderLauncherDelta.Value - 90
            ColorLightAdjust = SliderLauncherLight.Value - 20
        End If
#End If
        ThemeRefresh()
    End Sub

#Region "功能隐藏"

    Private Shared _HiddenForceShow As Boolean = False
    ''' <summary>
    ''' 是否强制显示被禁用的功能。
    ''' </summary>
    Public Shared Property HiddenForceShow As Boolean
        Get
            Return _HiddenForceShow
        End Get
        Set(value As Boolean)
            _HiddenForceShow = value
            HiddenRefresh()
        End Set
    End Property

    ''' <summary>
    ''' 更新功能隐藏带来的显示变化。
    ''' </summary>
    Public Shared Sub HiddenRefresh() Handles Me.Loaded
        If FrmMain.PanTitleSelect Is Nothing OrElse Not FrmMain.PanTitleSelect.IsLoaded Then Return
        Try
            ' 获取配置组引用以缩短代码
            Dim conf = Config.Preference.Hide

            ' 顶部栏：下载、设置、工具
            Dim IsAllTitleHidden As Boolean = Not HiddenForceShow AndAlso
                                    conf.PageDownload AndAlso
                                    conf.PageSetup AndAlso
                                    conf.PageTools

            If IsAllTitleHidden Then
                FrmMain.PanTitleSelect.Visibility = Visibility.Collapsed
            Else
                FrmMain.PanTitleSelect.Visibility = Visibility.Visible
                FrmMain.BtnTitleSelect1.Visibility = If(Not HiddenForceShow AndAlso conf.PageDownload, Visibility.Collapsed, Visibility.Visible)
                FrmMain.BtnTitleSelect2.Visibility = If(Not HiddenForceShow AndAlso conf.PageSetup, Visibility.Collapsed, Visibility.Visible)
                FrmMain.BtnTitleSelect3.Visibility = If(Not HiddenForceShow AndAlso conf.PageTools, Visibility.Collapsed, Visibility.Visible)
            End If

            ' 功能隐藏设置卡片
            If FrmSetupUI IsNot Nothing Then
                FrmSetupUI.CardSwitch.Visibility = If(Not HiddenForceShow AndAlso conf.FunctionHidden, Visibility.Collapsed, Visibility.Visible)
                FrmSetupUI.CardSwitch.Title = If(HiddenForceShow, "功能隐藏（已暂时关闭，按 F12 以重新启用）", "功能隐藏")
            End If

            ' 设置子页面 (FrmSetupLeft)
            If FrmSetupLeft IsNot Nothing Then
                FrmSetupLeft.ItemLaunch.Visibility = If(Not HiddenForceShow AndAlso conf.SetupLaunch, Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemUI.Visibility = If(Not HiddenForceShow AndAlso conf.SetupUi, Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemGameManage.Visibility = If(Not HiddenForceShow AndAlso conf.SetupGameManage, Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemLauncherMisc.Visibility = If(Not HiddenForceShow AndAlso conf.SetupLauncherMisc, Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemJava.Visibility = If(Not HiddenForceShow AndAlso conf.SetupJava, Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemUpdate.Visibility = If(Not HiddenForceShow AndAlso conf.SetupUpdate, Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemGameLink.Visibility = If(Not HiddenForceShow AndAlso conf.SetupGameLink, Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemAbout.Visibility = If(Not HiddenForceShow AndAlso conf.SetupAbout, Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemFeedback.Visibility = If(Not HiddenForceShow AndAlso conf.SetupFeedback, Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemLog.Visibility = If(Not HiddenForceShow AndAlso conf.SetupLog, Visibility.Collapsed, Visibility.Visible)

                Dim categories = {
    (FrmSetupLeft.TextGameCategory, Not (conf.SetupLaunch AndAlso conf.SetupJava AndAlso conf.SetupGameManage)),
    (FrmSetupLeft.TextToolsCategory, Not conf.SetupGameLink),
    (FrmSetupLeft.TextLauncherCategory, Not (conf.SetupUi AndAlso conf.SetupLauncherMisc)),
    (FrmSetupLeft.TextAboutCategory, Not (conf.SetupAbout AndAlso conf.SetupUpdate AndAlso conf.SetupFeedback AndAlso conf.SetupLog))
}

                For Each category In categories
                    Dim isVisible = category.Item2 OrElse HiddenForceShow
                    category.Item1.Visibility = If(isVisible, Visibility.Visible, Visibility.Collapsed)
                    If isVisible Then category.Item1.Opacity = 0.6
                Next

                ' 统计设置页可用项数量
                Dim SetupCount As Integer = 0
                If Not conf.SetupLaunch Then SetupCount += 1
                If Not conf.SetupUi Then SetupCount += 1
                If Not conf.SetupGameManage Then SetupCount += 1
                If Not conf.SetupLauncherMisc Then SetupCount += 1
                If Not conf.SetupJava Then SetupCount += 1
                If Not conf.SetupUpdate Then SetupCount += 1
                If Not conf.SetupGameLink Then SetupCount += 1
                If Not conf.SetupAbout Then SetupCount += 1
                If Not conf.SetupFeedback Then SetupCount += 1
                If Not conf.SetupLog Then SetupCount += 1
                FrmSetupLeft.PanItem.Visibility = If(SetupCount < 2 AndAlso Not HiddenForceShow, Visibility.Collapsed, Visibility.Visible)
            End If

            ' 工具子页面 (FrmToolsLeft)
            If FrmToolsLeft IsNot Nothing Then
                FrmToolsLeft.ItemGameLink.Visibility = If(Not HiddenForceShow AndAlso conf.ToolsGameLink, Visibility.Collapsed, Visibility.Visible)
                FrmToolsLeft.ItemLauncherHelp.Visibility = If(Not HiddenForceShow AndAlso conf.ToolsHelp, Visibility.Collapsed, Visibility.Visible)
                FrmToolsLeft.ItemTest.Visibility = If(Not HiddenForceShow AndAlso conf.ToolsTest, Visibility.Collapsed, Visibility.Visible)
    
                ' 处理分类标题
                Dim isGameLinkVisible = (Not HiddenForceShow AndAlso Not conf.ToolsGameLink) OrElse HiddenForceShow
                FrmToolsLeft.TextGameLinkCategory.Visibility = If(isGameLinkVisible, Visibility.Visible, Visibility.Collapsed)
                If isGameLinkVisible Then FrmToolsLeft.TextGameLinkCategory.Opacity = 0.6

                Dim isToolsVisible = (Not HiddenForceShow AndAlso (Not conf.ToolsHelp OrElse Not conf.ToolsTest)) OrElse HiddenForceShow
                FrmToolsLeft.TextToolsCategory.Visibility = If(isToolsVisible, Visibility.Visible, Visibility.Collapsed)
                If isToolsVisible Then FrmToolsLeft.TextToolsCategory.Opacity = 0.6
    
                ' 统计工具页可用项数量
                Dim ToolsCount As Integer = 0
                If Not conf.ToolsGameLink Then ToolsCount += 1
                If Not conf.ToolsHelp Then ToolsCount += 1
                If Not conf.ToolsTest Then ToolsCount += 1
                FrmToolsLeft.PanItem.Visibility = If(ToolsCount < 2 AndAlso Not HiddenForceShow, Visibility.Collapsed, Visibility.Visible)
            End If

            ' 其他入口刷新
            If FrmMain.PageCurrent = FormMain.PageType.InstanceSelect Then FrmSelectRight.BtnEmptyDownload_Loaded()
            If FrmMain.PageCurrent = FormMain.PageType.Launch Then FrmLaunchLeft.RefreshButtonsUI()
            If FrmMain.PageCurrent = FormMain.PageType.InstanceSetup AndAlso FrmInstanceModDisabled IsNot Nothing Then FrmInstanceModDisabled.BtnDownload_Loaded()

        Catch ex As Exception
            Log(ex, "刷新功能隐藏项目失败", LogLevel.Feedback)
        End Try
    End Sub

    ' ================= 设置页面协同 =================
    Private Sub HiddenSetupMain() Handles CheckHiddenPageSetup.Change
        Dim IsChecked As Boolean = CheckHiddenPageSetup.Checked
        CheckHiddenSetupLaunch.Checked = IsChecked
        CheckHiddenSetupUI.Checked = IsChecked
        CheckHiddenSetupGameManage.Checked = IsChecked
        CheckHiddenLauncherMisc.Checked = IsChecked
        CheckHiddenSetupJava.Checked = IsChecked
        CheckHiddenSetupUpdate.Checked = IsChecked
        CheckHiddenSetupGameLink.Checked = IsChecked
        CheckHiddenSetupAbout.Checked = IsChecked
        CheckHiddenSetupFeedback.Checked = IsChecked
        CheckHiddenSetupLog.Checked = IsChecked
    End Sub

    ' ================= 设置页面协同 =================
    Private Sub HiddenSetupMain(sender As Object, user As Boolean) Handles CheckHiddenPageSetup.Change
        If Not user Then Return ' 仅处理用户点击，防止死循环
        Dim IsChecked As Boolean = CheckHiddenPageSetup.Checked
        CheckHiddenSetupLaunch.Checked = IsChecked
        CheckHiddenSetupUI.Checked = IsChecked
        CheckHiddenSetupGameManage.Checked = IsChecked
        CheckHiddenLauncherMisc.Checked = IsChecked
        CheckHiddenSetupJava.Checked = IsChecked
        CheckHiddenSetupUpdate.Checked = IsChecked
        CheckHiddenSetupGameLink.Checked = IsChecked
        CheckHiddenSetupAbout.Checked = IsChecked
        CheckHiddenSetupFeedback.Checked = IsChecked
        CheckHiddenSetupLog.Checked = IsChecked
    End Sub

    Private Sub HiddenSetupSub(sender As Object, user As Boolean) Handles CheckHiddenSetupLaunch.Change, CheckHiddenSetupUI.Change,
    CheckHiddenSetupJava.Change, CheckHiddenSetupGameManage.Change, CheckHiddenLauncherMisc.Change, CheckHiddenSetupUpdate.Change, CheckHiddenSetupGameLink.Change,
    CheckHiddenSetupAbout.Change, CheckHiddenSetupFeedback.Change, CheckHiddenSetupLog.Change

        If Not user Then Return
        Dim conf = Config.Preference.Hide
        ' 判断是否全部勾选
        Dim AllChecked As Boolean = conf.SetupLaunch AndAlso conf.SetupUi AndAlso conf.SetupJava AndAlso
                               conf.SetupUpdate AndAlso conf.SetupGameLink AndAlso conf.SetupAbout AndAlso
                               conf.SetupFeedback AndAlso conf.SetupLog AndAlso conf.SetupLauncherMisc AndAlso conf.SetupGameManage
        CheckHiddenPageSetup.Checked = AllChecked
    End Sub

    ' ================= 工具页面协同 =================
    Private Sub HiddenToolsMain(sender As Object, user As Boolean) Handles CheckHiddenPageTools.Change
        If Not user Then Return
        Dim IsChecked As Boolean = CheckHiddenPageTools.Checked
        CheckHiddenToolsGameLink.Checked = IsChecked
        CheckHiddenToolsHelp.Checked = IsChecked
        CheckHiddenToolsTest.Checked = IsChecked
    End Sub

    Private Sub HiddenToolsSub(sender As Object, user As Boolean) Handles CheckHiddenToolsGameLink.Change,
    CheckHiddenToolsHelp.Change, CheckHiddenToolsTest.Change

        If Not user Then Return
        Dim conf = Config.Preference.Hide
        Dim AllChecked As Boolean = conf.ToolsGameLink AndAlso conf.ToolsHelp AndAlso conf.ToolsTest
        CheckHiddenPageTools.Checked = AllChecked
    End Sub

    '警告提示
    Private Sub HiddenHint(sender As Object, user As Boolean) Handles CheckHiddenFunctionHidden.Change, CheckHiddenPageSetup.Change, CheckHiddenSetupUI.Change
        If AniControlEnabled = 0 AndAlso sender.Checked Then Hint("按 F12 即可暂时关闭功能隐藏设置。千万别忘了，要不然设置就改不回来了……")
    End Sub

#End Region

    '赞助
    Private Sub BtnLauncherDonate_Click(sender As Object, e As EventArgs) Handles BtnLauncherDonate.Click
        OpenWebsite("https://afdian.com/a/LTCat")
    End Sub

    '滑动条
    Private Sub SliderLoad()
        SliderMusicVolume.GetHintText = Function(v) Math.Ceiling(v * 0.1) & "%"
        SliderLauncherOpacity.GetHintText = Function(v) Math.Round(40 + v * 0.1) & "%"
        SliderLauncherHue.GetHintText = Function(v) v & "°"
        SliderLauncherSat.GetHintText = Function(v) v & "%"
        SliderLauncherDelta.GetHintText =
        Function(Value As Integer) As String
            If Value > 90 Then
                Return "+" & (Value - 90)
            ElseIf Value = 90 Then
                Return 0
            Else
                Return Value - 90
            End If
        End Function
        SliderLauncherLight.GetHintText =
        Function(Value As Integer) As String
            If Value > 20 Then
                Return "+" & (Value - 20)
            ElseIf Value = 20 Then
                Return 0
            Else
                Return Value - 20
            End If
        End Function
        SliderBackgroundOpacity.GetHintText = Function(v) Math.Round(v * 0.1) & "%"
        SliderBackgroundBlur.GetHintText = Function(v) v & " 像素"
        SliderBlurValue.GetHintText = Function(v) v & " 像素"
        SliderBlurSamplingRate.GetHintText = Function(v) v & "%"
    End Sub
    Private Sub BtnHomepageMarket_Click(sender As Object, e As EventArgs) Handles BtnGotoHomepageMarket.Click
        FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.HomepageMarket})
    End Sub
End Class

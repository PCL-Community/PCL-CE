Imports PCL.Core.App
Imports PCL.Core.App.Configuration
Imports PCL.Core.UI
Imports PCL.Core.Utils.Exts

Class PageSetupSystem

    Private Shadows IsLoaded As Boolean = False

    Private Sub PageSetupSystem_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        Reload()
        SliderLoad()
        AniControlEnabled -= 1

    End Sub
    Public Sub Reload()

        '下载
        SliderDownloadThread.Value = Setup.Get("ToolDownloadThread")
        SliderDownloadSpeed.Value = Setup.Get("ToolDownloadSpeed")
        ComboDownloadSource.SelectedIndex = Setup.Get("ToolDownloadSource")
        ComboDownloadVersion.SelectedIndex = Setup.Get("ToolDownloadVersion")
        CheckDownloadAutoSelectVersion.Checked = Setup.Get("ToolDownloadAutoSelectVersion")
        CheckFixAuthlib.Checked = Setup.Get("ToolFixAuthlib")

        'Mod 与整合包
        ComboDownloadTranslateV2.SelectedIndex = Setup.Get("ToolDownloadTranslateV2")
        ComboDownloadMod.SelectedIndex = Setup.Get("ToolDownloadMod")
        ComboModLocalNameStyle.SelectedIndex = Setup.Get("ToolModLocalNameStyle")
        CheckDownloadIgnoreQuilt.Checked = Setup.Get("ToolDownloadIgnoreQuilt")
        CheckDownloadClipboard.Checked = Setup.Get("ToolDownloadClipboard")

        'Minecraft 更新提示
        CheckUpdateRelease.Checked = Setup.Get("ToolUpdateRelease")
        CheckUpdateSnapshot.Checked = Setup.Get("ToolUpdateSnapshot")

        '辅助设置
        CheckHelpChinese.Checked = Setup.Get("ToolHelpChinese")

        '系统设置
        ComboSystemActivity.SelectedIndex = Setup.Get("SystemSystemActivity")
        CheckSystemDisableHardwareAcceleration.Checked = Setup.Get("SystemDisableHardwareAcceleration")
        SliderAniFPS.Value = Setup.Get("UiAniFPS")
        SliderMaxLog.Value = Setup.Get("SystemMaxLog")
        CheckSystemTelemetry.Checked = Setup.Get("SystemTelemetry")

        '网络
        TextSystemHttpProxy.Text = Setup.Get("SystemHttpProxy")
        TextSystemHttpProxyCustomUsername.Text = Setup.Get("SystemHttpProxyCustomUsername")
        TextSystemHttpProxyCustomPassword.Text = Setup.Get("SystemHttpProxyCustomPassword")
        CType(FindName($"RadioHttpProxyType{Setup.Get("SystemHttpProxyType")}"), MyRadioBox).SetChecked(True, False)
        CheckNetDohEnable.Checked = Config.System.NetworkConfig.EnableDoH

        '调试选项
        CheckDebugMode.Checked = Setup.Get("SystemDebugMode")
        SliderDebugAnim.Value = Setup.Get("SystemDebugAnim")
        CheckDebugDelay.Checked = Setup.Get("SystemDebugDelay")
        CheckDebugSkipCopy.Checked = Setup.Get("SystemDebugSkipCopy")

    End Sub

    '初始化
    Public Sub Reset()
        Try
            Setup.Reset("ToolDownloadThread")
            Setup.Reset("ToolDownloadSpeed")
            Setup.Reset("ToolDownloadSource")
            Setup.Reset("ToolDownloadVersion")
            Setup.Reset("ToolDownloadTranslateV2")
            Setup.Reset("ToolDownloadIgnoreQuilt")
            Setup.Reset("ToolDownloadClipboard")
            Setup.Reset("ToolDownloadMod")
            Setup.Reset("ToolDownloadAutoSelectVersion")
            Setup.Reset("ToolFixAuthlib")
            Setup.Reset("ToolModLocalNameStyle")
            Setup.Reset("ToolUpdateRelease")
            Setup.Reset("ToolUpdateSnapshot")
            Setup.Reset("ToolHelpChinese")
            Setup.Reset("SystemDebugMode")
            Setup.Reset("SystemDebugAnim")
            Setup.Reset("SystemDebugDelay")
            Setup.Reset("SystemDebugSkipCopy")
            Setup.Reset("SystemSystemUpdate")
            Setup.Reset("SystemSystemActivity")
            Setup.Reset("SystemDisableHardwareAcceleration")
            Setup.Reset("SystemHttpProxy")
            Setup.Reset("SystemHttpProxyType")
            Setup.Reset("SystemHttpProxyCustomUsername")
            Setup.Reset("SystemHttpProxyCustomPassword")
            Setup.Reset("SystemUseDefaultProxy")
            Config.System.NetworkConfig.Reset()
            Setup.Reset("UiAniFPS")

            Log("[Setup] 已初始化启动器页设置")
            Hint("已初始化启动器页设置！", HintType.Finish, False)
        Catch ex As Exception
            Log(ex, "初始化启动器页设置失败", LogLevel.Msgbox)
        End Try

        Reload()
    End Sub

    '将控件改变路由到设置改变
    Private Shared Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles CheckDebugMode.Change, CheckDebugDelay.Change, CheckDebugSkipCopy.Change, CheckUpdateRelease.Change, CheckUpdateSnapshot.Change, CheckHelpChinese.Change, CheckDownloadIgnoreQuilt.Change, CheckDownloadClipboard.Change, CheckSystemDisableHardwareAcceleration.Change, CheckDownloadAutoSelectVersion.Change, CheckSystemTelemetry.Change, CheckFixAuthlib.Change, CheckNetDohEnable.Change
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Checked)
    End Sub
    Private Shared Sub SliderChange(sender As MySlider, e As Object) Handles SliderDebugAnim.Change, SliderDownloadThread.Change, SliderDownloadSpeed.Change, SliderAniFPS.Change, SliderMaxLog.Change
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Value)
    End Sub
    Private Shared Sub ComboChange(sender As MyComboBox, e As Object) Handles ComboDownloadVersion.SelectionChanged, ComboModLocalNameStyle.SelectionChanged, ComboDownloadTranslateV2.SelectionChanged, ComboSystemActivity.SelectionChanged, ComboDownloadSource.SelectionChanged, ComboDownloadMod.SelectionChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.SelectedIndex)
    End Sub
    Private Shared Sub RadioBoxChange(sender As MyRadioBox, e As Object) Handles RadioHttpProxyType0.Check, RadioHttpProxyType1.Check, RadioHttpProxyType2.Check
        Dim gotCfg = sender.Tag.ToString.Split("/")
        If AniControlEnabled = 0 Then Setup.Set(gotCfg(0), Integer.Parse(gotCfg(1)))
    End Sub

    '网络
    Private Sub ApplyHttpProxyBtn_OnClicked(sender As Object, e As MouseButtonEventArgs) Handles BtnApplyHttpProxy.Click
        Setup.Set("SystemHttpProxy", TextSystemHttpProxy.Text)
        Setup.Set("SystemHttpProxyCustomPassword", TextSystemHttpProxyCustomPassword.Text)
        Setup.Set("SystemHttpProxyCustomUsername", TextSystemHttpProxyCustomUsername.Text)
    End Sub

    '滑动条
    Private Sub SliderLoad()
        SliderDownloadThread.GetHintText = Function(v) v + 1
        SliderDownloadSpeed.GetHintText =
        Function(v)
            Select Case v
                Case Is <= 14
                    Return $"{(v + 1) * 0.1:F1} M/s"
                Case Is <= 31
                    Return $"{(v - 11) * 0.5:F1} M/s"
                Case Is <= 41
                    Return (v - 21) & " M/s"
                Case Else
                    Return "无限制"
            End Select
        End Function
        SliderDebugAnim.GetHintText = Function(v) If(v > 29, "关闭", Math.Round((v / 10 + 0.1), 1) & "x")
        SliderAniFPS.GetHintText =
            Function(v)
                Return $"{v + 1} FPS"
            End Function
        SliderMaxLog.GetHintText =
            Function(v)
                'y = 10x + 50 (0 <= x <= 5, 50 <= y <= 100)
                'y = 50x - 150 (5 < x <= 13, 100 < y <= 500)
                'y = 100x - 800 (13 < x <= 28, 500 < y <= 2000)
                Select Case v
                    Case Is <= 5
                        Return v * 10 + 50
                    Case Is <= 13
                        Return v * 50 - 150
                    Case Is <= 28
                        Return v * 100 - 800
                    Case Else
                        Return "无限制"
                End Select
            End Function
    End Sub
    Private Sub SliderDownloadThread_PreviewChange(sender As Object, e As RouteEventArgs) Handles SliderDownloadThread.PreviewChange
        If SliderDownloadThread.Value < 100 Then Return
        If Not Setup.Get("HintDownloadThread") Then
            Setup.Set("HintDownloadThread", True)
            MyMsgBox("如果设置过多的下载线程，可能会导致下载时出现非常严重的卡顿。" & vbCrLf &
                     "一般设置 64 线程即可满足大多数下载需求，除非你知道你在干什么，否则不建议设置更多的线程数！", "警告", "我知道了", IsWarn:=True)
        End If
    End Sub

    '硬件加速
    Private Sub Check_DisableHardwareAcceleration(sender As Object, user As Boolean) Handles CheckSystemDisableHardwareAcceleration.Change
        Hint("此项变更将在重启 PCL 后生效")
    End Sub

    '调试模式
    Private Sub CheckDebugMode_Change() Handles CheckDebugMode.Change
        If AniControlEnabled = 0 Then Hint("部分调试信息将在刷新或启动器重启后切换显示！",, False)
    End Sub

    '自动更新
    Private Sub ComboSystemActivity_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboSystemActivity.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        If ComboSystemActivity.SelectedIndex <> 2 Then Return
        If MyMsgBox("若选择此项，即使在将来出现严重问题时，你也无法获取相关通知。" & vbCrLf &
                    "例如，如果发现某个版本游戏存在严重 Bug，你可能就会因为无法得到通知而导致无法预知的后果。" & vbCrLf & vbCrLf &
                    "一般选择 仅在有重要通知时显示公告 就可以让你尽量不受打扰了。" & vbCrLf &
                    "除非你在制作服务器整合包，或时常手动更新启动器，否则极度不推荐选择此项！", "警告", "我知道我在做什么", "取消", IsWarn:=True) = 2 Then
            ComboSystemActivity.SelectedItem = e.RemovedItems(0)
        End If
    End Sub

#Region "导出 / 导入设置"

    Private Sub BtnSystemSettingExp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingExp.Click
        Dim savePath As String = SystemDialogs.SelectSaveFile("选择保存位置", "PCL 全局配置.json", "PCL 配置文件(*.json)|*.json", ExePath)
        If savePath.IsNullOrWhiteSpace() Then Exit Sub
        File.Copy(ConfigService.SharedConfigPath, savePath, True)
        Hint("配置导出成功！", HintType.Finish)
        OpenExplorer(savePath)
    End Sub
    Private Sub BtnSystemSettingImp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingImp.Click
        Dim sourcePath As String = SystemDialogs.SelectFile("PCL 配置文件(*.json)|*.json", "选择配置文件")
        If sourcePath.IsNullOrWhiteSpace() Then Exit Sub
        File.Copy(sourcePath, ConfigService.SharedConfigPath, True)
        MyMsgBox("配置导入成功！请重启 PCL 以应用配置……", Button1:="重启", ForceWait:=True)
        Process.Start(New ProcessStartInfo(ExePathWithName))
        FormMain.EndProgramForce(ProcessReturnValues.Success)
    End Sub

#End Region

End Class

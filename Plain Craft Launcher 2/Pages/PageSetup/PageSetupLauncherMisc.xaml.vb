Imports PCL.Core.App
Imports PCL.Core.App.Configuration
Imports PCL.Core.UI
Imports PCL.Core.Utils.Exts

Class PageSetupLauncherMisc

    Private Shadows IsLoaded As Boolean = False
    Private IsFirstLoad As Boolean = True
    Private Sub PageSetupLink_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        SliderLoad()
        Reload()
        AniControlEnabled -= 1

    End Sub
    Public Sub Reload() Handles Me.Loaded
        '系统设置
        ComboSystemActivity.SelectedIndex = States.System.AnnounceSolution
        CheckSystemDisableHardwareAcceleration.Checked = Config.System.DisableHardwareAcceleration
        SliderAniFPS.Value = Config.System.AnimationFpsLimit
        SliderMaxLog.Value = Config.System.MaxGameLog
        CheckSystemTelemetry.Checked = Config.System.Telemetry

        '网络
        TextSystemHttpProxy.Text = Config.Network.HttpProxy.CustomAddress
        TextSystemHttpProxyCustomUsername.Text = Config.Network.HttpProxy.CustomUsername
        TextSystemHttpProxyCustomPassword.Text = Config.Network.HttpProxy.CustomPassword
        CType(FindName($"RadioHttpProxyType{Config.Network.HttpProxy.Type}"), MyRadioBox).SetChecked(True, False)
        CheckNetDohEnable.Checked = Config.Network.EnableDoH

        '调试选项
        CheckDebugMode.Checked = Config.Debug.Enabled
        SliderDebugAnim.Value = Config.Debug.AnimationSpeed
        CheckDebugDelay.Checked = Config.Debug.DontCopy
    End Sub
    '初始化
    Public Sub Reset()
        Try
            Config.Network.Reset()
            Config.Debug.Reset()
            Config.System.Reset()
            Log("[Setup] 已初始化启动器-杂项页设置")
            Hint("已初始化杂项页设置！", HintType.Finish, False)
            Reload()
        Catch ex As Exception
            Log(ex, "初始化启动器-杂项页设置失败", LogLevel.Msgbox)
        End Try

        Reload()
    End Sub

    '将控件改变路由到设置改变
    Private Shared Sub ComboChange(sender As MyComboBox, e As Object) Handles ComboSystemActivity.SelectionChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.SelectedIndex)
    End Sub
    Private Shared Sub RadioBoxChange(sender As MyRadioBox, e As Object) Handles RadioHttpProxyType0.Check, RadioHttpProxyType1.Check, RadioHttpProxyType2.Check
        Dim gotCfg = sender.Tag.ToString.Split("/")
        If AniControlEnabled = 0 Then Setup.Set(gotCfg(0), Integer.Parse(gotCfg(1)))
    End Sub
    Private Shared Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles CheckDebugMode.Change, CheckDebugDelay.Change, CheckDebugSkipCopy.Change, CheckSystemDisableHardwareAcceleration.Change, CheckSystemTelemetry.Change, CheckNetDohEnable.Change
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Checked)
    End Sub
    Private Shared Sub SliderChange(sender As MySlider, e As Object) Handles SliderDebugAnim.Change, SliderAniFPS.Change, SliderMaxLog.Change
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Value)
    End Sub

    '网络
    Private Sub ApplyHttpProxyBtn_OnClicked(sender As Object, e As MouseButtonEventArgs) Handles BtnApplyHttpProxy.Click
        Config.Network.HttpProxy.CustomAddress = TextSystemHttpProxy.Text
        Config.Network.HttpProxy.CustomUsername = TextSystemHttpProxyCustomUsername.Text
        Config.Network.HttpProxy.CustomPassword = TextSystemHttpProxyCustomPassword.Text
    End Sub

    '滑动条
    Private Sub SliderLoad()
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

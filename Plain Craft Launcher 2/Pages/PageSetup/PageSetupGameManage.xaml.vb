Imports PCL.Core.App
Imports PCL.Core.App.Configuration
Imports PCL.Core.UI
Imports PCL.Core.Utils.Exts

Class PageSetupGameManage

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

    End Sub

    '初始化
    Public Sub Reset()
        Try
            Config.Download.Reset()
            Config.Tool.Reset()
            Log("[Setup] 已初始化其他页设置")
            Hint("已初始化其他页设置！", HintType.Finish, False)
        Catch ex As Exception
            Log(ex, "初始化其他页设置失败", LogLevel.Msgbox)
        End Try

        Reload()
    End Sub

    '将控件改变路由到设置改变
    Private Shared Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles CheckUpdateRelease.Change, CheckUpdateSnapshot.Change, CheckHelpChinese.Change, CheckDownloadIgnoreQuilt.Change, CheckDownloadClipboard.Change, CheckDownloadAutoSelectVersion.Change, CheckFixAuthlib.Change
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Checked)
    End Sub
    Private Shared Sub SliderChange(sender As MySlider, e As Object) Handles SliderDownloadThread.Change, SliderDownloadSpeed.Change
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Value)
    End Sub
    Private Shared Sub ComboChange(sender As MyComboBox, e As Object) Handles ComboDownloadVersion.SelectionChanged, ComboModLocalNameStyle.SelectionChanged, ComboDownloadTranslateV2.SelectionChanged, ComboDownloadSource.SelectionChanged, ComboDownloadMod.SelectionChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.SelectedIndex)
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
    End Sub
    Private Sub SliderDownloadThread_PreviewChange(sender As Object, e As RouteEventArgs) Handles SliderDownloadThread.PreviewChange
        If SliderDownloadThread.Value < 100 Then Return
        If Not Setup.Get("HintDownloadThread") Then
            Setup.Set("HintDownloadThread", True)
            MyMsgBox("如果设置过多的下载线程，可能会导致下载时出现非常严重的卡顿。" & vbCrLf &
                     "一般设置 64 线程即可满足大多数下载需求，除非你知道你在干什么，否则不建议设置更多的线程数！", "警告", "我知道了", IsWarn:=True)
        End If
    End Sub

End Class

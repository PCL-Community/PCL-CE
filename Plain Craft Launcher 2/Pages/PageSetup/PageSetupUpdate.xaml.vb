Imports PCL.Core.App
Imports PCL.Core.Utils

Public Class PageSetupUpdate
    Public UpdateInfo As VersionDataModel = Nothing
    
    Private Enum UpdateStatus
        Checking = 0
        Available = 1
        [Error] = 2
        Latest = 3
    End Enum
    
    Private Function IsLatest() As UpdateStatus
        Try
            If RemoteServer.IsLatest(
                If(IsUpdBetaChannel, UpdateChannel.beta, UpdateChannel.stable),
                If(IsArm64System, UpdateArch.arm64, UpdateArch.x64),
                SemVer.Parse(VersionBaseName),
                VersionCode) Then
                Return UpdateStatus.Latest
            Else 
                Return UpdateStatus.Available
            End If
        Catch ex As Exception
            Log(ex, "无法获取最新版本信息，请检查网络连接", LogLevel.Hint)
            Return UpdateStatus.Error
        End Try
    End Function
    
    Public Sub CheckUpdate()
        Select Case IsLatest()
            Case UpdateStatus.Available
                Dim checkUpdateEx As Exception = Nothing
                RunInNewThread(
                    Sub()
                        Try
                            UpdateInfo = RemoteServer.GetLatestVersion(
                                If(IsUpdBetaChannel, UpdateChannel.beta, UpdateChannel.stable),
                                If(IsArm64System, UpdateArch.arm64, UpdateArch.x64))
                        Catch ex As Exception
                            checkUpdateEx = ex
                        End Try
                    End Sub
                    ).Join()
                If UpdateInfo Is Nothing Then
                    Log(checkUpdateEx, "[Update] 检查更新失败", LogLevel.Msgbox)
                    Exit Sub
                End If
            Case UpdateStatus.Latest
                CardUpdate.Visibility = Visibility.Collapsed
                CardCheck.Visibility = Visibility.Visible
            Case UpdateStatus.Error
                CardUpdate.Visibility = Visibility.Collapsed
                CardCheck.Visibility = Visibility.Visible
                TextCurrentDesc.Text = "检查更新时出错"
        End Select
    End Sub
    
    Private Sub PageSetupUpdate_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        AniControlEnabled += 1
        ComboSystemUpdateChannel.SelectedIndex = Setup.Get("SystemSystemUpdateBranch")
        ComboSystemUpdateMode.SelectedIndex = Setup.Get("SystemSystemUpdate")
        AniControlEnabled -= 1
    End Sub
    
    Private Sub BtnUpdate_Click(sender As Object, e As EventArgs) Handles BtnUpdate.Click
        '检查 .NET 版本
        If Not UpdateInfo.VersionName.StartsWithF("2.13.") AndAlso Not ShellAndGetOutput("cmd", "/c dotnet --list-runtimes").ContainsF("Microsoft.WindowsDesktop.App 10.0.", True) Then
            MyMsgBox($"发现了启动器更新（版本 {UpdateInfo.VersionName}），但是新版本要求你的电脑安装 .NET 10 才可以运行。{vbCrLf}你需要先安装 .NET 10 才可以继续更新。{vbCrLf}{vbCrLf}点击下方按钮打开网页，然后选择 ⌈.NET 桌面运行时⌋ 中的 {If(IsArm64System, "Arm64", "x64")} 选项下载。", "启动器更新 - 缺少运行环境",
                     "下载 .NET 10 运行时", "取消", Button1Action:=Sub() OpenWebsite($"https://get.dot.net/10"), ForceWait:=True)
            Return
        End If
        '开始更新流程
        UpdateStart(False)
        'FrmMain.BtnExtraUpdateRestart.ShowRefresh()
        'FrmMain.BtnExtraUpdateRestart.Ribble()
    End Sub
    
    Private Sub BtnChangelogDetail_Click(sender As Object, e As EventArgs) Handles BtnChangelogDetail.Click
        MyMsgBoxMarkdown("", "关于此更新")
    End Sub
    
    Private Sub ComboSystemUpdateBranch_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboSystemUpdateChannel.SelectionChanged
        If AniControlEnabled <> 0 Then Exit Sub
        Select Case ComboSystemUpdateChannel.SelectedIndex
            Case 0
                Return
            Case 1
                If MyMsgBox("你正在切换启动器更新通道到测试版。" & vbCrLf &
                            "测试版可以提供下个版本更新内容的预览，但可能会包含未经充分测试的功能，稳定性欠佳。" & vbCrLf & vbCrLf &
                            "在升级到测试版后，你需要等待下一个正式版发布，或是手动重新下载启动器来切换到正式版。" & vbCrLf &
                            "该选项仅推荐具有一定基础知识和能力的用户选择。如果你正在制作整合包，请使用正式版！", "继续之前...", "我已知晓", "取消", IsWarn:=True) = 2 Then
                    ComboSystemUpdateChannel.SelectedItem = e.RemovedItems(0)
                Else
                    UpdateCheckByButton()
                End If
            Case 2
                If MyMsgBox("你正在切换启动器更新通道到开发版。" & vbCrLf &
                            "该通道可第一时间获取基于最新代码构建的开发版本，但可能极不稳定，甚至直接无法启动。" & vbCrLf & vbCrLf &
                            "在升级到开发版后，只能手动重新下载启动器来切换回正式版或测试版。" & vbCrLf &
                            "该选项仅推荐高级用户选择。如果你正在制作整合包，请使用正式版！", "继续之前...", "我已知晓", "取消", IsWarn:=True) = 2 Then
                    ComboSystemUpdateChannel.SelectedItem = e.RemovedItems(0)
                    Return
                End If
                Dim ret = MyMsgBoxInput("最终确认", "你确定要切换到开发版通道吗？" & vbCrLf &
                                                "开发版可能存在严重问题，甚至无法启动！" & vbCrLf &
                                                "在升级到开发版后，将无法切换回其他任何更新通道，只能手动重新下载启动器来切换回正式版或测试版。" & vbCrLf & vbCrLf &
                                                "该选项仅推荐高级用户选择。如果你正在制作整合包，请使用正式版！" & vbCrLf & 
                                                "请输入 '我确认切换到此分支并已知晓风险' 以确认。", Button1 := "提交", Button2 := "取消", IsWarn:=True)
                If ret Is Nothing Then 
                    ComboSystemUpdateChannel.SelectedItem = e.RemovedItems(0)
                    Return
                End If
                If ret = "我确认切换到此分支并已知晓风险" Then
                    UpdateCheckByButton()
                Else
                    Hint("你输入了错误的内容...")
                    ComboSystemUpdateChannel.SelectedItem = e.RemovedItems(0)
                End If
        End Select
    End Sub
    
    Private Sub TextMirrorCDK_PasswordChanged(sender As Object, e As EventArgs) Handles TextMirrorCDK.PasswordChanged
        Config.System.MirrorChyanKey = TextMirrorCDK.Password
    End Sub
    
    Private Sub BtnGetMirrorCDK_Click(sender As Object, e As EventArgs) Handles BtnGetMirrorCDK.Click
        OpenWebsite("https://mirrorchyan.com/")
    End Sub
    
End Class

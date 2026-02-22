Imports PCL.Core.App
Imports PCL.Core.Utils

Public Class PageSetupUpdate
    Private Sub Init() Handles Me.Loaded
        AniControlEnabled += 1
        TextMirrorCDK.Password = Config.Update.MirrorChyanKey
        ComboSystemUpdateChannel.SelectedIndex = Config.Update.UpdateChannel
        ComboSystemUpdateMode.SelectedIndex = Config.Update.UpdateMode
        TextCurrentVersion.Text = "PCL CE " & VersionNameFormat(VersionBaseName)
        AniControlEnabled -= 1
        CheckUpdate()
    End Sub
    
    Public UpdateInfo As VersionDataModel = Nothing
    
    Private Enum UpdateStatus
        Checking = 0
        Available = 1
        [Error] = 2
        Latest = 3
    End Enum
    
    Private Async Function IsLatestAsync() As Task(Of UpdateStatus)
        Try
            If Await RemoteServer.IsLatestAsync(
                If(IsCurrentVersionBeta, UpdateChannel.beta, UpdateChannel.stable),
                If(IsArm64System, UpdateArch.arm64, UpdateArch.x64),
                SemVer.Parse(VersionBaseName),
                VersionCode) Then
                Log("[Update] 已是最新版本")
                Return UpdateStatus.Latest
            Else 
                Log("[Update] 有可用的新版本")
                Return UpdateStatus.Available
            End If
        Catch ex As Exception
            Log(ex, "无法获取最新版本信息，请检查网络连接", LogLevel.Hint)
            Return UpdateStatus.Error
        End Try
    End Function
    
    Public Async Sub CheckUpdate() Handles BtnCheckAgain.Click
        Log("[Update] 开始检查更新")
        CardUpdate.Visibility = Visibility.Collapsed
        CardCheck.Visibility = Visibility.Visible
        TextCurrentDesc.Text = "正在检查更新..."
        BtnCheckAgain.IsEnabled = False
        Select Case Await IsLatestAsync()
            Case UpdateStatus.Available
                Dim checkUpdateEx As Exception = Nothing
                Try
                    UpdateInfo = RemoteServer.GetLatestVersion(
                        If(IsCurrentVersionBeta, UpdateChannel.beta, UpdateChannel.stable),
                        If(IsArm64System, UpdateArch.arm64, UpdateArch.x64))
                    TextUpdateName.Text = "PCL CE " & VersionNameFormat(UpdateInfo.VersionName)
                    Dim summary = UpdateInfo.Changelog.Between("<summary>", "</summary>")
                    If Not UpdateInfo.Changelog.Contains("<summary>") OrElse String.IsNullOrWhiteSpace(summary.Trim()) Then
                        TextChangelog.Text = "开发者似乎忘记提供更新摘要了...也许你可以点击下方看看完整更新日志？"
                    Else
                        TextChangelog.Text = summary
                    End If
                Catch ex As Exception
                    checkUpdateEx = ex
                End Try
                BtnCheckAgain.IsEnabled = True
                If UpdateInfo Is Nothing Then
                    TextCurrentDesc.Text = "检查更新时出错"
                    If checkUpdateEx IsNot Nothing Then
                        Log(checkUpdateEx, "[Update] 检查更新失败", LogLevel.Msgbox)
                    Else 
                        Log("[Update] 检查更新失败", LogLevel.Msgbox)
                    End If
                    Exit Sub
                End If
                If UpdateLoader IsNot Nothing AndAlso UpdateLoader.State = LoadState.Loading Then
                    BtnUpdate_Timer()
                    BtnUpdate.IsEnabled = False
                ElseIf IsUpdateWaitingRestart Then
                    BtnUpdate.Text = "重启安装"
                    BtnUpdate.IsEnabled = True
                Else
                    BtnUpdate.Text = "下载并安装"
                    BtnUpdate.IsEnabled = True
                End If
                CardUpdate.Visibility = Visibility.Visible
                CardCheck.Visibility = Visibility.Collapsed
            Case UpdateStatus.Latest
                CardUpdate.Visibility = Visibility.Collapsed
                CardCheck.Visibility = Visibility.Visible
                BtnCheckAgain.IsEnabled = True
                TextCurrentDesc.Text = "已是最新版本"
            Case UpdateStatus.Error
                CardUpdate.Visibility = Visibility.Collapsed
                CardCheck.Visibility = Visibility.Visible
                BtnCheckAgain.IsEnabled = True
                TextCurrentDesc.Text = "检查更新时出错"
        End Select
    End Sub
    
    Public Sub BtnUpdate_Timer()
        While UpdateLoader IsNot Nothing AndAlso UpdateLoader.State = LoadState.Loading
            RunInUi(Sub() BtnUpdate.Text = $"{Math.Round(UpdateLoader.Progress, 2)}%")
            Thread.Sleep(200)
        End While
    End Sub

    Private Sub BtnUpdate_Click(sender As Object, e As EventArgs) Handles BtnUpdate.Click
        '检查 .NET 版本
        If Not UpdateInfo.VersionName.StartsWithF("2.13.") AndAlso Not ShellAndGetOutput("cmd", "/c dotnet --list-runtimes").ContainsF("Microsoft.WindowsDesktop.App 8.0.", True) Then
            MyMsgBox($"发现了启动器更新（版本 {UpdateInfo.VersionName}），但是新版本要求你的电脑安装 .NET 8 才可以运行。{vbCrLf}你需要先安装 .NET 8 才可以继续更新。{vbCrLf}{vbCrLf}点击下方按钮打开网页，然后选择 ⌈.NET 桌面运行时⌋ 中的 {If(IsArm64System, "Arm64", "x64")} 选项下载。", "启动器更新 - 缺少运行环境",
                     "下载 .NET 8 运行时", "取消", Button1Action:=Sub() OpenWebsite($"https://get.dot.net/8"), ForceWait:=True)
            Return
        End If
        If IsUpdateWaitingRestart Then
            UpdateRestart(True, True)
        End If
        '开始更新流程
        UpdateStart(UpdateType.UpdateNow)
    End Sub

    Private Sub BtnChangelogDetail_Click(sender As Object, e As EventArgs) Handles BtnChangelogDetail.Click
        If UpdateInfo Is Nothing Then
            MyMsgBox("没有可用的更新日志...", "关于此更新")
        Else
            MyMsgBoxMarkdown(UpdateInfo.Changelog, "关于此更新")
        End If
    End Sub
    
    Private Sub ComboSystemUpdateMode_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboSystemUpdateMode.SelectionChanged
        If AniControlEnabled = 0 Then Config.Update.UpdateMode = ComboSystemUpdateMode.SelectedIndex
    End Sub
    
    Private Sub ComboSystemUpdateBranch_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboSystemUpdateChannel.SelectionChanged
        If AniControlEnabled <> 0 Then Exit Sub
        
        Dim IsCancelled As Boolean = False
        Select Case ComboSystemUpdateChannel.SelectedIndex
            Case 0
            Case 1
                If MyMsgBox("你正在切换启动器更新通道到测试版。" & vbCrLf &
                            "测试版可以提供下个版本更新内容的预览，但可能会包含未经充分测试的功能，稳定性欠佳。" & vbCrLf & vbCrLf &
                            "在升级到测试版后，你需要等待下一个正式版发布，或是手动重新下载启动器来切换到正式版。" & vbCrLf &
                            "该选项仅推荐具有一定基础知识和能力的用户选择。如果你正在制作整合包，请使用正式版！", "继续之前...", "我已知晓", "取消", IsWarn:=True) = 2 Then
                    IsCancelled = True
                Else
                    CheckUpdate()
                End If
            Case 2
                If MyMsgBox("你正在切换启动器更新通道到开发版。" & vbCrLf &
                            "该通道可第一时间获取基于最新代码构建的开发版本，但可能极不稳定，甚至直接无法启动。" & vbCrLf & vbCrLf &
                            "在升级到开发版后，只能手动重新下载启动器来切换回正式版或测试版。" & vbCrLf &
                            "该选项仅推荐高级用户选择。如果你正在制作整合包，请使用正式版！", "继续之前...", "我已知晓", "取消", IsWarn:=True) = 2 Then
                    IsCancelled = True
                    Exit Select
                End If
                Dim ret = MyMsgBoxInput("最终确认", "你确定要切换到开发版通道吗？" & vbCrLf &
                                                "开发版可能存在严重问题，甚至无法启动！" & vbCrLf &
                                                "在升级到开发版后，将无法切换回其他任何更新通道，只能手动重新下载启动器来切换回正式版或测试版。" & vbCrLf & vbCrLf &
                                                "该选项仅推荐高级用户选择。如果你正在制作整合包，请使用正式版！" & vbCrLf & 
                                                "请输入 '我确认切换到此分支并已知晓风险' 以确认。", Button1 := "提交", Button2 := "取消", IsWarn:=True)
                If ret Is Nothing Then 
                    IsCancelled = True
                    Exit Select
                End If
                If ret = "我确认切换到此分支并已知晓风险" Then
                    CheckUpdate()
                Else
                    Hint("你输入了错误的内容...")
                    IsCancelled = True
                End If
        End Select
        If IsCancelled Then
            AniControlEnabled += 1
            ComboSystemUpdateChannel.SelectedItem = e.RemovedItems(0)
            AniControlEnabled -= 1
        Else
            Config.Update.UpdateChannel = ComboSystemUpdateChannel.SelectedIndex
        End If
    End Sub
    
    Private Sub TextMirrorCDK_PasswordChanged(sender As Object, e As EventArgs) Handles TextMirrorCDK.PasswordChanged
        Config.Update.MirrorChyanKey = TextMirrorCDK.Password
    End Sub
    
    Private Sub BtnGetMirrorCDK_Click(sender As Object, e As EventArgs) Handles BtnGetMirrorCDK.Click
        OpenWebsite("https://mirrorchyan.com/")
    End Sub
    
    Private Sub BtnChangelog_Click(sender As Object, e As EventArgs) Handles BtnChangelog.Click
        OpenWebsite("https://github.com/PCL-Community/PCL2-CE/releases/v" & VersionBaseName)
    End Sub
    
    Public Function VersionNameFormat(str As String) As String
        str = str.Replace("v", "")
        If Not str.Contains("-") Then Return str
        Dim add = str.AfterLast("-")
        str = str.BeforeLast("-")
        Return str & " " & add.Replace(".", " ").Replace("beta", "Beta").Replace("rc", "RC")
    End Function
    
End Class

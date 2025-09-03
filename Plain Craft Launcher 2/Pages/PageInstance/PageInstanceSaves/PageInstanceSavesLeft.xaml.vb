Public Class PageInstanceSavesLeft
    Implements IRefreshable

#Region "龙猫牌 页面管理"

    ''' <summary>
    ''' 当前页面的编号。从 0 开始计算。
    ''' </summary>
    Public PageID As FormMain.PageSubType = FormMain.PageSubType.Default

    ''' <summary>
    ''' 勾选事件改变页面。
    ''' </summary>
    Private Sub PageCheck(sender As MyListItem, e As RouteEventArgs) Handles ItemBackup.Check, ItemInfo.Check, ItemDatapacks.Check
        '尚未初始化控件属性时，sender.Tag 为 Nothing，会导致切换到页面 0
        '若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        If sender.Tag IsNot Nothing Then PageChange(Val(sender.Tag))
    End Sub
    Public Function PageGet(Optional ID As FormMain.PageSubType = -1)
        If ID = -1 Then ID = PageID
        Select Case ID
            Case FormMain.PageSubType.VersionSavesInfo
                If FrmInstanceSavesInfo Is Nothing Then FrmInstanceSavesInfo = New PageInstanceSavesInfo
                Return FrmInstanceSavesInfo
            Case FormMain.PageSubType.VersionSavesBackup
                If FrmInstanceSavesBackup Is Nothing Then FrmInstanceSavesBackup = New PageInstanceSavesBackup
                Return FrmInstanceSavesBackup
            Case FormMain.PageSubType.VersionSavesDatapacks
                If FrmInstanceSavesDatapacks Is Nothing Then FrmInstanceSavesDatapacks = New PageInstanceSavesDatapacks
                Return FrmInstanceSavesDatapacks
            Case Else
                Throw New Exception("未知的实例设置子页面种类：" & ID)
        End Select
    End Function

    ''' <summary>
    ''' 切换现有页面。
    ''' </summary>
    Public Sub PageChange(ID As FormMain.PageSubType)
        If PageID = ID Then Return
        AniControlEnabled += 1
        Try
            PageChangeRun(PageGet(ID))
            PageID = ID
        Catch ex As Exception
            Log(ex, "切换分页面失败（ID " & ID & "）", LogLevel.Feedback)
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub
    Private Shared Sub PageChangeRun(Target As MyPageRight)
        AniStop("FrmMain PageChangeRight") '停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        If Target.Parent IsNot Nothing Then Target.SetValue(ContentPresenter.ContentProperty, Nothing)
        FrmMain.PageRight = Target
        CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnExit()
        AniStart({
            AaCode(Sub()
                       CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnForceExit()
                       FrmMain.PanMainRight.Child = FrmMain.PageRight
                       FrmMain.PageRight.Opacity = 0
                   End Sub, 130),
            AaCode(Sub()
                       '延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                       FrmMain.PageRight.Opacity = 1
                       FrmMain.PageRight.PageOnEnter()
                   End Sub, 30, True)
        }, "PageLeft PageChange")
    End Sub

    Public Sub Refresh(sender As Object, e As EventArgs) '由边栏按钮匿名调用
        Refresh(Val(sender.Tag))
    End Sub
    Public Sub Refresh() Implements IRefreshable.Refresh
        Refresh(FrmMain.PageCurrentSub)
    End Sub
    Public Sub Refresh(SubType As FormMain.PageSubType)
        Select Case SubType
            Case FormMain.PageSubType.VersionSavesBackup
                If FrmInstanceSavesBackup Is Nothing Then FrmInstanceSavesBackup = New PageInstanceSavesBackup
                If ItemBackup.Checked Then
                    FrmInstanceSavesBackup.Refresh()
                Else
                    ItemBackup.Checked = True
                End If
            Case FormMain.PageSubType.VersionSavesDatapacks
                If FrmInstanceSavesDatapacks Is Nothing Then FrmInstanceSavesDatapacks = New PageInstanceSavesDatapacks
                If ItemDatapacks.Checked Then
                    FrmInstanceSavesDatapacks.Refresh()
                Else
                    ItemDatapacks.Checked = True
                End If
        End Select
        Hint("刷新中……")
    End Sub

#End Region

    Public Shared CurrentSave As String

    '初始化
    Private IsLoad As Boolean = False
    Private Sub Page_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If IsLoad Then Return
        IsLoad = True

        ' 检查实例版本，如果小于1.13或出错则隐藏数据包按钮
        CheckDatapackButtonVisibility()
    End Sub

    ''' <summary>
    ''' 检查实例版本并决定是否显示数据包按钮
    ''' </summary>
    Private Sub CheckDatapackButtonVisibility()
        Try
            ' 获取当前实例
            Dim currentInstance As McInstance = PageInstanceLeft.Instance
            If currentInstance Is Nothing OrElse currentInstance.Version Is Nothing Then
                ' 如果无法获取实例信息，隐藏数据包按钮（出错就隐藏）
                ItemDatapacks.Visibility = Visibility.Collapsed
                Log("无法获取实例版本信息，隐藏数据包按钮", LogLevel.Debug)
                Return
            End If

            ' 解析版本号
            Dim versionParts() As String = currentInstance.Version.McName.Split("."c)
            If versionParts.Length < 2 Then
                ' 版本格式不正确，隐藏数据包按钮（出错就隐藏）
                ItemDatapacks.Visibility = Visibility.Collapsed
                Log($"版本格式不正确: {currentInstance.Version.McName}，隐藏数据包按钮", LogLevel.Debug)
                Return
            End If

            Dim majorVersion As Integer
            Dim minorVersion As Integer

            If Integer.TryParse(versionParts(0), majorVersion) AndAlso Integer.TryParse(versionParts(1), minorVersion) Then
                ' 检查是否小于1.13（1.12.2及以下版本）
                If majorVersion < 1 OrElse (majorVersion = 1 AndAlso minorVersion < 13) Then
                    ' 隐藏数据包按钮（版本太旧）
                    ItemDatapacks.Visibility = Visibility.Collapsed
                    Log($"实例版本 {currentInstance.Version.McName} 小于1.13，隐藏数据包按钮", LogLevel.Debug)

                    ' 如果当前选中的是数据包页面，自动切换到信息页面
                    If PageID = FormMain.PageSubType.VersionSavesDatapacks Then
                        ItemInfo.Checked = True
                        PageChange(FormMain.PageSubType.VersionSavesInfo)
                    End If
                Else
                    ' 显示数据包按钮（版本支持）
                    ItemDatapacks.Visibility = Visibility.Visible
                    Log($"实例版本 {currentInstance.Version.McName} 支持数据包，显示数据包按钮", LogLevel.Debug)
                End If
            Else
                ' 版本解析失败，隐藏数据包按钮（出错就隐藏）
                ItemDatapacks.Visibility = Visibility.Collapsed
                Log($"版本解析失败: {currentInstance.Version.McName}，隐藏数据包按钮", LogLevel.Debug)
            End If

        Catch ex As Exception
            ' 任何异常都隐藏数据包按钮（出错就隐藏）
            ItemDatapacks.Visibility = Visibility.Collapsed
            Log(ex, "检查实例版本时发生错误，隐藏数据包按钮", LogLevel.Debug)
        End Try
    End Sub

    Private Sub BtnOpenFolder_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenFolder.Click
        e.Handled = True
        OpenExplorer($"{CurrentSave}\")
    End Sub
End Class
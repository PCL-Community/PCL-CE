Public Class PageToolsLeft

    Private IsLoad As Boolean = False
    Private IsPageSwitched As Boolean = False '如果在 Loaded 前切换到其他页面，会导致触发 Loaded 时再次切换一次
    Private Sub PageLinkLeft_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If IsLoad Then Return
        IsLoad = True
        '切换默认页面
        If IsPageSwitched Then Exit Sub
        ItemGameLink.SetChecked(True, False, False)
    End Sub
    Public Sub New()
        InitializeComponent()
        AnimatedControl = Me.PanItem
    End Sub

    Private Sub PageOtherLeft_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        IsPageSwitched = False
    End Sub

#Region "页面切换"

    ''' <summary>
    ''' 当前页面的编号。
    ''' </summary>
    Public PageID As FormMain.PageSubType = FormMain.PageSubType.ToolsGameLink

    ''' <summary>
    ''' 勾选事件改变页面。
    ''' </summary>
    Private Sub PageCheck(sender As MyListItem, e As RouteEventArgs) Handles ItemGameLink.Check, ItemLauncherHelp.Check, ItemTest.Check
        '尚未初始化控件属性时，sender.Tag 为 Nothing，会导致切换到页面 0
        '若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        If sender.Tag IsNot Nothing Then PageChange(Val(sender.Tag))
    End Sub

    Public Function PageGet(Optional ID As FormMain.PageSubType = -1)
        If ID = -1 Then ID = PageID
        Select Case ID
            Case 0, FormMain.PageSubType.ToolsGameLink
                If FrmToolsGameLink Is Nothing Then FrmToolsGameLink = New PageToolsGameLink
                Return FrmToolsGameLink
            Case FormMain.PageSubType.SetupGameLink
                If FrmSetupGameLink Is Nothing Then FrmSetupGameLink = New PageSetupGameLink
                Return FrmSetupGameLink
            Case FormMain.PageSubType.ToolsTest
                If FrmToolsTest Is Nothing Then FrmToolsTest = New PageToolsTest
                Return FrmToolsTest
            Case FormMain.PageSubType.ToolsLauncherHelp
                If FrmToolsHelp Is Nothing Then FrmToolsHelp = New PageToolsHelp
                Return FrmToolsHelp
            Case Else
                Throw New Exception("未知的更多子页面种类：" & ID)
        End Select
    End Function

    ''' <summary>
    ''' 切换现有页面。
    ''' </summary>
    Public Sub PageChange(ID As FormMain.PageSubType)
        If PageID = ID Then Return
        AniControlEnabled += 1
        IsPageSwitched = True
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

#End Region

    Public Sub Refresh(sender As Object, e As EventArgs)
        If sender.Tag Is Nothing Then Return
        Dim id = Val(sender.Tag)
        Select Case id
            Case FormMain.PageSubType.ToolsGameLink
                If FrmToolsGameLink Is Nothing Then FrmToolsGameLink = New PageToolsGameLink
                FrmToolsGameLink.Reload()
                ItemGameLink.Checked = True
            Case FormMain.PageSubType.ToolsLauncherHelp
                If FrmToolsHelp Is Nothing Then FrmToolsHelp = New PageToolsHelp
                FrmToolsHelp.Refresh()
                ItemLauncherHelp.Checked = True
        End Select

    End Sub

    Public Shared Sub RefreshHelp()
        FrmToolsHelp.PageLoaderRestart()
        FrmToolsHelp.SearchBox.Text = ""
    End Sub

End Class

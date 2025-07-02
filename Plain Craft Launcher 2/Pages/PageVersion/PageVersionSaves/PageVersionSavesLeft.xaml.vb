Imports System.Threading.Tasks

Public Class PageVersionSavesLeft
    Implements IRefreshable

#Region "龙猫牌 页面管理"

    ''' <summary>
    ''' 当前页面的编号。从 0 开始计算。
    ''' </summary>
    Public PageID As FormMain.PageSubType = FormMain.PageSubType.Default

    ''' <summary>
    ''' 勾选事件改变页面。
    ''' </summary>
    Private Sub PageCheck(sender As MyListItem, e As RouteEventArgs) Handles ItemBackup.Check, ItemInfo.Check
        '尚未初始化控件属性时，sender.Tag 为 Nothing，会导致切换到页面 0
        '若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        If sender.Tag IsNot Nothing Then PageChange(Val(sender.Tag))
    End Sub
    Public Function PageGet(Optional ID As FormMain.PageSubType = -1)
        If ID = -1 Then ID = PageID
        Select Case ID
            Case FormMain.PageSubType.VersionSavesInfo
                If FrmVersionSavesInfo Is Nothing Then FrmVersionSavesInfo = New PageVersionSavesInfo
                Return FrmVersionSavesInfo
            Case FormMain.PageSubType.VersionSavesBackup
                If FrmVersionSavesBackup Is Nothing Then FrmVersionSavesBackup = New PageVersionSavesBackup
                Return FrmVersionSavesBackup
            Case Else
                Throw New Exception("未知的版本设置子页面种类：" & ID)
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
                If FrmVersionSavesBackup Is Nothing Then FrmVersionSavesBackup = New PageVersionSavesBackup
                If ItemBackup.Checked Then
                    FrmVersionSavesBackup.Refresh()
                Else
                    ItemBackup.Checked = True
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

    End Sub

    Private Sub BtnOpenFolder_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenFolder.Click
        e.Handled = True
        OpenExplorer($"{CurrentSave}\")
    End Sub
End Class

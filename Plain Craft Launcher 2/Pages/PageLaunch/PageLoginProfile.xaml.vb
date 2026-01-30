Imports System.Collections.ObjectModel

Class PageLoginProfile
    ''' <summary>
    ''' 刷新页面显示的所有信息。
    ''' </summary>
    Public Sub Reload() Handles Me.Loaded
        RefreshProfileList()
        FrmLoginProfileSkin = Nothing
        'RunInNewThread(Sub()
        '                   Thread.Sleep(800)
        '                   RunInUi(Sub() FrmLaunchLeft.RefreshPage(True))
        '               End Sub)
    End Sub
    Public Property ProfileCollection As New ObservableCollection(Of ProfileItem)
    Public Class ProfileItem
        Public ReadOnly Property Info As String
        Public ReadOnly Property Logo As String
        Public ReadOnly Property Profile As McProfile
        Public ReadOnly Property Username As String
            Get
                Return Profile.Username
            End Get
        End Property
        Public Sub New(profile As McProfile)
            Me.Profile = profile
            Info = GetProfileInfo(profile)
            Dim LogoPath As String = PathTemp & $"Cache\Skin\Head\{Profile.SkinHeadId}.png"
            If Not (File.Exists(LogoPath) AndAlso Not New FileInfo(LogoPath).Length = 0) Then
                LogoPath = ModBase.Logo.IconButtonUser
            End If
            Logo = LogoPath
        End Sub
    End Class
    ''' <summary>
    ''' 刷新档案列表
    ''' </summary>
    Public Sub RefreshProfileList()
        Log("[Profile] 刷新档案列表")
        ProfileCollection.Clear()
        GetProfile()
        Try
            For Each Profile In ProfileList
            ProfileCollection.Add(New ProfileItem(Profile))
            Next
            Log("[Profile] 档案列表刷新完成")
        Catch ex As Exception
            Log(ex, "读取档案列表失败", LogLevel.Feedback)
        End Try
        If Not ProfileList.Any() Then
            Setup.Set("HintProfileSelect", True)
            HintCreate.Visibility = Visibility.Visible
        Else
            HintCreate.Visibility = Visibility.Collapsed
        End If
    End Sub

#Region "控件"
    Private Sub SelectProfile(sender As Object, e As MouseButtonEventArgs)
        SelectedProfile = CType(sender, MyListItem).Tag
        Log($"[Profile] 选定档案: {sender.Tag.Username}, 以 {sender.Tag.Type} 方式验证")
        LastUsedProfile = ProfileList.IndexOf(sender.Tag) '获取当前档案的序号
        SaveProfile() '保存档案配置，确保切换后的档案被正确保存

        '清除登录验证缓存，确保使用新档案的验证信息
        McLoginMsLoader.State = LoadState.Waiting
        McLoginAuthLoader.State = LoadState.Waiting
        McLoginLegacyLoader.State = LoadState.Waiting

        RunInUi(Sub()
                    FrmLaunchLeft.RefreshPage(True)
                    FrmLaunchLeft.BtnLaunch.IsEnabled = True
                End Sub)
    End Sub
    Private Sub ProfileContMenuBuild(sender As MyListItem, e As EventArgs)
        '更改 UUID
        Dim btnEditUuid As New MyIconButton With {.Logo = Logo.IconButtonEdit, .ToolTip = "更改 UUID", .Tag = sender.Tag}
        ToolTipService.SetPlacement(btnEditUuid, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(btnEditUuid, 30)
        ToolTipService.SetHorizontalOffset(btnEditUuid, 2)
        AddHandler btnEditUuid.Click, AddressOf EditProfileUuid
        '复制 UUID
        Dim btnCopyUuid As New MyIconButton With {.Logo = Logo.IconButtonCopy, .ToolTip = "复制 UUID", .Tag = sender.Tag}
        ToolTipService.SetPlacement(btnCopyUuid, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(btnCopyUuid, 30)
        ToolTipService.SetHorizontalOffset(btnCopyUuid, 2)
        AddHandler btnCopyUuid.Click, AddressOf CopyProfileUuid
        '更改验证服务器名称
        Dim btnEditServerName As New MyIconButton With {.Logo = Logo.IconButtonInfo, .ToolTip = "更改验证服务器名称", .Tag = sender.Tag}
        ToolTipService.SetPlacement(btnEditServerName, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(btnEditServerName, 30)
        ToolTipService.SetHorizontalOffset(btnEditServerName, 2)
        AddHandler btnEditServerName.Click, AddressOf EditProfileServer
        '删除档案
        Dim btnDelete As New MyIconButton With {.Logo = Logo.IconButtonDelete, .ToolTip = "删除档案", .Tag = sender.Tag}
        ToolTipService.SetPlacement(btnDelete, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(btnDelete, 30)
        ToolTipService.SetHorizontalOffset(btnDelete, 2)
        AddHandler btnDelete.Click, AddressOf DeleteProfile
        '根据档案类型显示不同的菜单项
        If sender.Tag.Type = McLoginType.Legacy Then
            sender.Buttons = {btnEditUuid, btnDelete}
        Else
            sender.Buttons = {btnCopyUuid, btnDelete}
        End If
    End Sub
    '创建档案
    Private Sub BtnNew_Click(sender As Object, e As EventArgs) Handles BtnNew.Click
        RunInNewThread(Sub()
                           CreateProfile()
                           RunInUi(Sub() RefreshProfileList())
                       End Sub)
    End Sub
    '编辑 UUID
    Private Sub EditProfileUuid(sender As Object, e As EventArgs)
        EditOfflineUuid(sender.Tag)
    End Sub
    Private Sub CopyProfileUuid(sender As Object, e As EventArgs)
        ClipboardSet(sender.Tag.UUID)
    End Sub
    '编辑验证服务器名称
    Private Sub EditProfileServer(sender As Object, e As EventArgs)
        Dim name As String = MyMsgBoxInput("修改验证服务器名称", $"请输入新的验证服务器名称", sender.Tag.ServerName)
        If name IsNot Nothing Then
            EditAuthServerName(sender.Tag, name)
        End If
    End Sub
    '删除档案
    Private Sub DeleteProfile(sender As Object, e As EventArgs)
        If MyMsgBox($"你正在选择删除此档案，该操作无法撤销。{vbCrLf}确定继续？", "删除档案确认", "继续", "取消", IsWarn:=True, ForceWait:=True) = 2 Then Exit Sub
        RemoveProfile(sender.Tag)
        RunInUi(Sub() RefreshProfileList())
    End Sub
    '导入 / 导出档案
    Private Sub BtnPort_Click() Handles BtnPort.Click
        MigrateProfile()
        RunInUi(Sub() RefreshProfileList())
    End Sub
#End Region

End Class

Imports System.Windows.Threading
Public Class PageSelectRight

    Private LastInputTime As DateTime = DateTime.MinValue
    Private ReloadTimer As DispatcherTimer
    Private Const NormalDelay As Integer = 75  '正常输入延迟0.075秒
    Private Const QuickDelay As Integer = 50   '清空搜索框延迟0.05秒
    Private IsRefreshing As Boolean = False

    '窗口基础
    Private Sub PageSelectRight_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.RunOnUpdated, MaxDepth:=1, ExtraPath:="versions\")
        PanBack.ScrollToHome()
        AddHandler PanVerSearchBox.TextChanged, AddressOf PanVerSearchBox_TextChanged

        ReloadTimer = New DispatcherTimer With {
            .Interval = TimeSpan.FromMilliseconds(NormalDelay)
        }
        AddHandler ReloadTimer.Tick, AddressOf ReloadTimer_Tick
    End Sub

    Private Sub PanVerSearchBox_TextChanged(sender As Object, e As TextChangedEventArgs)
        '记录最后一次输入时间
        LastInputTime = DateTime.Now

        IsRefreshing = False

        '动态调整延迟时间
        If String.IsNullOrWhiteSpace(PanVerSearchBox.Text) Then
            If ReloadTimer.Interval.TotalMilliseconds <> QuickDelay Then
                ReloadTimer.Interval = TimeSpan.FromMilliseconds(QuickDelay)
            End If
        Else
            If ReloadTimer.Interval.TotalMilliseconds <> NormalDelay Then
                ReloadTimer.Interval = TimeSpan.FromMilliseconds(NormalDelay)
            End If
        End If


        If Not ReloadTimer.IsEnabled Then
            ReloadTimer.Start()
        End If
    End Sub

    Private Sub ReloadTimer_Tick(sender As Object, e As EventArgs)
        '检查是否超过当前设定的延迟时间没有新输入
        Dim elapsed = (DateTime.Now - LastInputTime).TotalMilliseconds
        Dim currentDelay = ReloadTimer.Interval.TotalMilliseconds

        If elapsed >= currentDelay AndAlso McInstanceListLoader.State = LoadState.Finished AndAlso Not IsRefreshing Then
            IsRefreshing = True

            '确保在UI线程执行刷新
            Dispatcher.BeginInvoke(Sub()
                                       McInstanceListUI(McInstanceListLoader)
                                       IsRefreshing = False
                                   End Sub)
            ReloadTimer.Stop()
        End If
    End Sub

    Private Sub PageSelectRight_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        '清理计时器
        If ReloadTimer IsNot Nothing Then
            ReloadTimer.Stop()
            RemoveHandler ReloadTimer.Tick, AddressOf ReloadTimer_Tick
            ReloadTimer = Nothing
        End If
    End Sub

    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanAllBack, Nothing, McInstanceListLoader, AddressOf McInstanceListUI, AutoRun:=False)
    End Sub

    Private Sub Load_Click(sender As Object, e As MouseButtonEventArgs) Handles Load.Click
        If McInstanceListLoader.State = LoadState.Failed Then
            LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        End If
    End Sub

    '窗口属性
    ''' <summary>
    ''' 是否显示隐藏的 Minecraft 实例。
    ''' </summary>
    Public ShowHidden As Boolean = False

#Region "结果 UI 化"

    Private Sub McInstanceListUI(Loader As LoaderTask(Of String, Integer))
        Try
            Dim Path As String = Loader.Input
            '加载 UI
            PanMain.Children.Clear()

            Dim hasVisibleFolders As Boolean = False
            Dim searchText As String = PanVerSearchBox.Text.Trim().ToLower() ' 获取搜索框文本
            Dim hasAnyResults As Boolean = False
            Dim originalHasInstances As Boolean = McInstanceList.ToArray.Any(Function(c) c.Value.Count > 0)

            ' 搜索无结果时显示 PanEmptySearch
            PanEmptySearch.Visibility = Visibility.Collapsed ' 默认隐藏

            For Each Card As KeyValuePair(Of McInstanceCardType, List(Of McInstance)) In McInstanceList.ToArray
                If Card.Key = McInstanceCardType.Hidden Xor ShowHidden Then Continue For
                Dim filteredInstances = Card.Value.Where(Function(v)
                                                             If String.IsNullOrEmpty(searchText) Then Return True
                                                             Return v.Name.ToLower().Contains(searchText) OrElse
                           (v.Info IsNot Nothing AndAlso v.Info.ToLower().Contains(searchText))
                                                         End Function).ToList()
                If filteredInstances.Count = 0 Then Continue For

                hasVisibleFolders = True
                hasAnyResults = True
                If filteredInstances.Count = 0 Then Continue For
                hasVisibleFolders = True
#Region "确认卡片名称"
                Dim CardName As String = ""
                Select Case Card.Key
                    Case McInstanceCardType.OriginalLike
                        CardName = "常规实例"
                    Case McInstanceCardType.API
                        Dim IsForgeExists As Boolean = False
                        Dim IsNeoForgeExists As Boolean = False
                        Dim IsFabricExists As Boolean = False
                        Dim IsQuiltExists As Boolean = False
                        Dim IsLiteExists As Boolean = False
                        Dim IsCleanroomExists As Boolean = False
                        Dim IsLabyModExists As Boolean = False
                        For Each instance As McInstance In Card.Value
                            If instance.Version.HasFabric Then IsFabricExists = True
                            If instance.Version.HasQuilt Then IsQuiltExists = True
                            If instance.Version.HasLiteLoader Then IsLiteExists = True
                            If instance.Version.HasForge Then IsForgeExists = True
                            If instance.Version.HasNeoForge Then IsNeoForgeExists = True
                            If instance.Version.HasCleanroom Then IsCleanroomExists = True
                            If instance.Version.HasLabyMod Then IsLabyModExists = True
                        Next
                        If If(IsLiteExists, 1, 0) + If(IsForgeExists, 1, 0) + If(IsFabricExists, 1, 0) + If(IsNeoForgeExists, 1, 0) + If(IsQuiltExists, 1, 0) + If(IsCleanroomExists, 1, 0) + If(IsLabyModExists, 1, 0) > 1 Then
                            CardName = "可安装 Mod"
                        ElseIf IsForgeExists Then
                            CardName = "Forge 实例"
                        ElseIf IsNeoForgeExists Then
                            CardName = "NeoForge 实例"
                        ElseIf IsCleanroomExists Then
                            CardName = "Cleanroom 实例"
                        ElseIf IsLabyModExists Then
                            CardName = "LabyMod 实例"
                        ElseIf IsLiteExists Then
                            CardName = "LiteLoader 实例"
                        ElseIf IsQuiltExists Then
                            CardName = "Quilt 实例"
                        Else
                            CardName = "Fabric 实例"
                        End If
                    Case McInstanceCardType.Error
                        CardName = "错误的实例"
                    Case McInstanceCardType.Hidden
                        CardName = "隐藏的实例"
                    Case McInstanceCardType.Rubbish
                        CardName = "不常用实例"
                    Case McInstanceCardType.Star
                        CardName = "收藏夹"
                    Case McInstanceCardType.Fool
                        CardName = "愚人节版本"
                    Case Else
                        Throw New ArgumentException("未知的卡片种类（" & Card.Key & "）")
                End Select
#End Region
                '建立控件
                Dim CardTitle As String = CardName & If(CardName = "收藏夹", "", " (" & filteredInstances.Count & ")")
                Dim NewCard As New MyCard With {.Title = CardTitle, .Margin = New Thickness(0, 0, 0, 15)}
                Dim NewStack As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0), .Tag = filteredInstances}
                NewCard.Children.Add(NewStack)
                NewCard.SwapControl = NewStack
                PanMain.Children.Add(NewCard)
                '确定卡片是否展开
                Dim PutMethod = Sub(Stack As StackPanel)
                                    For Each item In Stack.Tag
                                        Stack.Children.Add(McVersionListItem(item))
                                    Next
                                End Sub
                If Card.Key = McInstanceCardType.Rubbish OrElse Card.Key = McInstanceCardType.Error OrElse Card.Key = McInstanceCardType.Fool Then
                    NewCard.IsSwaped = True
                    NewCard.InstallMethod = PutMethod
                Else
                    MyCard.StackInstall(NewStack, PutMethod)
                End If
            Next

            '若只有一个卡片，则强制展开
            If PanMain.Children.Count = 1 AndAlso CType(PanMain.Children(0), MyCard).IsSwaped Then
                CType(PanMain.Children(0), MyCard).IsSwaped = False
            End If

            PanVerSearchBox.Visibility = If(hasVisibleFolders, Visibility.Visible, Visibility.Collapsed)

            '判断应该显示哪一个页面
            If Not hasAnyResults Then
                If Not originalHasInstances Then
                    '完全没有实例的情况
                    PanEmpty.Visibility = Visibility.Visible
                    PanBack.Visibility = Visibility.Collapsed
                    If ShowHidden Then
                        LabEmptyTitle.Text = "无隐藏实例"
                        LabEmptyContent.Text = "没有实例被隐藏，你可以在实例设置的实例分类选项中隐藏实例。" & vbCrLf & "再次按下 F11 即可退出隐藏实例查看模式。"
                        BtnEmptyDownload.Visibility = Visibility.Collapsed
                    Else
                        LabEmptyTitle.Text = "无可用实例"
                        LabEmptyContent.Text = "未找到任何游戏实例，请先下载一个游戏实例。" & vbCrLf & "若有已存在的实例，请在左边的列表中选择添加文件夹，选择 .minecraft 文件夹将其导入。"
                        BtnEmptyDownload.Visibility = If(Setup.Get("UiHiddenPageDownload") AndAlso Not PageSetupUI.HiddenForceShow, Visibility.Collapsed, Visibility.Visible)
                    End If
                Else
                    '有实例但搜索无结果的情况
                    If ShowHidden AndAlso McInstanceList.ToArray.Any(Function(c) c.Key = McInstanceCardType.Hidden AndAlso c.Value.Count > 0) Then
                        '有隐藏实例但搜索无结果 - 显示搜索无结果提示
                        PanVerSearchBox.Visibility = Visibility.Visible
                        PanEmpty.Visibility = Visibility.Collapsed
                        PanBack.Visibility = Visibility.Visible
                        PanEmptySearch.Visibility = Visibility.Visible
                        LabEmptySearchTitle.Text = "无匹配的隐藏实例"
                        LabEmptySearchContent.Text = If(String.IsNullOrWhiteSpace(searchText),
                        "请输入搜索内容",
                        $"没有找到与 '{searchText}' 匹配的隐藏实例")
                    ElseIf ShowHidden Then
                        '无隐藏实例 - 显示"无隐藏实例"提示
                        PanEmpty.Visibility = Visibility.Visible
                        PanBack.Visibility = Visibility.Collapsed
                        LabEmptyTitle.Text = "无隐藏实例"
                        LabEmptyContent.Text = "没有实例被隐藏，你可以在实例设置的实例分类选项中隐藏实例。" & vbCrLf & "再次按下 F11 即可退出隐藏实例查看模式。"
                        BtnEmptyDownload.Visibility = Visibility.Collapsed
                        PanVerSearchBox.Visibility = Visibility.Collapsed
                    Else
                        ' 普通模式下的搜索无结果
                        PanVerSearchBox.Visibility = Visibility.Visible
                        PanEmpty.Visibility = Visibility.Collapsed
                        PanBack.Visibility = Visibility.Visible
                        PanEmptySearch.Visibility = Visibility.Visible
                        LabEmptySearchTitle.Text = "无匹配的游戏实例"
                        LabEmptySearchContent.Text = If(String.IsNullOrWhiteSpace(searchText),
                        "请输入搜索内容",
                        $"没有找到与 '{searchText}' 匹配的实例")
                    End If
                End If
            Else
                PanBack.Visibility = Visibility.Visible
                PanEmpty.Visibility = Visibility.Collapsed
                PanEmptySearch.Visibility = Visibility.Collapsed ' 有结果时隐藏
            End If



        Catch ex As Exception
            Log(ex, "将实例列表转换显示时失败", LogLevel.Feedback)
        End Try
    End Sub
    Public Shared Function McVersionListItem(Version As McInstance) As MyListItem
        Dim NewItem As New MyListItem With {.Title = Version.Name, .Info = Version.Info, .Height = 42, .Tag = Version, .SnapsToDevicePixels = True, .Type = MyListItem.CheckType.Clickable}
        Try
            If Version.Logo.EndsWith("PCL\Logo.png") Then
                NewItem.Logo = Version.Path & "PCL\Logo.png" '修复老版本中，存储的自定义 Logo 使用完整路径，导致移动后无法加载的 Bug
            Else
                NewItem.Logo = Version.Logo
            End If
        Catch ex As Exception
            Log(ex, "加载实例图标失败", LogLevel.Hint)
            NewItem.Logo = "pack://application:,,,/images/Blocks/RedstoneBlock.png"
        End Try
        NewItem.ContentHandler = AddressOf McVersionListContent
        Return NewItem
    End Function
    Private Shared Sub McVersionListContent(sender As MyListItem, e As EventArgs)
        Dim Version As McInstance = sender.Tag
        '注册点击事件
        AddHandler sender.Click, AddressOf Item_Click
        '图标按钮
        Dim BtnStar As New MyIconButton
        If Version.IsStar Then
            BtnStar.ToolTip = "取消收藏"
            ToolTipService.SetPlacement(BtnStar, Primitives.PlacementMode.Center)
            ToolTipService.SetVerticalOffset(BtnStar, 30)
            ToolTipService.SetHorizontalOffset(BtnStar, 2)
            BtnStar.LogoScale = 1.1
            BtnStar.Logo = Logo.IconButtonLikeFill
        Else
            BtnStar.ToolTip = "收藏"
            ToolTipService.SetPlacement(BtnStar, Primitives.PlacementMode.Center)
            ToolTipService.SetVerticalOffset(BtnStar, 30)
            ToolTipService.SetHorizontalOffset(BtnStar, 2)
            BtnStar.LogoScale = 1.1
            BtnStar.Logo = Logo.IconButtonLikeLine
        End If
        AddHandler BtnStar.Click, Sub()
                                      WriteIni(Version.Path & "PCL\Setup.ini", "IsStar", Not Version.IsStar)
                                      McInstanceListForceRefresh = True
                                      LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
                                  End Sub
        Dim BtnDel As New MyIconButton With {.LogoScale = 1.1, .Logo = Logo.IconButtonDelete}
        BtnDel.ToolTip = "删除"
        ToolTipService.SetPlacement(BtnDel, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnDel, 30)
        ToolTipService.SetHorizontalOffset(BtnDel, 2)
        AddHandler BtnDel.Click, Sub() DeleteVersion(sender, Version)
        If Version.State <> McInstanceState.Error Then
            Dim BtnCont As New MyIconButton With {.LogoScale = 1.1, .Logo = Logo.IconButtonSetup}
            BtnCont.ToolTip = "设置"
            ToolTipService.SetPlacement(BtnCont, Primitives.PlacementMode.Center)
            ToolTipService.SetVerticalOffset(BtnCont, 30)
            ToolTipService.SetHorizontalOffset(BtnCont, 2)
            AddHandler BtnCont.Click,
            Sub()
                PageInstanceLeft.Instance = Version
                FrmMain.PageChange(FormMain.PageType.InstanceSetup, 0)
            End Sub
            AddHandler sender.MouseRightButtonUp,
            Sub()
                PageInstanceLeft.Instance = Version
                FrmMain.PageChange(FormMain.PageType.InstanceSetup, 0)
            End Sub
            sender.Buttons = {BtnStar, BtnDel, BtnCont}
        Else
            Dim BtnCont As New MyIconButton With {.LogoScale = 1.15, .Logo = Logo.IconButtonOpen}
            BtnCont.ToolTip = "打开文件夹"
            ToolTipService.SetPlacement(BtnCont, Primitives.PlacementMode.Center)
            ToolTipService.SetVerticalOffset(BtnCont, 30)
            ToolTipService.SetHorizontalOffset(BtnCont, 2)
            AddHandler BtnCont.Click, Sub() PageInstanceOverall.OpenVersionFolder(Version)
            AddHandler sender.MouseRightButtonUp, Sub() PageInstanceOverall.OpenVersionFolder(Version)
            sender.Buttons = {BtnStar, BtnDel, BtnCont}
        End If
    End Sub

#End Region

#Region "页面事件"

    '点击选项
    Public Shared Sub Item_Click(sender As MyListItem, e As EventArgs)
        Dim instance As McInstance = sender.Tag
        If New McInstance(instance.Path).Check Then
            '正常实例
            McInstanceCurrent = instance
            Setup.Set("LaunchInstanceSelect", McInstanceCurrent.Name)
            FrmMain.PageBack()
        Else
            '错误实例
            PageInstanceOverall.OpenVersionFolder(instance)
        End If
    End Sub

    Private Sub BtnDownload_Click(sender As Object, e As EventArgs) Handles BtnEmptyDownload.Click
        FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall)
    End Sub

    '修改此代码时，同时修改 PageInstanceOverall 中的代码
    Public Shared Sub DeleteVersion(Item As MyListItem, Version As McInstance)
        Try
            Dim IsShiftPressed As Boolean = My.Computer.Keyboard.ShiftKeyDown
            Dim IsHintIndie As Boolean = Version.State <> McInstanceState.Error AndAlso Version.PathIndie <> PathMcFolder
            Select Case MyMsgBox($"你确定要{If(IsShiftPressed, "永久", "")}删除实例 {Version.Name} 吗？" &
                        If(IsHintIndie, vbCrLf & "由于该实例开启了版本隔离，删除时该实例对应的存档、资源包、Mod 等文件也将被一并删除！", ""),
                        "实例删除确认", , "取消",, True)
                Case 1
                    IniClearCache(Version.PathIndie & "options.txt")
                    IniClearCache(Version.Path & "PCL\Setup.ini")
                    If IsShiftPressed Then
                        DeleteDirectory(Version.Path)
                        Hint("实例 " & Version.Name & " 已永久删除！", HintType.Finish)
                    Else
                        FileIO.FileSystem.DeleteDirectory(Version.Path, FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.SendToRecycleBin)
                        Hint("实例 " & Version.Name & " 已删除到回收站！", HintType.Finish)
                    End If
                Case 2
                    Return
            End Select
            '从 UI 中移除
            If Version.DisplayType = McInstanceCardType.Hidden OrElse Not Version.IsStar Then
                '仅出现在当前卡片
                Dim Parent As StackPanel = Item.Parent
                If Parent.Children.Count > 2 Then '当前的项目与一个占位符
                    '删除后还有剩
                    Dim Card As MyCard = Parent.Parent
                    Card.Title = Card.Title.Replace(Parent.Children.Count - 1, Parent.Children.Count - 2) '有一个占位符
                    Parent.Children.Remove(Item)
                    If McInstanceCurrent IsNot Nothing AndAlso Version.Path = McInstanceCurrent.Path Then
                        '删除当前实例就更改选择
                        McInstanceCurrent = CType(Parent.Children(0), MyListItem).Tag
                    End If
                    LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.UpdateOnly, MaxDepth:=1, ExtraPath:="versions\")
                Else
                    '删除后没剩了
                    LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
                End If
            Else
                '同时出现在当前卡片与收藏夹
                LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            End If
        Catch ex As OperationCanceledException
            Log(ex, "删除实例 " & Version.Name & " 被主动取消")
        Catch ex As Exception
            Log(ex, "删除实例 " & Version.Name & " 失败", LogLevel.Msgbox)
        End Try
    End Sub

    Public Sub BtnEmptyDownload_Loaded() Handles BtnEmptyDownload.Loaded
        Dim NewVisibility = If((Setup.Get("UiHiddenPageDownload") AndAlso Not PageSetupUI.HiddenForceShow) OrElse ShowHidden, Visibility.Collapsed, Visibility.Visible)
        If BtnEmptyDownload.Visibility <> NewVisibility Then
            BtnEmptyDownload.Visibility = NewVisibility
            PanLoad.TriggerForceResize()
        End If
    End Sub

#End Region

End Class
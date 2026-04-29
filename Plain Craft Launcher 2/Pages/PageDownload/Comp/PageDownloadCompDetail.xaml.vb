
Imports PCL.Core.UI

Public Class PageDownloadCompDetail
    Private _compItem As MyCompItem = Nothing

#Region "加载器"

    Private _compFileLoader As New LoaderTask(Of Integer, List(Of CompFile))(
        "Comp File",
        Sub(task)
            LoadTargetFromAdditional()
            Dim result = CompFilesGet(_project.Id, _project.FromCurseForge)
            If task.IsAborted Then Return
            task.Output = result
        End Sub)

    '初始化加载器信息
    Private Sub PageDownloadCompDetail_Inited(sender As Object, e As EventArgs) Handles Me.Initialized
        LoadTargetFromAdditional()
        PageLoaderInit(Load, PanLoad, PanMain, CardIntro, _compFileLoader, AddressOf Load_OnFinish)
    End Sub
    Public Sub LoadTargetFromAdditional() Handles Me.Loaded
        _project = FrmMain.PageCurrent.Additional(0)
        _targetInstance = FrmMain.PageCurrent.Additional(2)
        _targetLoader = FrmMain.PageCurrent.Additional(3)
        _pageType = FrmMain.PageCurrent.Additional(4)
    End Sub
    Private _project As CompProject
    Private _targetInstance As String, _targetLoader As CompLoaderType
    ''' <summary>
    ''' 当前页面应展示的内容类别。可能为 Any。
    ''' </summary>
    Private _pageType As CompType
    '自动重试
    Private Sub Load_State(sender As Object, state As MyLoading.MyLoadingState, oldState As MyLoading.MyLoadingState) Handles Load.StateChanged
        Select Case _compFileLoader.State
            Case LoadState.Failed
                Dim errorMessage As String = ""
                If _compFileLoader.Error IsNot Nothing Then errorMessage = _compFileLoader.Error.Message
                If errorMessage.Contains("不是有效的 Json 文件") Then
                    Log("[Comp] 下载的文件 Json 列表损坏，已自动重试", LogLevel.Debug)
                    PageLoaderRestart()
                End If
        End Select
    End Sub
    '结果 UI 化
    Private Class CardSorter
        Implements IComparer(Of String)
        Public Topmost As String = ""
        Public Function Compare(x As String, y As String) As Integer Implements IComparer(Of String).Compare
            '相同
            If x = y Then Return 0
            '置顶
            If x = Topmost Then Return -1
            If y = Topmost Then Return 1
            '特殊版本
            Dim isXSpecial As Boolean = Not x.Contains(".")
            Dim isYSpecial As Boolean = Not y.Contains(".")
            If isXSpecial AndAlso isYSpecial Then Return String.Compare(x, y, StringComparison.Ordinal)
            If isXSpecial Then Return 1
            If isYSpecial Then Return -1
            '比较版本号
            Dim versionCodeSort = -CompareVersion(x.Replace(x.BeforeFirst(" ") & " ", ""), y.Replace(y.BeforeFirst(" ") & " ", ""))
            If versionCodeSort <> 0 Then Return versionCodeSort
            '比较全部
            Return -CompareVersion(x, y)
        End Function
        Public Sub New(Optional topmost As String = "")
            Me.Topmost = If(topmost, "")
        End Sub
    End Class

    Private _instanceFilter As String
    Private _modLoaderFilter As String
    Private GroupedDrop As Boolean '是否按 Drop 筛选（1.21 / 1.20 / 1.19 / ...）而非小版本号（1.21.1 / 1.21 / 1.20.4 / ...）
    Private GroupedOld As Boolean '是否折叠远古版本为一个选项
    '筛选类型相同的结果（Modrinth 会返回 Mod、服务端插件、数据包混合的列表）
    Private Function GetResults() As List(Of CompFile)
        Dim results As List(Of CompFile) = _compFileLoader.Output
        If _pageType = CompType.Any Then
            results = results.Where(Function(r) r.Type <> CompType.Plugin).ToList
        ElseIf _pageType = CompType.Shader OrElse _pageType = CompType.ResourcePack Then
            '不筛选光影和资源包，否则原版光影会因为是资源包格式而被过滤（Meloong-Git/#6473）
        Else
            results = results.Where(Function(r) r.Type = _pageType).ToList
        End If
        Return results
    End Function
    Private Sub Load_OnFinish()
        Dim results = GetResults()

        ' 初始化筛选器
        Dim instanceFilters As List(Of String) = Nothing
        Dim modLoaderFilters As List(Of String) = Nothing
        Dim updateFilters =
        Sub()
            instanceFilters = results.SelectMany(Function(v) v.GameVersions).Select(Function(v) GetGroupedVersionName(v, GroupedDrop, GroupedOld)).
                Distinct.OrderByDescending(Function(s) s, New VersionComparer).ToList
            modLoaderFilters = results.SelectMany(Function(v) v.ModLoaders).Select(Function(l) l.ToString()).
                Distinct.OrderByDescending(Function(s) s).ToList
        End Sub

        ' 确定分组方式
        GroupedDrop = False : GroupedOld = False
        updateFilters()
        If instanceFilters.Count < 9 Then GoTo GroupDone
        GroupedDrop = True : GroupedOld = False
        updateFilters()
        If instanceFilters.Count < 9 Then GoTo GroupDone
        GroupedDrop = False : GroupedOld = True
        updateFilters()
        If instanceFilters.Count < 9 Then GoTo GroupDone
        GroupedDrop = True : GroupedOld = True
        updateFilters()
GroupDone:

        ' UI 化筛选器
        PanInstanceFilter.Children.Clear()
        PanModLoaderFilter.Children.Clear()
        If _pageType = CompType.Mod Then
            PanInstanceFilter.Margin = New Thickness(10, 10, 0, 5)
            PanModLoaderFilter.Margin = New Thickness(10, 5, 0, 10)
        Else
            PanInstanceFilter.Margin = New Thickness(10, 10, 0, 10)
            PanModLoaderFilter.Margin = New Thickness(0)
        End If
        If instanceFilters.Count < 2 Then
            CardFilter.Visibility = Visibility.Collapsed
            _instanceFilter = Nothing
        Else
            CardFilter.Visibility = Visibility.Visible
            ' 插入标签
            If _pageType = CompType.Mod Then
                Dim instanceTextBlock As New TextBlock With {
                        .Text = "实例筛选：", .VerticalAlignment = VerticalAlignment.Center,
                        .Margin = New Thickness(2, 0, 0, 0)}
                PanInstanceFilter.Children.Add(instanceTextBlock)
                Dim modLoaderTextBlock As New TextBlock With {
                        .Text = "模组加载器筛选：", .VerticalAlignment = VerticalAlignment.Center,
                        .Margin = New Thickness(2, 0, 0, 0)}
                PanModLoaderFilter.Children.Add(modLoaderTextBlock)
            End If
            
            instanceFilters.Insert(0, "全部")
            modLoaderFilters.Insert(0, "全部")
            ' 转化为按钮
            For Each version As String In instanceFilters
                Dim newButton As New MyRadioButton With {
                    .Text = version, .Margin = New Thickness(2, 0, 2, 0), .ColorType = MyRadioButton.ColorState.Highlight}
                newButton.LabText.Margin = New Thickness(-2, 0, 10, 0)
                AddHandler newButton.Check,
                Sub(sender As MyRadioButton, raiseByMouse As Boolean)
                    _instanceFilter = If(sender.Text = "全部", Nothing, sender.Text)
                    UpdateFilterResult()
                End Sub
                PanInstanceFilter.Children.Add(newButton)
            Next
            If _pageType = CompType.Mod Then
                For Each loader As String In modLoaderFilters
                    Dim newButton As New MyRadioButton With {
                            .Text = loader, .Margin = New Thickness(2, 0, 2, 0),
                            .ColorType = MyRadioButton.ColorState.Highlight}
                    newButton.LabText.Margin = New Thickness(- 2, 0, 10, 0)
                    AddHandler newButton.Check,
                        Sub(sender As MyRadioButton, raiseByMouse As Boolean)
                            _modLoaderFilter = If(sender.Text = "全部", Nothing, sender.Text)
                            UpdateFilterResult()
                        End Sub
                    PanModLoaderFilter.Children.Add(newButton)
                Next
            End If
            ' 自动选择
            Dim instanceToCheck As MyRadioButton = Nothing
            Dim modLoaderToCheck As MyRadioButton = Nothing
            If _targetInstance <> "" Then
                Dim targetFile = results.FirstOrDefault(Function(v) v.GameVersions.Contains(_targetInstance))
                If targetFile IsNot Nothing Then
                    Dim targetGroup = GetGroupedVersionName(_targetInstance, GroupedDrop, GroupedOld)
                    Dim children =
                            If _
                            (_pageType = CompType.Mod, PanInstanceFilter.Children.Cast (Of UIElement)().Skip(1),
                             PanInstanceFilter.Children)
                    For Each button As MyRadioButton In children
                        If button.Text <> targetGroup Then Continue For
                        instanceToCheck = button
                        Exit For
                    Next
                End If
            End If
            If _pageType = CompType.Mod Then
                If _targetLoader <> CompLoaderType.Any Then
                    Dim targetFile = results.FirstOrDefault(Function(v) v.ModLoaders.Contains(_targetLoader))
                    If targetFile IsNot Nothing Then
                        Dim children =
                                If _
                                (_pageType = CompType.Mod, PanInstanceFilter.Children.Cast (Of UIElement)().Skip(1),
                                 PanInstanceFilter.Children)
                        For Each button As MyRadioButton In children
                            If button.Text <> _targetLoader.ToString() Then Continue For
                            modLoaderToCheck = button
                            Exit For
                        Next
                    End If
                End If
            End If
            
            ' 注意：在 Mod 下 index 0 是 TextBlock
            Dim index As Integer = If(_pageType = CompType.Mod, 1, 0) 
            If instanceToCheck Is Nothing Then instanceToCheck = PanInstanceFilter.Children(index)
            If modLoaderToCheck Is Nothing And _pageType = CompType.Mod Then modLoaderToCheck = PanModLoaderFilter.Children(index)
            instanceToCheck.Checked = True
            If _pageType = CompType.Mod Then modLoaderToCheck.Checked = True
        End If

        '更新筛选结果（文件列表 UI 化）
        UpdateFilterResult()
    End Sub
    Private Sub UpdateFilterResult()
        Dim results = GetResults()
        If results Is Nothing Then Exit Sub

        ' 1. 预处理基础变量
        Dim targetVersionText As String = If(_targetLoader <> CompLoaderType.Any, _targetLoader.ToString() & " ", "")
        Dim targetCardName As String = If(_targetInstance <> "" OrElse _targetLoader <> CompLoaderType.Any,
                                      $"所选版本：{targetVersionText}{_targetInstance}", "")

        ' 使用 HashSet 提高查询性能 O(1)
        Dim supportedLoaders As New HashSet(Of CompLoaderType)([Enum].GetValues(GetType(CompLoaderType)).Cast(Of CompLoaderType))
        Dim ignoreQuilt As Boolean = Setup.Get("ToolDownloadIgnoreQuilt")
        Dim hasMultipleLoaders As Boolean = _project.ModLoaders.Count > 1

        ' 2. 核心数据归类 (使用 Dictionary 配合 HashSet 去重)
        Dim dict As New SortedDictionary(Of String, List(Of CompFile))(New CardSorter(targetCardName))
        dict.Add("其他", New List(Of CompFile))

        ' 用于记录每个卡片内已存在的 version，防止 Contains(version) 的 O(n) 消耗
        Dim versionDuplicateChecker As New Dictionary(Of String, HashSet(Of CompFile))

        For Each version As CompFile In results
            ' 处理普通卡片归类
            For Each gameVersion In version.GameVersions
                ' 筛选器预检查
                Dim currentGroupedName As String = GetGroupedVersionName(gameVersion, GroupedDrop, GroupedOld)
                If _instanceFilter IsNot Nothing AndAlso currentGroupedName <> _instanceFilter Then Continue For
                Dim verName As String = GetGroupedVersionName(gameVersion, False, False)
                Dim loaders As New List(Of String)

                ' 判定 Loader 逻辑
                If hasMultipleLoaders AndAlso version.Type = CompType.Mod AndAlso McInstanceInfo.IsFormatFit(verName) Then
                    For Each loader In version.ModLoaders
                        If loader = CompLoaderType.Quilt AndAlso ignoreQuilt Then Continue For
                        If Not supportedLoaders.Contains(loader) Then Continue For
                        
                        ' 模组加载器筛选器
                        If _modLoaderFilter IsNot Nothing AndAlso loader.ToString() <> _modLoaderFilter Then Continue For
                        
                        loaders.Add(loader.ToString() & " ")
                    Next
                    
                    If loaders.Count = 0 AndAlso _modLoaderFilter IsNot Nothing Then
                        Continue For
                    End If
                End If

                If loaders.Count = 0 Then loaders.Add("")

                ' 填充数据
                For Each loaderPrefix In loaders
                    Dim targetKey As String = loaderPrefix & verName
                    AddVersionToDict(dict, versionDuplicateChecker, targetKey, version)
                Next
            Next

            ' 处理“所选版本”卡片 (逻辑合并，减少二次循环)
            If targetCardName <> "" Then
                Dim isMatchFilter As Boolean = (_instanceFilter Is Nothing OrElse
                                           GetGroupedVersionName(_targetInstance, GroupedDrop, GroupedOld).StartsWithF(_instanceFilter))

                If isMatchFilter AndAlso version.GameVersions.Contains(_targetInstance) Then
                    If _targetLoader = CompLoaderType.Any OrElse version.ModLoaders.Contains(_targetLoader) Then
                        ' 再次检查 version 是否符合筛选器（针对该文件的所有游戏版本）
                        If _instanceFilter Is Nothing OrElse version.GameVersions.Any(Function(v) GetGroupedVersionName(v, GroupedDrop, GroupedOld) = _instanceFilter) Then
                            AddVersionToDict(dict, versionDuplicateChecker, targetCardName, version)
                        End If
                    End If
                End If
            End If
        Next

        ' 3. 渲染 UI
        Try
            PanResults.Children.Clear()
            Dim additionalTitles As List(Of String) = If(FrmMain.PageCurrent.Additional IsNot Nothing,
                                                   CType(FrmMain.PageCurrent.Additional(1), List(Of String)),
                                                   New List(Of String))

            For Each pair In dict
                If pair.Value.Count = 0 Then Continue For

                ' 创建卡片组件
                Dim newCard As New MyCard With {
                    .Title = pair.Key,
                    .Margin = New Thickness(0, 0, 0, 15)
                }

                ' 闭包引用：避免在 Sub 内做高耗时操作
                Dim files = pair.Value
                Dim currentKey = pair.Key

                Dim newStack As New StackPanel With {
                    .Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0),
                    .VerticalAlignment = VerticalAlignment.Top,
                    .Tag = files
                }

                newCard.Children.Add(newStack)
                newCard.SwapControl = newStack

                ' 延迟加载安装项的逻辑
                newCard.InstallMethod = Sub(stack)
                                            Dim list = CType(stack.Tag, List(Of CompFile))
                                            ' 排序和去重检查
                                            list.Sort(Function(a, b) b.ReleaseDate.CompareTo(a.ReleaseDate))
                                            Dim distinctCount = list.Select(Function(f) f.DisplayName).Distinct().Count()
                                            Dim badDisplayName = distinctCount <> list.Count

                                            ' 批量添加子项
                                            Select Case _project.Type
                                                Case CompType.ModPack
                                                    For Each item In list
                                                        stack.Children.Add(item.ToListItem(AddressOf FrmDownloadCompDetail.Install_Click, AddressOf FrmDownloadCompDetail.Save_Click, BadDisplayName:=badDisplayName))
                                                    Next
                                                Case CompType.World
                                                    For Each item In list
                                                        stack.Children.Add(item.ToListItem(AddressOf FrmDownloadCompDetail.InstallWorld_Click, AddressOf FrmDownloadCompDetail.Save_Click, BadDisplayName:=badDisplayName))
                                                    Next
                                                Case Else
                                                    CompFilesCardPreload(stack, list)
                                                    For Each item In list
                                                        stack.Children.Add(item.ToListItem(AddressOf FrmDownloadCompDetail.Save_Click, BadDisplayName:=badDisplayName))
                                                    Next
                                            End Select
                                        End Sub

                PanResults.Children.Add(newCard)

                ' 展开逻辑
                If currentKey = targetCardName OrElse additionalTitles.Contains(newCard.Title) Then
                    newCard.StackInstall()
                Else
                    newCard.IsSwapped = True
                End If

                ' 特殊提示
                If currentKey = "其他" Then
                    newStack.Children.Add(New MyHint With {
                        .Text = "由于版本信息更新缓慢，可能无法识别刚更新的 MC 版本。几天后即可正常识别。",
                        .Theme = MyHint.Themes.Yellow,
                        .Margin = New Thickness(5, 0, 0, 8)
                    })
                End If
            Next

            ' 单卡片自动展开
            If PanResults.Children.Count = 1 Then
                DirectCast(PanResults.Children(0), MyCard).IsSwapped = False
            End If

        Catch ex As Exception
            Log(ex, "可视化工程下载列表出错", LogLevel.Feedback)
        End Try
    End Sub

    ''' <summary>
    ''' 辅助方法：向字典添加数据并处理去重
    ''' </summary>
    Private Sub AddVersionToDict(dict As SortedDictionary(Of String, List(Of CompFile)),
                             checker As Dictionary(Of String, HashSet(Of CompFile)),
                             key As String, version As CompFile)
        If Not dict.ContainsKey(key) Then
            dict.Add(key, New List(Of CompFile))
            checker.Add(key, New HashSet(Of CompFile))
        End If

        ' 使用 HashSet.Add 判断是否重复，比 List.Contains 快得多
        If checker(key).Add(version) Then
            dict(key).Add(version)
        End If
    End Sub
    Private Function GetGroupedVersionName(name As String, groupedByDrop As Boolean, foldOld As Boolean) As String
        If name Is Nothing Then
            Return "其他"
        ElseIf name.Contains("w") Then
            Return "快照版"
        ElseIf Not McInstanceInfo.IsFormatFit(name) OrElse (foldOld AndAlso McInstanceInfo.VersionToDrop(name, True) < 120) Then
            Return "远古版"
        ElseIf groupedByDrop Then
            Return McInstanceInfo.DropToVersion(McInstanceInfo.VersionToDrop(name, True))
        Else
            Return name
        End If
    End Function

#End Region
    Private _isFirstInit As Boolean = True
    Private Sub Init() Handles Me.PageEnter
        AniControlEnabled += 1
        _project = FrmMain.PageCurrent.Additional(0)
        PanBack.ScrollToHome()
        '重启加载器
        If _isFirstInit Then
            '在 Me.Initialized 已经初始化了加载器，不再重复初始化
            _isFirstInit = False
        Else
            PageLoaderRestart(IsForceRestart:=True)
        End If
        '放置当前工程
        If _compItem IsNot Nothing Then PanIntro.Children.Remove(_compItem)
        _compItem = _project.ToCompItem(True, True)
        _compItem.CanInteraction = False
        _compItem.ShowFavoriteBtn = false
        _compItem.Margin = New Thickness(-7, -7, 0, 8)
        PanIntro.Children.Insert(0, _compItem)

        '决定按钮显示
        BtnIntroWeb.Text = If(_project.FromCurseForge, "CurseForge", "Modrinth")
        BtnIntroWiki.Visibility = If(_project.WikiId = 0, Visibility.Collapsed, Visibility.Visible)

        AniControlEnabled -= 1
    End Sub

    '整合包安装
    Public Sub Install_Click(sender As MyListItem, e As EventArgs)
        Try

            '获取基本信息
            Dim File As CompFile = sender.Tag
            Dim LoaderName As String = $"{If(_project.FromCurseForge, "CurseForge", "Modrinth")} 整合包下载：{_project.TranslatedName} "

            '获取实例名
            Dim PackName As String = _project.TranslatedName.Replace(".zip", "").Replace(".rar", "").Replace(".mrpack", "").Replace("\", "＼").Replace("/", "／").Replace("|", "｜").Replace(":", "：").Replace("<", "＜").Replace(">", "＞").Replace("*", "＊").Replace("?", "？").Replace("""", "").Replace("： ", "：")
            Dim Validate As New ValidateFolderName(McFolderSelected & "versions")
            If Validate.Validate(PackName) <> "" Then PackName = ""
            Dim InstanceName As String = MyMsgBoxInput("输入实例名称", "", PackName, New ObjectModel.Collection(Of Validate) From {Validate})
            If String.IsNullOrEmpty(InstanceName) Then Return

            '构造步骤加载器
            Dim Loaders As New List(Of LoaderBase)
            Dim Target As String = $"{McFolderSelected}versions\{InstanceName}\原始整合包.{If(_project.FromCurseForge, "zip", "mrpack")}"
            Dim LogoFileAddress As String = MyImage.GetTempPath(_compItem.Logo)
            Loaders.Add(New LoaderDownload("下载整合包文件", New List(Of NetFile) From {File.ToNetFile(Target)}) With {.ProgressWeight = 10, .Block = True})
            Loaders.Add(New LoaderTask(Of Integer, Integer)("准备安装整合包",
            Sub() ModpackInstall(Target, InstanceName, If(IO.File.Exists(LogoFileAddress), LogoFileAddress, Nothing), File.ProjectId, isOnlineInstall := True)) With {.ProgressWeight = 0.1})

            '启动
            Dim Loader As New LoaderCombo(Of String)(LoaderName, Loaders) With {.OnStateChanged =
            Sub(MyLoader)
                Select Case MyLoader.State
                    Case LoadState.Failed
                        Hint(MyLoader.Name & "失败：" & MyLoader.Error.Message, HintType.Critical)
                    Case LoadState.Aborted
                        Hint(MyLoader.Name & "已取消！", HintType.Info)
                    Case LoadState.Loading
                        Return '不重新加载版本列表
                End Select
                McInstallFailedClearFolder(MyLoader)
            End Sub}
            Loader.Start(McFolderSelected & "versions\" & InstanceName & "\")
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()

        Catch ex As Exception
            Log(ex, "下载资源整合包失败", LogLevel.Feedback)
        End Try
    End Sub
    '世界下载
    Public Sub InstallWorld_Click(sender As MyListItem, e As EventArgs)
        Try

            '获取基本信息
            Dim File As CompFile = sender.Tag
            Dim LoaderName As String = $"{If(_project.FromCurseForge, "CurseForge", "Modrinth")} 世界下载：{_project.TranslatedName} "

            '确认默认保存位置
            Dim DefaultFolder As String = Nothing
            Dim SubFolder As String = "saves\"
            Dim IsVersionSuitable As Func(Of McInstance, Boolean) = Nothing
            '获取资源所需的加载器
            Dim AllowedLoaders As New List(Of CompLoaderType)
            If File.ModLoaders.Any Then
                AllowedLoaders = File.ModLoaders
            ElseIf _project.ModLoaders.Any Then
                AllowedLoaders = _project.ModLoaders
            End If
            Log($"[Comp] 世界要求的加载器种类：" & If(AllowedLoaders.Any(), AllowedLoaders.Join(" / "), "无要求"))
            '判断某个版本是否符合资源要求
            IsVersionSuitable =
                    Function(Version)
                        If Version Is Nothing Then Return False
                        If Not Version.IsLoaded Then Version.Load()
                        If File.GameVersions.Any(Function(v) v.Contains(".")) AndAlso
                               Not File.GameVersions.Any(Function(v) v.Contains(".") AndAlso v = Version.Info.VanillaName) Then Return False
                        '加载器
                        If Not AllowedLoaders.Any() Then Return True '无要求
                        Return False
                    End Function
            '获取常规资源默认下载位置
            If CachedFolder.ContainsKey(File.Type) AndAlso Not String.IsNullOrEmpty(CachedFolder(File.Type)) Then
                DefaultFolder = CachedFolder.GetOrDefault(File.Type, If(McInstanceSelected?.PathIndie, ExePath))
                Log($"[Comp] 使用上次下载时的文件夹作为默认下载位置：{DefaultFolder}")
            ElseIf McInstanceSelected IsNot Nothing AndAlso IsVersionSuitable(McInstanceSelected) Then
                DefaultFolder = $"{McInstanceSelected.PathIndie}{SubFolder}"
                Directory.CreateDirectory(DefaultFolder)
                Log($"[Comp] 使用当前实例作为默认下载位置：{DefaultFolder}")
            Else
                '查找所有可能的实例
                Dim NeedLoad As Boolean = McInstanceListLoader.State <> LoadState.Finished
                If NeedLoad Then
                    Hint("正在查找适合的游戏实例……")
                    LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\", WaitForExit:=True)
                End If
                Dim SuitableVersions = McInstanceList.Values.SelectMany(Function(l) l).Where(Function(v) IsVersionSuitable(v)).
                            Select(Function(v) New DirectoryInfo($"{v.PathIndie}{SubFolder}"))
                If SuitableVersions.Any Then
                    Dim SelectedVersion = SuitableVersions.
                                OrderByDescending(Function(Dir) If(Dir.Exists, Dir.LastWriteTimeUtc, Date.MinValue)). '先按文件夹更改时间降序
                                ThenByDescending(Function(Dir) If(Dir.Exists, Dir.GetFiles().Length, -1)). '再按文件夹中的文件数量降序
                                First()
                    DefaultFolder = SelectedVersion.FullName
                    Directory.CreateDirectory(DefaultFolder)
                    Log($"[Comp] 使用适合的游戏实例作为默认下载位置：{DefaultFolder}")
                Else
                    DefaultFolder = McFolderSelected
                    If NeedLoad Then
                        Hint("当前 MC 文件夹中没有找到适合此资源文件的实例！")
                    Else
                        Log("[Comp] 由于当前实例不兼容，使用当前的 MC 文件夹作为默认下载位置")
                    End If
                End If
            End If

            Dim Target As String = SystemDialogs.SelectSaveFile("选择世界安装位置 (saves 文件夹)", File.FileName, "世界文件|" & "*.zip", DefaultFolder)
            If String.IsNullOrEmpty(Target) Then Return

            '构造步骤加载器
            Dim Loaders As New List(Of LoaderBase)
            Dim TargetPath As String = Target.BeforeLast("\")
            Dim LogoFileAddress As String = MyImage.GetTempPath(_compItem.Logo)
            Loaders.Add(New LoaderDownload("下载世界文件", New List(Of NetFile) From {File.ToNetFile(Target)}) With {.ProgressWeight = 10, .Block = True})
            Loaders.Add(New LoaderTask(Of Integer, Integer)("安装世界", Sub() ExtractFile(Target, TargetPath, Encoding.UTF8)) With {.ProgressWeight = 0.1, .Block = True})
            Loaders.Add(New LoaderTask(Of Integer, Integer)("清理缓存", Sub() IO.File.Delete(Target)))

            '启动
            Dim Loader As New LoaderCombo(Of Integer)(LoaderName, Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Loader.Start()
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()

        Catch ex As Exception
            Log(ex, "下载世界资源失败", LogLevel.Feedback)
        End Try
    End Sub
    '资源下载；整合包另存为
    Public Shared CachedFolder As New Dictionary(Of CompType, String) '仅在本次缓存的下载文件夹
    Public Sub Save_Click(sender As Object, e As EventArgs)
        Dim File As CompFile = If(TypeOf sender Is MyListItem, sender, sender.Parent).Tag
        RunInNewThread(
        Sub()
            Try
                Dim Desc As String = Nothing
                Select Case File.Type
                    Case CompType.ModPack : Desc = "整合包"
                    Case CompType.Mod : Desc = "Mod "
                    Case CompType.ResourcePack : Desc = "资源包"
                    Case CompType.Shader : Desc = "光影包"
                    Case CompType.DataPack : Desc = "数据包"
                    Case CompType.World : Desc = "世界"
                End Select
                '确认默认保存位置
                Dim DefaultFolder As String = Nothing
                If File.Type <> CompType.ModPack Then
                    Dim SubFolder As String = Nothing
                    Select Case File.Type
                        Case CompType.Mod : SubFolder = "mods\"
                        Case CompType.ResourcePack : SubFolder = "resourcepacks\"
                        Case CompType.Shader : SubFolder = "shaderpacks\"
                        Case CompType.World : SubFolder = "saves\"
                        Case CompType.DataPack : SubFolder = "" '导航到版本根目录
                    End Select
                    Dim IsVersionSuitable As Func(Of McInstance, Boolean) = Nothing
                    '获取资源所需的加载器
                    Dim AllowedLoaders As New List(Of CompLoaderType)
                    If File.ModLoaders.Any Then
                        AllowedLoaders = File.ModLoaders
                    ElseIf _project.ModLoaders.Any Then
                        AllowedLoaders = _project.ModLoaders
                    End If
                    Log($"[Comp] {Desc}要求的加载器种类：" & If(AllowedLoaders.Any(), AllowedLoaders.Join(" / "), "无要求"))
                    '判断某个版本是否符合资源要求
                    IsVersionSuitable =
                    Function(Version)
                        If Version Is Nothing Then Return False
                        If Not Version.IsLoaded Then Version.Load()
                        '只对 Mod 和数据包进行版本检测
                        If File.Type = CompType.Mod OrElse File.Type = CompType.DataPack Then
                            If File.GameVersions.Any(Function(v) v.Contains(".")) AndAlso
                               Not File.GameVersions.Any(Function(v) v.Contains(".") AndAlso v = Version.Info.VanillaName) Then Return False
                        End If
                        '加载器
                        If Not AllowedLoaders.Any() Then Return True '无要求
                        If AllowedLoaders.Contains(CompLoaderType.Forge) AndAlso Version.Info.HasForge Then Return True
                        If AllowedLoaders.Contains(CompLoaderType.Fabric) AndAlso Version.Info.HasFabric OrElse Version.Info.HasLegacyFabric Then Return True
                        If AllowedLoaders.Contains(CompLoaderType.NeoForge) AndAlso Version.Info.HasNeoForge Then Return True
                        If AllowedLoaders.Contains(CompLoaderType.LiteLoader) AndAlso Version.Info.HasLiteLoader Then Return True
                        Return False
                    End Function
                    '获取常规资源默认下载位置
                    If CachedFolder.ContainsKey(File.Type) AndAlso Not String.IsNullOrEmpty(CachedFolder(File.Type)) Then
                        DefaultFolder = CachedFolder.GetOrDefault(File.Type, If(McInstanceSelected?.PathIndie, ExePath))
                        Log($"[Comp] 使用上次下载时的文件夹作为默认下载位置：{DefaultFolder}")
                    ElseIf McInstanceSelected IsNot Nothing AndAlso IsVersionSuitable(McInstanceSelected) Then
                        DefaultFolder = $"{McInstanceSelected.PathIndie}{SubFolder}"
                        Directory.CreateDirectory(DefaultFolder)
                        Log($"[Comp] 使用当前实例作为默认下载位置：{DefaultFolder}")
                    Else
                        '查找所有可能的实例
                        Dim NeedLoad As Boolean = McInstanceListLoader.State <> LoadState.Finished
                        If NeedLoad Then
                            Hint("正在查找适合的游戏实例……")
                            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\", WaitForExit:=True)
                        End If
                        Dim SuitableVersions = McInstanceList.Values.SelectMany(Function(l) l).Where(Function(v) IsVersionSuitable(v)).
                            Select(Function(v) New DirectoryInfo($"{v.PathIndie}{SubFolder}"))
                        If SuitableVersions.Any Then
                            Dim SelectedVersion = SuitableVersions.
                                OrderByDescending(Function(Dir) If(Dir.Exists, Dir.LastWriteTimeUtc, Date.MinValue)). '先按文件夹更改时间降序
                                ThenByDescending(Function(Dir) If(Dir.Exists, Dir.GetFiles().Length, -1)). '再按文件夹中的文件数量降序
                                First()
                            DefaultFolder = SelectedVersion.FullName
                            Directory.CreateDirectory(DefaultFolder)
                            Log($"[Comp] 使用适合的游戏实例作为默认下载位置：{DefaultFolder}")
                        Else
                            DefaultFolder = McFolderSelected
                            If NeedLoad Then
                                Hint("当前 MC 文件夹中没有找到适合此资源文件的实例！")
                            Else
                                Log("[Comp] 由于当前实例不兼容，使用当前的 MC 文件夹作为默认下载位置")
                            End If
                        End If
                    End If
                End If
                '获取基本信息
                Dim FileName = CompFileNameGet(_project, File)
                RunInUi(
                Sub()
                    '弹窗要求选择保存位置
                    Dim Target As String
                    Target = SystemDialogs.SelectSaveFile("选择保存位置", FileName,
                        Desc & "文件|" &
                        If(File.Type = CompType.Mod,
                            If(File.FileName.EndsWith(".litemod"), "*.litemod", "*.jar"),
                            If(File.FileName.EndsWith(".mrpack"), "*.mrpack", "*.zip")), DefaultFolder)
                    If Not Target.Contains("\") Then Return
                    '构造步骤加载器
                    Dim LoaderName As String = Desc & "下载：" & GetFileNameWithoutExtentionFromPath(Target) & " "
                    If Target <> DefaultFolder Then
                        If CachedFolder.ContainsKey(File.Type) Then
                            CachedFolder(File.Type) = GetPathFromFullPath(Target)
                        Else
                            CachedFolder.Add(File.Type, GetPathFromFullPath(Target))
                        End If
                    End If
                    Dim Loaders As New List(Of LoaderBase)
                    Loaders.Add(New LoaderDownload("下载文件", New List(Of NetFile) From {File.ToNetFile(Target)}) With {.ProgressWeight = 6, .Block = True})
                    '启动
                    Dim Loader As New LoaderCombo(Of Integer)(LoaderName, Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
                    Loader.Start(1)
                    LoaderTaskbarAdd(Loader)
                    FrmMain.BtnExtraDownload.ShowRefresh()
                    FrmMain.BtnExtraDownload.Ribble()
                End Sub)
            Catch ex As Exception
                Log(ex, "保存资源文件失败", LogLevel.Feedback)
            End Try
        End Sub, "Download CompDetail Save")
    End Sub

    Private Sub BtnIntroWeb_Click(sender As Object, e As EventArgs) Handles BtnIntroWeb.Click
        OpenWebsite(_project.Website)
    End Sub
    Private Sub BtnIntroWiki_Click(sender As Object, e As EventArgs) Handles BtnIntroWiki.Click
        OpenWebsite("https://www.mcmod.cn/class/" & _project.WikiId & ".html")
    End Sub
    Private Sub BtnIntroCopy_Click(sender As Object, e As EventArgs) Handles BtnIntroCopy.Click
        ClipboardSet(_compItem.LabTitle.Text & _compItem.LabTitleRaw.Text)
    End Sub
    Private Sub BtnFavorites_Click(sender As Object, e As EventArgs) Handles BtnFavorites.Click
        CompFavorites.ShowMenu(_project, sender)
    End Sub
    Private Sub BtnIntroLinkCopy_Click(sender As Object, e As EventArgs) Handles BtnIntroLinkCopy.Click
        CompClipboard.CurrentText = _project.Website
        ClipboardSet(_project.Website)
    End Sub
    '翻译简介
    Private Async Sub BtnTranslate_Click(sender As Object, e As EventArgs) Handles BtnTranslate.Click
        Hint($"正在获取 {_project.TranslatedName} 的简介译文……")
        Dim ChineseDescription = Await _project.ChineseDescription
        If ChineseDescription Is Nothing Then Return
        MyMsgBox($"原文：{_project.Description}{Environment.NewLine}译文：{ChineseDescription}")
    End Sub

    ''' <summary>
    ''' 刷新收藏按钮的显示状态
    ''' </summary>
    Public Sub RefreshFavoriteButton()
        Try
            If _project IsNot Nothing Then
                ' 刷新顶部的项目卡片收藏状态
                If _compItem IsNot Nothing Then
                    _compItem.RefreshFavoriteStatus()
                End If
            End If
        Catch ex As Exception
            Log(ex, "刷新收藏按钮状态时出错")
        End Try
    End Sub

End Class

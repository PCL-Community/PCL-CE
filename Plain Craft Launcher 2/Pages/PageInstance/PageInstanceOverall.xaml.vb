Imports Microsoft.VisualBasic.FileIO
Imports PCL.Core.Minecraft
Imports PCL.Core.UI
Imports NEWSetup = PCL.Core.ProgramSetup.Setup

Public Class PageInstanceOverall

    Private IsLoad As Boolean = False
    Private Sub PageSetupLaunch_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '更新设置
        ItemDisplayLogoCustom.Tag = "PCL\Logo.png"
        Reload()

        '非重复加载部分
        If IsLoad Then Return
        IsLoad = True
        PanDisplay.TriggerForceResize()

    End Sub

    Public ItemVersion As MyListItem
    ''' <summary>
    ''' 确保当前页面上的信息已正确显示。
    ''' </summary>
    Private Sub Reload()
        AniControlEnabled += 1

        Dim instance = PageInstanceLeft.Instance
        '刷新设置项目
        ComboDisplayType.SelectedIndex = NEWSetup.Instance.DisplayType(instance.Path)
        BtnDisplayStar.Text = If(instance.IsStar, "从收藏夹中移除", "加入收藏夹")
        BtnFolderMods.Visibility = If(instance.Modable, Visibility.Visible, Visibility.Collapsed)
        '刷新实例显示
        PanDisplayItem.Children.Clear()
        ItemVersion = PageSelectRight.McVersionListItem(instance)
        ItemVersion.IsHitTestVisible = False
        PanDisplayItem.Children.Add(ItemVersion)
        FrmMain.PageNameRefresh()
        '刷新实例信息
        GetInstanceInfo()
        '刷新实例图标
        ComboDisplayLogo.SelectedIndex = 0
        Dim Logo As String = NEWSetup.Instance.LogoPath(instance.Path)
        Dim LogoCustom As Boolean = NEWSetup.Instance.IsLogoCustom(instance.Path)
        If LogoCustom Then
            For Each Selection As MyComboBoxItem In ComboDisplayLogo.Items
                If Selection.Tag = Logo OrElse (Selection.Tag = "PCL\Logo.png" AndAlso Logo.EndsWith("PCL\Logo.png")) Then
                    ComboDisplayLogo.SelectedItem = Selection
                    Exit For
                End If
            Next
        End If

        AniControlEnabled -= 1
    End Sub

    Private InstanceInfoLoader As LoaderCombo(Of Integer)
    Private ModpackCompItem As MyCompItem
    Private Sub GetInstanceInfo()
        ModpackCompItem = Nothing
        RunInUi(Sub()
                    PanInfo.Children.Clear()
                    PanInfo.Children.Add(New MyLoading With {.Text = "正在获取信息", .Margin = New Thickness(0, 0, 0, 10)})
                End Sub)
        Dim loaders As New List(Of LoaderBase)
        loaders.Add(New LoaderTask(Of Integer, Integer)("获取可能的整合包信息", Sub()
                                                                          Dim modpackId = NEWSetup.Instance.ModpackId(PageInstanceLeft.Instance.Path)
                                                                          If Not String.IsNullOrWhiteSpace(modpackId) Then
                                                                              Dim compProjects = CompRequest.GetCompProjectsByIds(New List(Of String) From {NEWSetup.Instance.ModpackId(PageInstanceLeft.Instance.Path)})
                                                                              If Not compProjects.Count = 0 Then RunInUi(Sub()
                                                                                                                             ModpackCompItem = compProjects.First().ToCompItem(False, False)
                                                                                                                             ModpackCompItem.Tag = compProjects.First()
                                                                                                                         End Sub)
                                                                          End If
                                                                      End Sub) With {.Block = True})
        loaders.Add(New LoaderTask(Of Integer, Integer)("获取实例信息", Sub()
                                                                      RunInUi(Sub()
                                                                                  Dim instance = PageInstanceLeft.Instance
                                                                                  Dim instanceInfo = instance.Version
                                                                                  Dim items As New List(Of MyListItem)
                                                                                  Dim launchCount = NEWSetup.Instance.LaunchCount(instance.Path)
                                                                                  If launchCount = 0 Then
                                                                                      items.Add(New MyListItem With {.Title = "启动次数", .Info = "从未启动", .Logo = "pack://application:,,,/images/Blocks/RedstoneLampOff.png"})
                                                                                  Else
                                                                                      items.Add(New MyListItem With {.Title = "启动次数", .Info = "已启动 " & NEWSetup.Instance.LaunchCount(instance.Path).ToString() & " 次", .Logo = "pack://application:,,,/images/Blocks/RedstoneLampOn.png"})
                                                                                  End If
                                                                                  If Not String.IsNullOrWhiteSpace(NEWSetup.Instance.ModpackVersion(instance.Path)) Then items.Add(New MyListItem With {.Title = "整合包版本", .Info = NEWSetup.Instance.ModpackVersion(instance.Path), .Logo = "pack://application:,,,/images/Blocks/CommandBlock.png"})
                                                                                  items.Add(New MyListItem With {.Title = "Minecraft", .Info = instanceInfo.McName, .Logo = "pack://application:,,,/images/Blocks/Grass.png"})
                                                                                  If instanceInfo.HasForge Then items.Add(New MyListItem With {.Title = "Forge", .Info = instanceInfo.ForgeVersion, .Logo = "pack://application:,,,/images/Blocks/Anvil.png"})
                                                                                  If instanceInfo.HasNeoForge Then items.Add(New MyListItem With {.Title = "NeoForge", .Info = instanceInfo.NeoForgeVersion, .Logo = "pack://application:,,,/images/Blocks/NeoForge.png"})
                                                                                  If instanceInfo.HasCleanroom Then items.Add(New MyListItem With {.Title = "Cleanroom", .Info = instanceInfo.CleanroomVersion, .Logo = "pack://application:,,,/images/Blocks/Cleanroom.png"})
                                                                                  If instanceInfo.HasFabric Then items.Add(New MyListItem With {.Title = "Fabric", .Info = instanceInfo.FabricVersion, .Logo = "pack://application:,,,/images/Blocks/Fabric.png"})
                                                                                  If instanceInfo.HasQuilt Then items.Add(New MyListItem With {.Title = "Quilt", .Info = instanceInfo.QuiltVersion, .Logo = "pack://application:,,,/images/Blocks/Quilt.png"})
                                                                                  If instanceInfo.HasOptiFine Then items.Add(New MyListItem With {.Title = "OptiFine", .Info = instanceInfo.OptiFineVersion, .Logo = "pack://application:,,,/images/Blocks/GrassPath.png"})
                                                                                  If instanceInfo.HasLiteLoader Then items.Add(New MyListItem With {.Title = "LiteLoader", .Info = "已安装", .Logo = "pack://application:,,,/images/Blocks/Egg.png"})
                                                                                  If instanceInfo.HasLegacyFabric Then items.Add(New MyListItem With {.Title = "Legacy Fabric", .Info = instanceInfo.LegacyFabricVersion, .Logo = "pack://application:,,,/images/Blocks/Fabric.png"})
                                                                                  If instanceInfo.HasLabyMod Then items.Add(New MyListItem With {.Title = "LabyMod", .Info = instanceInfo.LabyModVersion, .Logo = "pack://application:,,,/images/Blocks/LabyMod.png"})
                                                                                  Dim wrapPanel As New WrapPanel With {.Margin = New Thickness(0, -5, -20, 7)}
                                                                                  For Each item In items
                                                                                      wrapPanel.Children.Add(item)
                                                                                      wrapPanel.Children.Add(New TextBlock With{.Width = 2})
                                                                                  Next
                                                                                  PanInfo.Children.Clear()
                                                                                  If ModpackCompItem IsNot Nothing Then
                                                                                      PanInfo.Children.Add(ModpackCompItem)
                                                                                      PanInfo.Children.Add(New TextBlock)
                                                                                  End If
                                                                                  PanInfo.Children.Add(wrapPanel)
                                                                              End Sub)
                                                                  End Sub))
        InstanceInfoLoader = New LoaderCombo(Of Integer)("Instance Info Loader", loaders) With {.Show = False}
        InstanceInfoLoader.Start()
    End Sub

#Region "卡片：个性化"

    '实例分类
    Private Sub ComboDisplayType_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboDisplayType.SelectionChanged
        If Not (IsLoad AndAlso AniControlEnabled = 0) Then Return
        If ComboDisplayType.SelectedIndex <> 1 Then
            '改为不隐藏
            Try
                '若设置分类为可安装 Mod，则显示正常的 Mod 管理页面
                NEWSetup.Instance.DisplayType(PageInstanceLeft.Instance.Path) = ComboDisplayType.SelectedIndex
                PageInstanceLeft.Instance.DisplayType = NEWSetup.Instance.DisplayType(PageInstanceLeft.Instance.Path)
                FrmInstanceLeft.RefreshModDisabled()

                WriteIni(PathMcFolder & "PCL.ini", "InstanceCache", "") '要求刷新缓存
                LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            Catch ex As Exception
                Log(ex, "修改实例分类失败（" & PageInstanceLeft.Instance.Name & "）", LogLevel.Feedback)
            End Try
            Reload() '更新 “打开 Mod 文件夹” 按钮
        Else
            '改为隐藏
            Try
                If Not Setup.Get("HintHide") Then
                    If MyMsgBox("确认要从实例列表中隐藏该实例吗？隐藏该实例后，它将不再出现于 PCL 显示的实例列表中。" & vbCrLf & "此后，在实例列表页面按下 F11 才可以查看被隐藏的实例。", "隐藏实例提示",, "取消") <> 1 Then
                        ComboDisplayType.SelectedIndex = 0
                        Return
                    End If
                    Setup.Set("HintHide", True)
                End If
                NEWSetup.Instance.DisplayType(PageInstanceLeft.Instance.Path) = CInt(McInstanceCardType.Hidden)
                WriteIni(PathMcFolder & "PCL.ini", "InstanceCache", "") '要求刷新缓存
                LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            Catch ex As Exception
                Log(ex, "隐藏实例 " & PageInstanceLeft.Instance.Name & " 失败", LogLevel.Feedback)
            End Try
        End If
    End Sub

    '更改描述
    Private Sub BtnDisplayDesc_Click(sender As Object, e As EventArgs) Handles BtnDisplayDesc.Click
        Try
            Dim OldInfo As String = NEWSetup.Instance.CustomInfo(PageInstanceLeft.Instance.Path)
            Dim NewInfo As String = MyMsgBoxInput("更改描述", "修改实例的描述文本，留空则使用 PCL 的默认描述。", OldInfo, New ObjectModel.Collection(Of Validate), "默认描述")
            If NewInfo IsNot Nothing AndAlso OldInfo <> NewInfo Then NEWSetup.Instance.CustomInfo(PageInstanceLeft.Instance.Path) = NewInfo
            PageInstanceLeft.Instance = New McInstance(PageInstanceLeft.Instance.Name).Load()
            Reload()
            LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Log(ex, "实例 " & PageInstanceLeft.Instance.Name & " 描述更改失败", LogLevel.Msgbox)
        End Try
    End Sub

    '重命名实例
    Private Sub BtnDisplayRename_Click(sender As Object, e As EventArgs) Handles BtnDisplayRename.Click
        Try
            '确认输入的新名称
            Dim OldName As String = PageInstanceLeft.Instance.Name
            Dim OldPath As String = PageInstanceLeft.Instance.Path
            '修改此部分的同时修改快速安装的实例名检测*
            Dim NewName As String = MyMsgBoxInput("重命名实例", "", OldName, New ObjectModel.Collection(Of Validate) From {New ValidateFolderName(PathMcFolder & "versions", IgnoreCase:=False)})
            If String.IsNullOrWhiteSpace(NewName) Then Return
            Dim NewPath As String = PathMcFolder & "versions\" & NewName & "\"
            '获取临时中间名，以防止仅修改大小写的重命名失败
            Dim TempName As String = NewName & "_temp"
            Dim TempPath As String = PathMcFolder & "versions\" & TempName & "\"
            Dim IsCaseChangedOnly As Boolean = NewName.ToLower = OldName.ToLower
            '重新加载实例 Json 信息，避免 HMCL 项被合并
            Dim JsonObject As JObject
            Try
                JsonObject = GetJson(ReadFile(PageInstanceLeft.Instance.Path & PageInstanceLeft.Instance.Name & ".json"))
            Catch ex As Exception
                Log(ex, "重命名读取 Json 时失败")
                JsonObject = PageInstanceLeft.Instance.JsonObject
            End Try
            '重命名主文件夹
            FileSystem.RenameDirectory(OldPath, TempName)
            FileSystem.RenameDirectory(TempPath, NewName)
            '清理 ini 缓存
            IniClearCache(PageInstanceLeft.Instance.PathIndie & "options.txt")
            '重命名 Jar 文件与 natives 文件夹
            '不能进行遍历重命名，否则在实例名很短的时候容易误伤其他文件（Meloong-Git/#6443）
            If Directory.Exists($"{NewPath}{OldName}-natives") Then
                If IsCaseChangedOnly Then
                    FileSystem.RenameDirectory($"{NewPath}{OldName}-natives", $"{OldName}natives_temp")
                    FileSystem.RenameDirectory($"{NewPath}{OldName}-natives_temp", $"{NewName}-natives")
                Else
                    DeleteDirectory($"{NewPath}{NewName}-natives")
                    FileSystem.RenameDirectory($"{NewPath}{OldName}-natives", $"{NewName}-natives")
                End If
            End If
            If File.Exists($"{NewPath}{OldName}.jar") Then
                If IsCaseChangedOnly Then
                    FileSystem.RenameFile($"{NewPath}{OldName}.jar", $"{OldName}_temp.jar")
                    FileSystem.RenameFile($"{NewPath}{OldName}_temp.jar", $"{NewName}.jar")
                Else
                    File.Delete($"{NewPath}{NewName}.jar")
                    FileSystem.RenameFile($"{NewPath}{OldName}.jar", $"{NewName}.jar")
                End If
            End If
            '替换实例设置文件中的路径
            If File.Exists(NewPath & "PCL\Setup.ini") Then
                WriteFile(NewPath & "PCL\Setup.ini", ReadFile(NewPath & "PCL\Setup.ini").Replace(OldPath, NewPath))
            End If
            '更改已选中的实例
            If ReadIni(PathMcFolder & "PCL.ini", "Version") = OldName Then
                WriteIni(PathMcFolder & "PCL.ini", "Version", NewName)
            End If
            '写入实例 Json
            Try
                JsonObject("id") = NewName
                WriteFile(NewPath & NewName & ".json", JsonObject.ToString)
            Catch ex As Exception
                Log(ex, "重命名实例 Json 失败")
            End Try
            '刷新与提示
            Hint("重命名成功！", HintType.Finish)
            PageInstanceLeft.Instance = New McInstance(NewName).Load()
            If Not IsNothing(McInstanceCurrent) AndAlso McInstanceCurrent.Equals(PageInstanceLeft.Instance) Then WriteIni(PathMcFolder & "PCL.ini", "Version", NewName)
            Reload()
            LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Log(ex, "重命名实例失败", LogLevel.Msgbox)
        End Try
    End Sub

    '实例图标
    Private Sub ComboDisplayLogo_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboDisplayLogo.SelectionChanged
        If Not (IsLoad AndAlso AniControlEnabled = 0) Then Return
        '选择 自定义 时修改图片
        Try
            If ComboDisplayLogo.SelectedItem Is ItemDisplayLogoCustom Then
                Dim FileName As String = SystemDialogs.SelectFile("常用图片文件(*.png;*.jpg;*.gif)|*.png;*.jpg;*.gif", "选择图片")
                If FileName = "" Then
                    Reload() '还原选项
                    Return
                End If
                CopyFile(FileName, PageInstanceLeft.Instance.Path & "PCL\Logo.png")
            Else
                File.Delete(PageInstanceLeft.Instance.Path & "PCL\Logo.png")
            End If
        Catch ex As Exception
            Log(ex, "更改自定义实例图标失败（" & PageInstanceLeft.Instance.Name & "）", LogLevel.Feedback)
        End Try
        '进行更改
        Try
            Dim NewLogo As String = ComboDisplayLogo.SelectedItem.Tag
            NEWSetup.Instance.LogoPath(PageInstanceLeft.Instance.Path) = NewLogo
            NEWSetup.Instance.IsLogoCustom(PageInstanceLeft.Instance.Path) = Not NewLogo = ""
            '刷新显示
            WriteIni(PathMcFolder & "PCL.ini", "InstanceCache", "") '要求刷新缓存
            PageInstanceLeft.Instance = New McInstance(PageInstanceLeft.Instance.Name).Load()
            Reload()
            LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Log(ex, "更改实例图标失败（" & PageInstanceLeft.Instance.Name & "）", LogLevel.Feedback)
        End Try
    End Sub

    '收藏夹
    Private Sub BtnDisplayStar_Click(sender As Object, e As EventArgs) Handles BtnDisplayStar.Click
        Try
            NEWSetup.Instance.Starred(PageInstanceLeft.Instance.Path) = Not PageInstanceLeft.Instance.IsStar
            PageInstanceLeft.Instance = New McInstance(PageInstanceLeft.Instance.Name).Load()
            Reload()
            McInstanceListForceRefresh = True
            LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Log(ex, "实例 " & PageInstanceLeft.Instance.Name & " 收藏状态更改失败", LogLevel.Msgbox)
        End Try
    End Sub

#End Region

#Region "卡片：快捷方式"

    '实例文件夹
    Private Sub BtnFolderVersion_Click() Handles BtnFolderVersion.Click
        OpenVersionFolder(PageInstanceLeft.Instance)
    End Sub
    Public Shared Sub OpenVersionFolder(Version As McInstance)
        OpenExplorer(Version.Path)
    End Sub

    '存档文件夹
    Private Sub BtnFolderSaves_Click() Handles BtnFolderSaves.Click
        Dim FolderPath As String = PageInstanceLeft.Instance.PathIndie & "saves\"
        Directory.CreateDirectory(FolderPath)
        OpenExplorer(FolderPath)
    End Sub

    'Mod 文件夹
    Private Sub BtnFolderMods_Click() Handles BtnFolderMods.Click
        Dim FolderPath As String = PageInstanceLeft.Instance.PathIndie & "mods\"
        Directory.CreateDirectory(FolderPath)
        OpenExplorer(FolderPath)
    End Sub

#End Region

#Region "卡片：管理"

    '导出启动脚本
    Private Sub BtnManageScript_Click() Handles BtnManageScript.Click
        Try
            '弹窗要求指定脚本的保存位置
            Dim SavePath As String = SystemDialogs.SelectSaveFile("选择脚本保存位置", "启动 " & PageInstanceLeft.Instance.Name & ".bat", "批处理文件(*.bat)|*.bat")
            If SavePath = "" Then Return
            '检查中断（等玩家选完弹窗指不定任务就结束了呢……）
            If McLaunchLoader.State = LoadState.Loading Then
                Hint("请在当前启动任务结束后再试！", HintType.Critical)
                Return
            End If
            '生成脚本
            If McLaunchStart(New McLaunchOptions With {.SaveBatch = SavePath, .Version = PageInstanceLeft.Instance}) Then
                If SelectedProfile.Type = McLoginType.Legacy Then
                    Hint("正在导出启动脚本……")
                Else
                    Hint("正在导出启动脚本……（注意，使用脚本启动可能会导致登录失效！）")
                End If
            End If
        Catch ex As Exception
            Log(ex, "导出启动脚本失败（" & PageInstanceLeft.Instance.Name & "）", LogLevel.Msgbox)
        End Try
    End Sub

    '补全文件
    Private Sub BtnManageCheck_Click(sender As Object, e As EventArgs) Handles BtnManageCheck.Click
        Try
            '忽略文件检查提示
            If ShouldIgnoreFileCheck(PageInstanceLeft.Instance) Then
                Hint("请先关闭 [实例设置 → 设置 → 高级启动选项 → 关闭文件校验]，然后再尝试补全文件！", HintType.Info)
                Return
            End If
            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar
                If OngoingLoader.Name <> PageInstanceLeft.Instance.Name & " 文件补全" Then Continue For
                Hint("正在处理中，请稍候！", HintType.Critical)
                Return
            Next
            '启动
            Dim Loader As New LoaderCombo(Of String)(PageInstanceLeft.Instance.Name & " 文件补全", DlClientFix(PageInstanceLeft.Instance, True, AssetsIndexExistsBehaviour.AlwaysDownload))
            Loader.OnStateChanged =
            Sub()
                Select Case Loader.State
                    Case LoadState.Finished
                        Hint(Loader.Name & "成功！", HintType.Finish)
                    Case LoadState.Failed
                        Hint(Loader.Name & "失败：" & Loader.Error.Message, HintType.Critical)
                    Case LoadState.Aborted
                        Hint(Loader.Name & "已取消！", HintType.Info)
                End Select
            End Sub
            Loader.Start(PageInstanceLeft.Instance.Name)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
        Catch ex As Exception
            Log(ex, "尝试补全文件失败（" & PageInstanceLeft.Instance.Name & "）", LogLevel.Msgbox)
        End Try
    End Sub

    '重置
    Private Sub BtnManageRestore_Click(sender As Object, e As EventArgs) Handles BtnManageRestore.Click
        Try
            Dim CurrentVersion = PageInstanceLeft.Instance.Version
            If Not CurrentVersion.McCodeMain = 99 AndAlso VersionSortInteger(CurrentVersion.McName, "1.5.2") = -1 AndAlso CurrentVersion.HasForge Then
                Hint("该实例暂不支持重置！", HintType.Info)
                Exit Sub
            End If
            '确认操作
            If MyMsgBox("你确定要重置实例 " & PageInstanceLeft.Instance.Name & " 吗？" & vbCrLf & "PCL 将会尝试重新从互联网获取此实例的资源文件信息，并重新执行自动安装。", "实例重置确认", "确认", "取消") = 2 Then Exit Sub

            '备份实例核心文件
            CopyFile(PageInstanceLeft.Instance.Path + PageInstanceLeft.Instance.Name + ".json", PageInstanceLeft.Instance.Path + "PCLInstallBackups\" + PageInstanceLeft.Instance.Name + ".json")
            CopyFile(PageInstanceLeft.Instance.Path + PageInstanceLeft.Instance.Name + ".jar", PageInstanceLeft.Instance.Path + "PCLInstallBackups\" + PageInstanceLeft.Instance.Name + ".jar")
            '提交安装申请
            Dim Request As New McInstallRequest With {
                .TargetInstanceName = PageInstanceLeft.Instance.Name,
                .TargetInstanceFolder = $"{PathMcFolder}versions\{PageInstanceLeft.Instance.Name}\",
                .MinecraftName = CurrentVersion.McName,
                .OptiFineEntry = If(CurrentVersion.HasOptiFine, New DlOptiFineListEntry With {.Inherit = CurrentVersion.McName, .NameDisplay = CurrentVersion.McName + " " + CurrentVersion.OptiFineVersion}, Nothing),
                .ForgeEntry = If(CurrentVersion.HasForge, New DlForgeVersionEntry(CurrentVersion.ForgeVersion, Nothing, Inherit:=CurrentVersion.McName) With {.Category = "installer"}, Nothing),
                .ForgeVersion = If(CurrentVersion.HasForge, CurrentVersion.ForgeVersion, Nothing),
                .NeoForgeVersion = If(CurrentVersion.HasNeoForge, CurrentVersion.NeoForgeVersion, Nothing),
                .CleanroomVersion = If(CurrentVersion.HasCleanroom, CurrentVersion.CleanroomVersion, Nothing),
                .FabricVersion = If(CurrentVersion.HasFabric, CurrentVersion.FabricVersion, Nothing),
                .QuiltVersion = If(CurrentVersion.HasQuilt, CurrentVersion.QuiltVersion, Nothing),
                .LiteLoaderEntry = If(CurrentVersion.HasLiteLoader, New DlLiteLoaderListEntry With {.Inherit = CurrentVersion.McName}, Nothing),
                .LegacyFabricVersion = If(CurrentVersion.HasLegacyFabric, CurrentVersion.LegacyFabricVersion, Nothing)
            }
            '.MinecraftJson = CurrentVersion.McName,
            If Not McInstall(Request, "重置") Then Exit Sub
            FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.Launch})
        Catch ex As Exception
            Log(ex, "重置实例 " & PageInstanceLeft.Instance.Name & " 失败", LogLevel.Msgbox)
        End Try
    End Sub

    '测试游戏
    Private Sub BtnManageTest_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnManageTest.Click
        Try
            McLaunchStart(New McLaunchOptions With
                 {.Version = PageInstanceLeft.Instance, .Test = True})
            FrmMain.PageChange(FormMain.PageType.Launch)
        Catch ex As Exception
            Log(ex, "测试游戏失败", LogLevel.Feedback)
        End Try
    End Sub

    '删除实例
    '修改此代码时，同时修改 PageSelectRight 中的代码
    Private Sub BtnManageDelete_Click(sender As Object, e As EventArgs) Handles BtnManageDelete.Click
        Try
            Dim IsShiftPressed As Boolean = Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift)
            Dim IsHintIndie As Boolean = PageInstanceLeft.Instance.State <> McInstanceState.Error AndAlso PageInstanceLeft.Instance.PathIndie <> PathMcFolder
            Select Case MyMsgBox($"你确定要{If(IsShiftPressed, "永久", "")}删除实例 {PageInstanceLeft.Instance.Name} 吗？" &
                        If(IsHintIndie, vbCrLf & "由于该实例开启了版本隔离，删除时该实例对应的存档、资源包、Mod 等文件也将被一并删除！", ""),
                        "实例删除确认", , "取消",, IsHintIndie OrElse IsShiftPressed)
                Case 1
                    IniClearCache(PageInstanceLeft.Instance.PathIndie & "options.txt")
                    If IsShiftPressed Then
                        DeleteDirectory(PageInstanceLeft.Instance.Path)
                        Hint("实例 " & PageInstanceLeft.Instance.Name & " 已永久删除！", HintType.Finish)
                    Else
                        FileSystem.DeleteDirectory(PageInstanceLeft.Instance.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin)
                        Hint("实例 " & PageInstanceLeft.Instance.Name & " 已删除到回收站！", HintType.Finish)
                    End If
                Case 2
                    Return
            End Select
            LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            FrmMain.PageBack()
        Catch ex As OperationCanceledException
            Log(ex, "删除实例 " & PageInstanceLeft.Instance.Name & " 被主动取消")
        Catch ex As Exception
            Log(ex, "删除实例 " & PageInstanceLeft.Instance.Name & " 失败", LogLevel.Msgbox)
        End Try
    End Sub

    '修补核心
    Private Sub BtnManagePatch_Click(sender As Object, e As EventArgs) Handles BtnManagePatch.Click
        Select Case MyMsgBox($"你确定要对 {PageInstanceLeft.Instance.Name} 的核心文件进行修补吗？ {vbCrLf}修补游戏核心可能导致游戏崩溃等问题。{vbCrLf}在修补核心后，文件校验会自动关闭。", Title:="修补提示", Button2:="取消")
            Case 1
                Dim UserInput As String = SystemDialogs.SelectFile("压缩文件(*.jar;*.zip)|*.jar;*.zip", "选择用于修补核心的文件")
                If UserInput Is Nothing Or String.IsNullOrWhiteSpace(UserInput) Then Return
                Hint("正在修补游戏核心，这可能需要一段时间")
                RunInNewThread(
                    Sub()
                        Dim Core As New GameCore(PageInstanceLeft.Instance.Path & PageInstanceLeft.Instance.Name & ".jar")
                        Core.AddToCore(UserInput)
                        Hint("修补游戏核心成功", HintType.Finish)
                        Setup.Set(“VersionAdvanceAssetsV2", True, instance:=PageInstanceLeft.Instance)
                    End Sub)
            Case 2
                Return
        End Select
    End Sub

#End Region

End Class

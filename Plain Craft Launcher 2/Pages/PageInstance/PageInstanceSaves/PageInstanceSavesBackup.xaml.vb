Imports PCL.Core.Utils.FileVersionControl
Class PageInstanceSavesBackup
    Implements IRefreshable

    Private Sub IRefreshable_Refresh() Implements IRefreshable.Refresh
        Refresh()
    End Sub
    Public Sub Refresh()
        RefreshList()
    End Sub

    Private _loaded As Boolean
    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()

        RefreshList()

        _loaded = True
        If _loaded Then Return

    End Sub

    Private Sub RefreshList()
        Try
            PanList.Children.Clear()
            Dim versions As List(Of VersionData)
            Using snap As New SnapLiteVersionControl(PageInstanceSavesLeft.CurrentSave)
                versions = snap.GetVersions()
                If versions.Any() Then
                    PanDisplay.Visibility = Visibility.Visible
                    PanEmpty.Visibility = Visibility.Collapsed
                Else
                    PanDisplay.Visibility = Visibility.Collapsed
                    PanEmpty.Visibility = Visibility.Visible
                End If
            End Using
            If versions Is Nothing OrElse Not versions.Any() Then Return
            For Each item In versions
                Dim newItem As New MyListItem With {
                    .Title = item.Name,
                    .Info = item.Desc,
                    .Tags = {item.Created.ToString()}.ToList()
                }

                Dim btnApply As New MyIconButton With {
                    .Logo = Logo.IconPlayGame,
                    .ToolTip = "回到到此快照"
                }

                AddHandler btnApply.Click, Sub()
                                               Try
                                                   If MyMsgBox("确定要应用此备份吗？请确保当前的存档已完成备份或者十分确定不再使用！", Button1:="确定", Button2:="取消") = 2 Then Return
                                                   Hint("应用快照中，请勿执行其他操作！")
                                                   Dim loaders As New List(Of LoaderBase)
                                                   loaders.Add(New LoaderTask(Of Integer, Integer)("搜寻并应用文件", Sub(load As LoaderTask(Of Integer, Integer))
                                                                                                                  load.Progress = 0.2
                                                                                                                  Using snap As New SnapLiteVersionControl(PageInstanceSavesLeft.CurrentSave)
                                                                                                                      snap.ApplyPastVersion(item.NodeId).GetAwaiter().GetResult()
                                                                                                                  End Using
                                                                                                                  load.Progress = 1
                                                                                                              End Sub))
                                                   Dim loader As New LoaderCombo(Of Integer)($"{item.Name} - 备份应用", loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
                                                   loader.Start(1)
                                                   LoaderTaskbarAdd(loader)
                                                   FrmMain.BtnExtraDownload.ShowRefresh()
                                                   FrmMain.BtnExtraDownload.Ribble()
                                               Catch ex As Exception
                                                   Log(ex, "应用快照过程中出现错误", LogLevel.Msgbox)
                                               End Try
                                           End Sub

                Dim btnExport As New MyIconButton With {
                    .Logo = Logo.IconButtonSave,
                    .ToolTip = "导出到压缩包"
                }

                AddHandler btnExport.Click, Sub()
                                                Try
                                                    Dim savePath = SelectSaveFile(
                                                    "选择保存备份导出的位置",
                                                    $"{item.Name}.zip",
                                                    "压缩文件(*.zip)|*.zip",
                                                    Path)
                                                    If String.IsNullOrEmpty(savePath) Then Return
                                                    Hint("快照导出中，请勿执行其他操作！")
                                                    Dim loaders As New List(Of LoaderBase)
                                                    loaders.Add(New LoaderTask(Of Integer, Integer)("制作压缩包", Sub(load As LoaderTask(Of Integer, Integer))
                                                                                                                 load.Progress = 0.2
                                                                                                                 Using snap As New SnapLiteVersionControl(PageInstanceSavesLeft.CurrentSave)
                                                                                                                     snap.Export(item.NodeId, savePath).GetAwaiter().GetResult()
                                                                                                                 End Using
                                                                                                                 load.Progress = 1
                                                                                                             End Sub))
                                                    Dim loader As New LoaderCombo(Of Integer)($"{item.Name} - 导出备份", loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
                                                    loader.Start(1)
                                                    LoaderTaskbarAdd(loader)
                                                    FrmMain.BtnExtraDownload.ShowRefresh()
                                                    FrmMain.BtnExtraDownload.Ribble()
                                                Catch ex As Exception
                                                    Log(ex, "备份导出过程中出现错误", LogLevel.Msgbox)
                                                End Try
                                            End Sub

                Dim btnDelete As New MyIconButton With {
                    .Logo = Logo.IconButtonDelete,
                    .ToolTip = "删除"
                }

                AddHandler btnDelete.Click, Sub()
                                                Try
                                                    If MyMsgBox($"你确定要删除备份 {item.Name} 吗？{vbCrLf}描述：{item.Desc}{vbCrLf}创建时间：{item.Created}", "删除确认", "确认", "取消") = 2 Then Return
                                                    Using snap As New SnapLiteVersionControl(PageInstanceSavesLeft.CurrentSave)
                                                        snap.DeleteVersion(item.NodeId)
                                                    End Using
                                                    RefreshList()
                                                    Hint("已删除！", HintType.Finish)
                                                Catch ex As Exception
                                                    Log(ex, $"执行删除任务失败")
                                                End Try
                                            End Sub

                newItem.Buttons = {btnDelete, btnExport, btnApply}

                PanList.Children.Add(newItem)
            Next
        Catch ex As Exception
            Log(ex, "获取备份信息失败", LogLevel.Msgbox)
        End Try
    End Sub

    Private Sub BtnCreate_Click() Handles BtnCreate.Click
        Try
            Dim input = MyMsgBoxInput(
                "请输入名称",
                DefaultInput:=$"{DateTime.Now:yyyy/dd/MM-HH:mm:ss}")
            If input Is Nothing Then Return
            If String.IsNullOrWhiteSpace(input) Then input = Nothing
            If MyMsgBox("备份功能不具备热备份功能，请确你没有在使用存档内的任何文件！", "请注意！", "继续", "返回") = 2 Then Return
            BtnCreate.IsEnabled = False
            Hint("开始备份任务，请勿执行其他操作！")
            Dim loaders As New List(Of LoaderBase)
            loaders.Add(New LoaderTask(Of Integer, Integer)("搜寻并制作备份", Sub(load As LoaderTask(Of Integer, Integer))
                                                                           load.Progress = 0.2
                                                                           Using snap As New SnapLiteVersionControl(PageInstanceSavesLeft.CurrentSave)
                                                                               snap.CreateNewVersion(input).GetAwaiter().GetResult()
                                                                           End Using
                                                                           load.Progress = 1
                                                                           RunInUi(Sub() RefreshList())
                                                                       End Sub))
            Dim loader As New LoaderCombo(Of Integer)($"{input} - 制作备份", loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            loader.Start(1)
            LoaderTaskbarAdd(loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
            BtnCreate.IsEnabled = True
        Catch ex As Exception
            Log(ex, $"备份过程中出现错误", LogLevel.Msgbox)
        End Try
    End Sub

    Private Sub BtnClean_Click() Handles BtnClean.Click
        If MyMsgBox("此功能可以清理备份文件中已不再需要的文件，建议在发生备份删除后使用。", "确定使用吗？", "确定", "返回") = 2 Then Return
        Dim loaders As New List(Of LoaderBase)
        loaders.Add(New LoaderTask(Of Integer, Integer)("寻找并清理备份文件", Sub(load As LoaderTask(Of Integer, Integer))
                                                                         load.Progress = 0.2
                                                                         Using snap As New SnapLiteVersionControl(PageInstanceSavesLeft.CurrentSave)
                                                                             snap.CleanUnrecordObjects().GetAwaiter().GetResult()
                                                                         End Using
                                                                         load.Progress = 1
                                                                     End Sub))
        Dim loader As New LoaderCombo(Of Integer)($"{GetFolderNameFromPath(PageInstanceSavesLeft.CurrentSave)} - 备份清理", loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
        loader.Start(1)
        LoaderTaskbarAdd(loader)
        FrmMain.BtnExtraDownload.ShowRefresh()
        FrmMain.BtnExtraDownload.Ribble()
    End Sub

End Class

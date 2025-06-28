Imports PCL.Core.Utils.FileVersionControl
Class PageVersionSavesBackup
    Implements IRefreshable

    Public Shared SnapInstance As New Dictionary(Of String, SnapLiteVersionControl)
    Private _currentInstance As SnapLiteVersionControl

    Private Sub IRefreshable_Refresh() Implements IRefreshable.Refresh
        Refresh()
    End Sub
    Public Sub Refresh()
        RefreshList()
    End Sub

    Private _loaded As Boolean
    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()

        Dim curPath = PageVersionSavesLeft.CurrentSave

        If Not SnapInstance.ContainsKey(curPath) Then
            SnapInstance.Add(curPath, New SnapLiteVersionControl(curPath))
        End If
        _currentInstance = SnapInstance(curPath)

        RefreshList()

        _loaded = True
        If _loaded Then Return

    End Sub

    Private Sub RefreshList()
        PanList.Children.Clear()
        Dim versions = _currentInstance.GetVersions()
        If versions.Any() Then
            PanDisplay.Visibility = Visibility.Visible
            PanEmpty.Visibility = Visibility.Collapsed
        Else
            PanDisplay.Visibility = Visibility.Collapsed
            PanEmpty.Visibility = Visibility.Visible
        End If
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
                                                                                                              _currentInstance.ApplyPastVersion(item.NodeId).GetAwaiter().GetResult()
                                                                                                              load.Progress = 1
                                                                                                              Hint("快照应用已完成", HintType.Finish)
                                                                                                          End Sub))
                                               Dim loader As New LoaderCombo(Of Integer)($"{item.Name} - 备份应用", loaders)
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
                                                                                                             _currentInstance.Export(item.NodeId, savePath).GetAwaiter().GetResult()
                                                                                                             load.Progress = 1
                                                                                                             Hint("快照导出已完成", HintType.Finish)
                                                                                                         End Sub))
                                                Dim loader As New LoaderCombo(Of Integer)($"{item.Name} - 导出备份", loaders)
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
                                                _currentInstance.DeleteVersion(item.NodeId)
                                                RefreshList()
                                                Hint("已删除！", HintType.Finish)
                                            Catch ex As Exception
                                                Log(ex, $"执行删除任务失败")
                                            End Try
                                        End Sub

            newItem.Buttons = {btnDelete, btnExport, btnApply}

            PanList.Children.Add(newItem)
        Next
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
                                                                           _currentInstance.CreateNewVersion(input).GetAwaiter().GetResult()
                                                                           Hint("备份已完成", HintType.Finish)
                                                                           load.Progress = 1
                                                                           RunInUi(Sub() RefreshList())
                                                                       End Sub))
            Dim loader As New LoaderCombo(Of Integer)($"{input} - 导出备份", loaders)
            loader.Start(1)
            LoaderTaskbarAdd(loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
            BtnCreate.IsEnabled = True
        Catch ex As Exception
            Log(ex, $"备份过程中出现错误", LogLevel.Msgbox)
        End Try
    End Sub

End Class

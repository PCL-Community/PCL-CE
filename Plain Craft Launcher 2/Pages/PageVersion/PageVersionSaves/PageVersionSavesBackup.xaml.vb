Imports PCL.Core.Utils.FileVersionControl
Class PageVersionSavesBackup

    Private _instance As New Dictionary(Of String, SnapLiteVersionControl)
    Private _currentInstance As SnapLiteVersionControl

    Private _loaded As Boolean
    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()

        Dim curPath = PageVersionSavesLeft.CurrentSave

        If Not _instance.ContainsKey(curPath) Then
            _instance.Add(curPath, New SnapLiteVersionControl(curPath))
        End If
        _currentInstance = _instance(curPath)

        RefreshList()

        _loaded = True
        If _loaded Then Return

    End Sub

    Private Sub RefreshList()
        PanList.Children.Clear()
        For Each item In _currentInstance.GetVersions()
            Dim newItem As New MyListItem With {
                .Title = item.Name,
                .Info = item.Desc,
                .Tags = {item.Created.ToString()}.ToList()
            }

            Dim btnApply As New MyIconButton With {
                .Logo = Logo.IconPlay,
                .ToolTip = "回到到此快照"
            }

            AddHandler btnApply.Click, Async Sub()
                                           Try
                                               Hint("应用快照中……请不要离开此页面")
                                               Await _currentInstance.ApplyPastVersion(item.NodeId)
                                               Hint("快照应用已完成", HintType.Finish)
                                           Catch ex As Exception
                                               Log(ex, "应用快照过程中出现错误", LogLevel.Msgbox)
                                           End Try
                                       End Sub

            Dim btnExport As New MyIconButton With {
                .Logo = Logo.IconButtonSave,
                .ToolTip = "导出到压缩包"
            }

            AddHandler btnExport.Click, Async Sub()
                                            Try
                                                Dim savePath = SelectSaveFile(
                                                "选择保存备份导出的位置",
                                                $"{item.Name}.zip",
                                                "压缩文件(*.zip)|*.zip",
                                                Path)
                                                If String.IsNullOrEmpty(savePath) Then Return
                                                Hint("快照导出中……请不要离开此页面")
                                                Await _currentInstance.Export(item.NodeId, savePath)
                                                Hint("快照导出已完成", HintType.Finish)
                                            Catch ex As Exception
                                                Log(ex, "备份导出过程中出现错误", LogLevel.Msgbox)
                                            End Try
                                        End Sub

            newItem.Buttons = {btnApply, btnExport}

            PanList.Children.Add(newItem)
        Next
    End Sub

    Private Async Sub BtnCreate_Click() Handles BtnCreate.Click
        Try
            BtnCreate.IsEnabled = False
            Hint("开始备份任务，请不要退出此页面……")
            Await _currentInstance.CreateNewVersion()
            Hint("备份已完成", HintType.Finish)
            RefreshList()
            BtnCreate.IsEnabled = True
        Catch ex As Exception
            Log(ex, $"备份过程中出现错误", LogLevel.Msgbox)
        End Try
    End Sub

End Class

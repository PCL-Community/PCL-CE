Imports PCL.Core.Minecraft.Saves.Services
Imports System.IO
Imports fNbt
Imports PCL.Core.Minecraft.Saves
Imports PCL.Core.Minecraft.Saves.Writers

Class PageInstanceSavesInfo
    Implements IRefreshable

    Private ReadOnly _service As New LevelDataService()
    Private _loadResult As LevelDataLoadResult
    Private _currentWriter As ILevelDataWriter
    Private _saveDatPath As String
    Private _loaded As Boolean

    Private Sub IRefreshable_Refresh() Implements IRefreshable.Refresh
        Refresh()
    End Sub

    Public Sub Refresh()
        If _loaded Then RefreshInfo()
    End Sub

    Private Async Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        _loaded = True
        Await RefreshInfo()
    End Sub

    Private Async Function RefreshInfo() As Task
        Try
            _saveDatPath = IO.Path.Combine(PageInstanceSavesLeft.CurrentSave, "level.dat")
            If Not File.Exists(_saveDatPath) Then
                PanContent.Visibility = Visibility.Collapsed
                Log("未找到 level.dat 文件，可能存档已损坏", LogLevel.Hint)
                Return
            End If

            _loadResult = Await _service.LoadAsync(_saveDatPath)
            If _loadResult Is Nothing Then Throw New Exception("无法解析存档数据")

            _currentWriter = _service.GetWriter(_loadResult.NbtFile)
            ClearUI()
            UpdateUI()
            PanContent.Visibility = Visibility.Visible

        Catch ex As Exception
            Log(ex, "获取存档信息失败", LogLevel.Msgbox)
            PanContent.Visibility = Visibility.Collapsed
            PanSettings.Visibility = Visibility.Collapsed
            HideAllHints()
        End Try
    End Function

    Private Sub HideAllHints()
        Hintversion1_9.Visibility = Visibility.Collapsed
        Hintversion1_8.Visibility = Visibility.Collapsed
        Hintversion1_3.Visibility = Visibility.Collapsed
    End Sub

    Private Sub ClearUI()
        PanList.Children.Clear()
        PanList.RowDefinitions.Clear()
        PanSettingsList.Children.Clear()
        PanSettingsList.RowDefinitions.Clear()
        HideAllHints()
        PanSettings.Visibility = Visibility.Collapsed
    End Sub

    Private Sub UpdateUI()
        Dim info = _loadResult.Info

        ' 版本提示
        Dim hintMsg As String = Nothing
        If Not info.HasDataVersion Then
            If info.HasDifficulty Then
                hintMsg = "1.9 以下的版本无法获取存档版本"
                Hintversion1_9.Text = hintMsg
                Hintversion1_9.Visibility = Visibility.Visible
            ElseIf info.HasAllowCommands Then
                hintMsg = "1.8 以下的版本无法获取存档版本和游戏难度"
                Hintversion1_8.Text = hintMsg
                Hintversion1_8.Visibility = Visibility.Visible
            Else
                hintMsg = "1.3 以下的版本无法获取存档版本、游戏难度和是否允许作弊"
                Hintversion1_3.Text = hintMsg
                Hintversion1_3.Visibility = Visibility.Visible
            End If
        End If

        ' 数据包按钮 (1.13+)
        FrmInstanceSavesLeft.ItemDatapack.Visibility = If(info.DataVersion.HasValue AndAlso info.DataVersion.Value >= 1444, Visibility.Visible, Visibility.Collapsed)

        ' 基本信息
        AddInfoRow("存档名称", info.LevelName)
        If info.VersionName IsNot Nothing Then
            If info.VersionId.HasValue Then
                AddInfoRow("存档版本", $"{info.VersionName} ({info.VersionId.Value})")
            Else
                AddInfoRow("存档版本", info.VersionName)
            End If
        End If
        AddInfoRow("种子", info.Seed, True, info.VersionName)

        ' 设置控件
        If info.HasAllowCommands Then AddAllowCommandsControl()
        If info.HasDifficulty Then AddDifficultyControl()
        If PanSettingsList.Children.Count > 0 Then PanSettings.Visibility = Visibility.Visible

        ' 其他信息
        AddInfoRow("最后一次游玩", info.LastPlayed.ToString("yyyy-MM-dd HH:mm:ss"))
        AddInfoRow("出生点 (X/Y/Z)", info.SpawnPoint)
        AddInfoRow("游戏模式", info.GameType)
        AddInfoRow("游戏时长", SavesPlayTime.FormatPlayTime(info.PlayTime))
    End Sub

    Private Sub AddAllowCommandsControl()
        Dim combo = New MyComboBox() With {
            .Width = 100,
            .HorizontalAlignment = HorizontalAlignment.Left,
            .ToolTip = "修改设置前请确保该存档未在游戏中打开，否则会导致设置无效"
        }
        combo.Items.Add(New With {.Value = 0, .Display = "不允许"})
        combo.Items.Add(New With {.Value = 1, .Display = "允许"})
        combo.SelectedValuePath = "Value"
        combo.DisplayMemberPath = "Display"
        combo.SelectedValue = _loadResult.Info.AllowCommands

        AddHandler combo.SelectionChanged,
            Async Sub(s, e)
                Await SaveSettingAsync(
                    Sub()
                        Dim gameLevel = _loadResult.NbtFile.RootTag.Get(Of NbtCompound)("Data")
                        _currentWriter.ModifyAllowCommands(gameLevel, CInt(combo.SelectedValue))
                    End Sub,
                    "作弊设置")
            End Sub

        AddSettingRow("是否允许作弊", combo)
    End Sub

    Private Sub AddDifficultyControl()
        Dim info = _loadResult.Info
        Dim combo = New MyComboBox() With {
            .Width = 100,
            .HorizontalAlignment = HorizontalAlignment.Left,
            .ToolTip = "修改设置前请确保该存档未在游戏中打开，否则会导致设置无效"
        }
        combo.Items.Add(New With {.Value = 0, .Display = "和平"})
        combo.Items.Add(New With {.Value = 1, .Display = "简单"})
        combo.Items.Add(New With {.Value = 2, .Display = "普通"})
        combo.Items.Add(New With {.Value = 3, .Display = "困难"})
        combo.SelectedValuePath = "Value"
        combo.DisplayMemberPath = "Display"

        Dim currentDifficulty = info.DifficultyDisplay
        If currentDifficulty = "和平" Then
            combo.SelectedValue = 0
        ElseIf currentDifficulty = "简单" Then
            combo.SelectedValue = 1
        ElseIf currentDifficulty = "普通" Then
            combo.SelectedValue = 2
        ElseIf currentDifficulty = "困难" Then
            combo.SelectedValue = 3
        End If

        Dim lockBox As New MyCheckBox() With {
            .Text = "锁定难度",
            .ToolTip = "锁定后无法在游戏中更改游戏难度",
            .VerticalAlignment = VerticalAlignment.Center,
            .Margin = New Thickness(10, 0, 0, 0)
        }
        If info.IsHardcore Then
            lockBox.Visibility = Visibility.Collapsed
        Else
            lockBox.Checked = info.IsDifficultyLocked
        End If

        Dim panel As New StackPanel() With {
            .Orientation = Orientation.Horizontal,
            .HorizontalAlignment = HorizontalAlignment.Left
        }
        panel.Children.Add(combo)
        panel.Children.Add(lockBox)

        AddHandler combo.SelectionChanged,
            Async Sub(s, e)
                If combo.SelectedValue Is Nothing Then Return
                Await SaveDifficultyAsync(combo, lockBox)
            End Sub

        AddHandler lockBox.Change,
            Async Sub(sender, user)
                If combo.SelectedValue Is Nothing Then Return
                Await SaveDifficultyAsync(combo, lockBox)
            End Sub

        AddSettingRow("游戏难度", panel)
    End Sub

    Private Async Function SaveDifficultyAsync(combo As MyComboBox, lockBox As MyCheckBox) As Task
        Try
            Dim newDifficulty As Integer = CInt(combo.SelectedValue)
            Dim newLocked As Boolean = (lockBox.Visibility = Visibility.Visible AndAlso lockBox.Checked)
            Dim gameLevel = _loadResult.NbtFile.RootTag.Get(Of NbtCompound)("Data")

            _currentWriter.ModifyDifficulty(gameLevel, newDifficulty, newLocked)
            Dim success = Await _service.SaveAsync(_saveDatPath, _loadResult.NbtFile)

            If Not success Then
                Hint("难度设置修改失败", HintType.Critical)
                Return
            End If

            ' 更新本地缓存
            Select Case newDifficulty
                Case 0 : _loadResult.Info.DifficultyDisplay = "和平"
                Case 1 : _loadResult.Info.DifficultyDisplay = "简单"
                Case 2 : _loadResult.Info.DifficultyDisplay = "普通"
                Case 3 : _loadResult.Info.DifficultyDisplay = "困难"
            End Select
            _loadResult.Info.IsDifficultyLocked = newLocked

            Hint("难度设置修改成功", HintType.Finish)
        Catch ex As Exception
            Log(ex, "难度设置修改失败", LogLevel.Hint)
            Hint("难度设置修改失败：" & ex.Message, HintType.Critical)
        End Try
    End Function

    Private Async Function SaveSettingAsync(modifyAction As Action, settingName As String) As Task
        Try
            modifyAction()
            If Await _service.SaveAsync(_saveDatPath, _loadResult.NbtFile) Then
                Hint($"{settingName}修改成功", HintType.Finish)
            Else
                Hint($"{settingName}修改失败", HintType.Critical)
            End If
        Catch ex As Exception
            Log(ex, $"{settingName}修改失败", LogLevel.Hint)
            Hint($"{settingName}修改失败：{ex.Message}", HintType.Critical)
        End Try
    End Function

    Private Sub AddSettingRow(headText As String, control As UIElement)
        Dim idx = PanSettingsList.RowDefinitions.Count
        PanSettingsList.RowDefinitions.Add(New RowDefinition() With {.Height = GridLength.Auto})

        Dim head = New TextBlock() With {.Text = headText, .Margin = New Thickness(0, 3, 0, 3)}
        Grid.SetRow(head, idx)
        Grid.SetColumn(head, 0)

        Grid.SetRow(control, idx)
        Grid.SetColumn(control, 2)

        PanSettingsList.Children.Add(head)
        PanSettingsList.Children.Add(control)
        PanSettingsList.RowDefinitions.Add(New RowDefinition() With {.Height = New GridLength(8)})
    End Sub

    Private Sub AddInfoRow(head As String, content As String, Optional isSeed As Boolean = False, Optional versionName As String = Nothing)
        Dim headBlock = New TextBlock() With {.Text = head, .Margin = New Thickness(0, 3, 0, 3)}
        Dim panel = New StackPanel() With {.Orientation = Orientation.Horizontal}

        If isSeed AndAlso content <> "获取失败" Then
            Dim btn = New MyTextButton() With {.Text = content, .Margin = New Thickness(0, 3, 0, 3)}
            AddHandler btn.Click,
                Sub()
                    Try
                        ClipboardSet(content)
                        Hint("已复制到剪贴板", HintType.Finish)
                    Catch ex As Exception
                        Log(ex, "复制失败", LogLevel.Hint)
                        Hint($"复制失败：{ex.Message}", HintType.Critical)
                    End Try
                End Sub
            panel.Children.Add(btn)
            AddChunkbaseButton(panel, content, versionName)
        Else
            panel.Children.Add(New TextBlock() With {.Text = content, .Margin = New Thickness(0, 3, 0, 3)})
        End If

        Dim idx = PanList.RowDefinitions.Count
        PanList.RowDefinitions.Add(New RowDefinition() With {.Height = GridLength.Auto})
        Grid.SetRow(headBlock, idx)
        Grid.SetColumn(headBlock, 0)
        Grid.SetRow(panel, idx)
        Grid.SetColumn(panel, 2)
        PanList.Children.Add(headBlock)
        PanList.Children.Add(panel)
    End Sub

    Private Sub AddChunkbaseButton(parent As StackPanel, seed As String, versionName As String)
        Dim btn = New MyIconButton() With {
            .Logo = Logo.IconButtonlink,
            .ToolTip = "跳转到 Chunkbase 查看地图",
            .Width = 22,
            .Height = 22,
            .Margin = New Thickness(5, 0, 0, 0)
        }
        AddHandler btn.Click,
            Sub()
                Dim url = ChunkbaseHelper.BuildUrl(seed, versionName)
                If url Is Nothing Then
                    If versionName Is Nothing Then
                        Log("无法确定存档版本", LogLevel.Hint)
                    Else
                        Log($"当前存档版本 '{versionName}' 可能是预览版，Chunkbase 不支持查看此类版本的地图", LogLevel.Hint)
                    End If
                    Return
                End If
                OpenWebsite(url)
            End Sub
        parent.Children.Add(btn)
    End Sub

End Class
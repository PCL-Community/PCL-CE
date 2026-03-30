Imports PCL.Core.Minecraft.Saves.Services
Imports System.IO
Imports fNbt
Imports PCL.Core.Minecraft.Saves
Imports PCL.Core.Minecraft.Saves.Writers

Class PageInstanceSavesInfo
    Implements IRefreshable

    Private _service As New LevelDataService()
    Private _loadResult As LevelDataLoadResult
    Private _currentWriter As ILevelDataWriter
    Private _saveDatPath As String

    Private Sub IRefreshable_Refresh() Implements IRefreshable.Refresh
        Refresh()
    End Sub

    Public Sub Refresh()
        RefreshInfo()
    End Sub

    Private _loaded As Boolean

    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        RefreshInfo()
        _loaded = True
    End Sub

    Private Async Sub RefreshInfo()
        Try
            _saveDatPath = IO.Path.Combine(PageInstanceSavesLeft.CurrentSave, "level.dat")

            If Not File.Exists(_saveDatPath) Then
                Log("未找到 level.dat 文件，可能存档已损坏", LogLevel.Hint)
                PanContent.Visibility = Visibility.Collapsed
                Return
            End If

            _loadResult = Await _service.LoadAsync(_saveDatPath)
            If _loadResult Is Nothing Then
                Throw New Exception("无法解析存档数据")
            End If

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
    End Sub

    Private Sub HideAllHints()
        Hintversion1_9.Visibility = Visibility.Collapsed
        Hintversion1_8.Visibility = Visibility.Collapsed
        Hintversion1_3.Visibility = Visibility.Collapsed
    End Sub

    Private Sub ClearUI()
        ClearInfoTable()
        PanSettingsList.Children.Clear()
        PanSettingsList.RowDefinitions.Clear()
        HideAllHints()
        PanSettings.Visibility = Visibility.Collapsed
    End Sub

    Private Sub UpdateUI()
        Dim info = _loadResult.Info

        ' 显示版本提示
        Dim versionHint = GetVersionHint(info.HasDataVersion, info.HasDifficulty, info.HasAllowCommands)
        If versionHint IsNot Nothing Then
            If info.HasDifficulty Then
                Hintversion1_9.Visibility = Visibility.Visible
                Hintversion1_9.Text = versionHint
            ElseIf info.HasAllowCommands Then
                Hintversion1_8.Visibility = Visibility.Visible
                Hintversion1_8.Text = versionHint
            Else
                Hintversion1_3.Visibility = Visibility.Visible
                Hintversion1_3.Text = versionHint
            End If
        End If

        ' 显示数据包按钮（1.13+）
        FrmInstanceSavesLeft.ItemDatapack.Visibility = If(ShouldShowDataPack(info.DataVersion), Visibility.Visible, Visibility.Collapsed)

        ' 基本信息
        AddInfoTable("存档名称", info.LevelName)

        If info.VersionName IsNot Nothing AndAlso info.VersionId.HasValue Then
            AddInfoTable("存档版本", $"{info.VersionName} ({info.VersionId.Value})")
        ElseIf info.VersionName IsNot Nothing Then
            AddInfoTable("存档版本", info.VersionName)
        End If

        AddInfoTable("种子", info.Seed, isSeed:=True, versionName:=info.VersionName, allowCopy:=True)

        ' 设置控件
        AddSettingsControls()

        ' 其他信息
        AddInfoTable("最后一次游玩", info.LastPlayed.ToString("yyyy-MM-dd HH:mm:ss"))
        AddInfoTable("出生点 (X/Y/Z)", info.SpawnPoint)
        AddInfoTable("游戏模式", info.GameType)

        AddInfoTable("游戏时长", SavesPlayTime.FormatPlayTime(info.PlayTime))
    End Sub

    Private Function GetVersionHint(hasDataVersion As Boolean, hasDifficulty As Boolean, hasAllowCommands As Boolean) As String
        If hasDataVersion Then Return Nothing
        If hasDifficulty Then Return "1.9 以下的版本无法获取存档版本"
        If hasAllowCommands Then Return "1.8 以下的版本无法获取存档版本和游戏难度"
        Return "1.3 以下的版本无法获取存档版本、游戏难度和是否允许作弊"
    End Function

    Private Function ShouldShowDataPack(dataVersion As Integer?) As Boolean
        Const DATA_VERSION_1_13 = 1444
        Return dataVersion.HasValue AndAlso dataVersion.Value >= DATA_VERSION_1_13
    End Function

    Private Sub AddSettingsControls()
        Dim info = _loadResult.Info

        If info.HasAllowCommands Then
            AddAllowCommandsControl()
        End If

        If info.HasDifficulty Then
            AddDifficultyControl()
        End If
    End Sub

    Private Sub AddAllowCommandsControl()
        PanSettings.Visibility = Visibility.Visible
        Dim info = _loadResult.Info

        Dim combo As New MyComboBox() With {
            .Width = 100,
            .HorizontalAlignment = HorizontalAlignment.Left,
            .ToolTip = "修改设置前请确保该存档未在游戏中打开，否则会导致设置无效"
        }

        combo.Items.Add(New With {.Value = 0, .Display = "不允许"})
        combo.Items.Add(New With {.Value = 1, .Display = "允许"})
        combo.SelectedValuePath = "Value"
        combo.DisplayMemberPath = "Display"
        combo.SelectedValue = info.AllowCommands

        AddHandler combo.SelectionChanged, Async Sub(s, e)
            Try
                Dim newVal As Integer = CInt(combo.SelectedValue)
                Dim gameLevel = _loadResult.NbtFile.RootTag.Get(Of NbtCompound)("Data")
                _currentWriter.ModifyAllowCommands(gameLevel, newVal)
                Dim success = Await _service.SaveAsync(_saveDatPath, _loadResult.NbtFile)

                If success Then
                    info.AllowCommands = newVal
                    Hint("作弊设置修改成功", HintType.Finish)
                Else
                    Hint("作弊设置修改失败", HintType.Critical)
                End If
            Catch ex As Exception
                Log(ex, "作弊设置修改失败", LogLevel.Hint)
                Hint("作弊设置修改失败：" & ex.Message, HintType.Critical)
            End Try
        End Sub

        AddSettingRow("是否允许作弊", combo)
    End Sub

    Private Sub AddDifficultyControl()
        PanSettings.Visibility = Visibility.Visible
        Dim info = _loadResult.Info

        Dim difficultyCombo As New MyComboBox() With {
            .Width = 100,
            .HorizontalAlignment = HorizontalAlignment.Left,
            .ToolTip = "修改设置前请确保该存档未在游戏中打开，否则会导致设置无效"
        }

        difficultyCombo.Items.Add(New With {.Value = 0, .Display = "和平"})
        difficultyCombo.Items.Add(New With {.Value = 1, .Display = "简单"})
        difficultyCombo.Items.Add(New With {.Value = 2, .Display = "普通"})
        difficultyCombo.Items.Add(New With {.Value = 3, .Display = "困难"})
        difficultyCombo.SelectedValuePath = "Value"
        difficultyCombo.DisplayMemberPath = "Display"

        ' 根据当前难度显示值设置选中项
        Dim currentDifficulty = info.DifficultyDisplay
        Dim selectedValue As Integer
        If currentDifficulty = "和平" Then
            selectedValue = 0
        ElseIf currentDifficulty = "简单" Then
            selectedValue = 1
        ElseIf currentDifficulty = "普通" Then
            selectedValue = 2
        ElseIf currentDifficulty = "困难" Then
            selectedValue = 3
        Else
            selectedValue = 2
        End If
        difficultyCombo.SelectedValue = selectedValue

        Dim lockCheckBox As New MyCheckBox() With {
            .Text = "锁定难度",
            .ToolTip = "锁定当前难度设置，锁定后无法在游戏中更改游戏难度",
            .VerticalAlignment = VerticalAlignment.Center,
            .Margin = New Thickness(10, 0, 0, 0)
        }

        If Not info.IsHardcore Then
            lockCheckBox.Checked = info.IsDifficultyLocked
        Else
            lockCheckBox.Visibility = Visibility.Collapsed
        End If

        Dim difficultyPanel As New StackPanel() With {
            .Orientation = Orientation.Horizontal,
            .HorizontalAlignment = HorizontalAlignment.Left
        }
        difficultyPanel.Children.Add(difficultyCombo)
        difficultyPanel.Children.Add(lockCheckBox)

        AddHandler difficultyCombo.SelectionChanged, Async Sub(s, e)
            If difficultyCombo.SelectedValue Is Nothing Then Return
            Await SaveDifficultySettingsAsync(difficultyCombo, lockCheckBox)
        End Sub

        AddHandler lockCheckBox.Change, Async Sub(sender, user)
            If difficultyCombo.SelectedValue Is Nothing Then Return
            Await SaveDifficultySettingsAsync(difficultyCombo, lockCheckBox)
        End Sub

        AddSettingRow("游戏难度", difficultyPanel)
    End Sub

    Private Async Function SaveDifficultySettingsAsync(difficultyCombo As MyComboBox, lockCheckBox As MyCheckBox) As Task
        Try
            Dim newDifficulty As Integer = CInt(difficultyCombo.SelectedValue)
            Dim newLocked As Boolean = If(lockCheckBox.Visibility = Visibility.Visible, lockCheckBox.Checked, False)
            Dim gameLevel = _loadResult.NbtFile.RootTag.Get(Of NbtCompound)("Data")

            _currentWriter.ModifyDifficulty(gameLevel, newDifficulty, newLocked)
            Dim success = Await _service.SaveAsync(_saveDatPath, _loadResult.NbtFile)

            If Not success Then
                Hint("难度设置修改失败", HintType.Critical)
                Return
            End If

            ' 更新本地缓存
            Dim newDisplayName As String
            Select Case newDifficulty
                Case 0
                    newDisplayName = "和平"
                Case 1
                    newDisplayName = "简单"
                Case 2
                    newDisplayName = "普通"
                Case 3
                    newDisplayName = "困难"
                Case Else
                    newDisplayName = "未知"
            End Select
            _loadResult.Info.DifficultyDisplay = newDisplayName
            _loadResult.Info.IsDifficultyLocked = newLocked

            Hint("难度设置修改成功", HintType.Finish)
        Catch ex As Exception
            Log(ex, "难度设置修改失败", LogLevel.Hint)
            Hint("难度设置修改失败：" & ex.Message, HintType.Critical)
        End Try
    End Function

    Private Sub AddSettingRow(headText As String, control As UIElement)
        Dim rowIndex = PanSettingsList.RowDefinitions.Count

        PanSettingsList.RowDefinitions.Add(New RowDefinition() With {.Height = New GridLength(1, GridUnitType.Auto)})

        Dim headTextBlock As New TextBlock With {.Text = headText, .Margin = New Thickness(0, 3, 0, 3)}
        Grid.SetRow(headTextBlock, rowIndex)
        Grid.SetColumn(headTextBlock, 0)

        Grid.SetRow(control, rowIndex)
        Grid.SetColumn(control, 2)

        PanSettingsList.Children.Add(headTextBlock)
        PanSettingsList.Children.Add(control)
        PanSettingsList.RowDefinitions.Add(New RowDefinition() With {.Height = New GridLength(8, GridUnitType.Pixel)})
    End Sub

    Private Sub ClearInfoTable()
        PanList.Children.Clear()
        PanList.RowDefinitions.Clear()
    End Sub

    Private Sub AddInfoTable(head As String, content As String, Optional isSeed As Boolean = False, Optional versionName As String = Nothing, Optional allowCopy As Boolean = False)
        Dim headTextBlock As New TextBlock With {.Text = head, .Margin = New Thickness(0, 3, 0, 3)}
        Dim contentStack As New StackPanel With {.Orientation = Orientation.Horizontal}
        Dim contentTextBlock As UIElement

        If allowCopy Then
            Dim copyBtn As New MyTextButton With {.Text = content, .Margin = New Thickness(0, 3, 0, 3)}
            contentTextBlock = copyBtn
            AddHandler copyBtn.Click, Sub()
                Try
                    ClipboardSet(content)
                    Hint("已复制到剪贴板", HintType.Finish)
                Catch ex As Exception
                    Log(ex, "复制到剪贴板失败", LogLevel.Hint)
                    Hint("复制失败：" & ex.Message, HintType.Critical)
                End Try
            End Sub
        Else
            contentTextBlock = New TextBlock With {.Text = content, .Margin = New Thickness(0, 3, 0, 3)}
        End If

        contentStack.Children.Add(contentTextBlock)

        If isSeed AndAlso content <> "获取失败" AndAlso content <> "未知" Then
            AddChunkbaseButton(contentStack, content, versionName)
        End If

        AddToGrid(headTextBlock, contentStack)
    End Sub

    Private Sub AddChunkbaseButton(parentStack As StackPanel, seed As String, versionName As String)
        Dim chunkbaseBtn As New MyIconButton With {
            .Logo = Logo.IconButtonlink,
            .ToolTip = "跳转到 Chunkbase 查看地图",
            .Width = 22,
            .Height = 22,
            .Margin = New Thickness(5, 0, 0, 0)
        }

        parentStack.Children.Add(chunkbaseBtn)

        AddHandler chunkbaseBtn.Click, Sub()
            Try
                Dim url = ChunkbaseHelper.BuildUrl(seed, versionName)
                If url Is Nothing Then
                    If versionName Is Nothing Then
                        Log("当前存档版本无法确定，无法跳转到 Chunkbase", LogLevel.Hint)
                        Hint("无法确定存档版本", HintType.Critical)
                    Else
                        Log($"当前存档版本 '{versionName}' 可能是预览版，Chunkbase 不支持", LogLevel.Hint)
                        Hint($"版本 {versionName} 暂不支持", HintType.Critical)
                    End If
                    Return
                End If
                OpenWebsite(url)
            Catch ex As Exception
                Log(ex, "跳转到 Chunkbase 失败", LogLevel.Hint)
                Hint("跳转失败：" & ex.Message, HintType.Critical)
            End Try
        End Sub
    End Sub

    Private Sub AddToGrid(headTextBlock As TextBlock, contentStack As StackPanel)
        PanList.Children.Add(headTextBlock)
        PanList.Children.Add(contentStack)

        Dim targetRow = New RowDefinition
        PanList.RowDefinitions.Add(targetRow)
        Dim rowIndex = PanList.RowDefinitions.IndexOf(targetRow)

        Grid.SetRow(headTextBlock, rowIndex)
        Grid.SetColumn(headTextBlock, 0)
        Grid.SetRow(contentStack, rowIndex)
        Grid.SetColumn(contentStack, 2)
    End Sub

End Class
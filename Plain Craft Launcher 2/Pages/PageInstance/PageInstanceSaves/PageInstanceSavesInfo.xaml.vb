Imports PCL.Core.Minecraft.Saves
Imports System.IO

Class PageInstanceSavesInfo
    Implements IRefreshable
    
    Private _levelDataInfo As LevelDataInfo
    Private _levelDataWriter As LevelDataWriter
    Private _levelDataReader As LevelDataReader

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
            Dim saveDatPath = IO.Path.Combine(PageInstanceSavesLeft.CurrentSave, "level.dat")
            
            ' 检查文件是否存在
            If Not File.Exists(saveDatPath) Then
                Log("未找到 level.dat 文件，可能存档已损坏", LogLevel.Hint)
                PanContent.Visibility = Visibility.Collapsed
                Return
            End If
            
            ' 使用核心库读取数据
            _levelDataReader = New LevelDataReader(saveDatPath)
            _levelDataInfo = Await _levelDataReader.LoadAsync()
            
            If _levelDataInfo Is Nothing Then
                Throw New Exception("无法解析存档数据")
            End If
            
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
        ' 显示版本提示
        Dim versionHint = LevelDataVersion.GetVersionHint(
            _levelDataInfo.HasDataVersion,
            _levelDataInfo.HasDifficulty,
            _levelDataInfo.HasAllowCommands)
        
        If versionHint IsNot Nothing Then
            If _levelDataInfo.HasDifficulty Then
                Hintversion1_9.Visibility = Visibility.Visible
                Hintversion1_9.Text = versionHint
            ElseIf _levelDataInfo.HasAllowCommands Then
                Hintversion1_8.Visibility = Visibility.Visible
                Hintversion1_8.Text = versionHint
            Else
                Hintversion1_3.Visibility = Visibility.Visible
                Hintversion1_3.Text = versionHint
            End If
        End If
        
        ' 显示数据包按钮（1.13+）
        Dim shouldShowDataPack = LevelDataVersion.ShouldShowDataPack(_levelDataInfo.DataVersion)
        FrmInstanceSavesLeft.ItemDatapack.Visibility = If(shouldShowDataPack, Visibility.Visible, Visibility.Collapsed)
        
        ' 添加基本信息
        AddInfoTable("存档名称", _levelDataInfo.LevelName)
        
        ' 安全地显示版本信息
        If _levelDataInfo.VersionName IsNot Nothing AndAlso _levelDataInfo.VersionId.HasValue Then
            AddInfoTable("存档版本", $"{_levelDataInfo.VersionName} ({_levelDataInfo.VersionId.Value})")
        ElseIf _levelDataInfo.VersionName IsNot Nothing Then
            AddInfoTable("存档版本", _levelDataInfo.VersionName)
        End If
        
        AddInfoTable("种子", _levelDataInfo.Seed, isSeed:=True, versionName:=_levelDataInfo.VersionName, allowCopy:=True)
        
        ' 添加设置控件
        AddSettingsControls()
        
        ' 添加其他信息
        AddInfoTable("最后一次游玩", _levelDataInfo.LastPlayed.ToString("yyyy-MM-dd HH:mm:ss"))
        AddInfoTable("出生点 (X/Y/Z)", _levelDataInfo.SpawnPoint)
        AddInfoTable("游戏模式", _levelDataInfo.GameType)
        
        ' 显示难度信息（1.8以下不显示）
        If _levelDataInfo.HasDifficulty AndAlso Hintversion1_8.Visibility <> Visibility.Visible Then
            Dim lockedStatus = If(_levelDataInfo.IsDifficultyLocked OrElse _levelDataInfo.IsHardcore, "是", "否")
            AddInfoTable("困难度", $"{_levelDataInfo.DifficultyDisplayName} (是否已锁定难度：{lockedStatus})")
        End If
        
        AddInfoTable("游戏时长", FormatPlayTime(_levelDataInfo.PlayTime))
    End Sub
    
    Private Sub AddSettingsControls()
        Dim saveDatPath = IO.Path.Combine(PageInstanceSavesLeft.CurrentSave, "level.dat")
        
        ' 初始化写入器
        Dim nbtFile = _levelDataReader.GetNbtFile()
        If nbtFile Is Nothing Then
            Log("无法获取 NBT 文件对象", LogLevel.Hint)
            Return
        End If
        
        _levelDataWriter = New LevelDataWriter(saveDatPath, nbtFile)
        
        ' 命令权限设置
        If _levelDataInfo.HasAllowCommands Then
            AddAllowCommandsControl()
        End If
        
        ' 难度设置
        If _levelDataInfo.HasDifficulty Then
            AddDifficultyControl()
        End If
    End Sub
    
    Private Sub AddAllowCommandsControl()
        PanSettings.Visibility = Visibility.Visible
        
        Dim combo As New MyComboBox() With {
            .Width = 100,
            .HorizontalAlignment = HorizontalAlignment.Left,
            .ToolTip = "修改设置前请确保该存档未在游戏中打开，否则会导致设置无效"
        }
        
        combo.Items.Add(New With {.Value = 0, .Display = "不允许"})
        combo.Items.Add(New With {.Value = 1, .Display = "允许"})
        combo.SelectedValuePath = "Value"
        combo.DisplayMemberPath = "Display"
        combo.SelectedValue = _levelDataInfo.AllowCommands
        
        AddHandler combo.SelectionChanged, Async Sub(s, e)
            Try
                Dim newVal As Integer = CInt(combo.SelectedValue)
                _levelDataWriter.ModifyAllowCommands(newVal)
                Dim success = Await _levelDataWriter.SaveAsync()
                
                If success Then
                    _levelDataInfo.AllowCommands = newVal
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
        
        ' 设置当前难度值
        If _levelDataInfo.IsNewFormat AndAlso _levelDataInfo.Difficulty IsNot Nothing Then
            Select Case _levelDataInfo.Difficulty
                Case "peaceful"
                    difficultyCombo.SelectedValue = 0
                Case "easy"
                    difficultyCombo.SelectedValue = 1
                Case "normal"
                    difficultyCombo.SelectedValue = 2
                Case "hard"
                    difficultyCombo.SelectedValue = 3
                Case Else
                    difficultyCombo.SelectedValue = 2
            End Select
        Else
            difficultyCombo.SelectedValue = _levelDataInfo.DifficultyOld
        End If
        
        Dim lockCheckBox As New MyCheckBox() With {
            .Text = "锁定难度",
            .ToolTip = "锁定当前难度设置，锁定后无法在游戏中更改游戏难度",
            .VerticalAlignment = VerticalAlignment.Center,
            .Margin = New Thickness(10, 0, 0, 0)
        }
        
        If Not _levelDataInfo.IsHardcore Then
            lockCheckBox.Checked = _levelDataInfo.IsDifficultyLocked
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
            
            _levelDataWriter.ModifyDifficulty(newDifficulty, newLocked, _levelDataInfo.IsHardcore, _levelDataInfo.IsNewFormat)
            Dim success = Await _levelDataWriter.SaveAsync()
            
            If Not success Then
                Hint("难度设置修改失败", HintType.Critical)
                Return
            End If
            
            ' 更新本地缓存
            If _levelDataInfo.IsNewFormat Then
                Select Case newDifficulty
                    Case 0
                        _levelDataInfo.Difficulty = "peaceful"
                    Case 1
                        _levelDataInfo.Difficulty = "easy"
                    Case 2
                        _levelDataInfo.Difficulty = "normal"
                    Case 3
                        _levelDataInfo.Difficulty = "hard"
                End Select
            Else
                _levelDataInfo.DifficultyOld = newDifficulty
            End If
            _levelDataInfo.IsDifficultyLocked = newLocked
            
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
        
        ' 添加Chunkbase跳转按钮
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
    
    Private Function FormatPlayTime(playTime As TimeSpan) As String
        If playTime.TotalSeconds < 60 Then
            Return $"{playTime.Seconds} 秒"
        ElseIf playTime.TotalHours < 1 Then
            Return $"{playTime.Minutes} 分钟 {playTime.Seconds} 秒"
        ElseIf playTime.TotalDays < 1 Then
            Return $"{playTime.Hours} 小时 {playTime.Minutes} 分钟"
        Else
            Return $"{playTime.Days} 天 {playTime.Hours} 小时 {playTime.Minutes} 分钟"
        End If
    End Function
End Class
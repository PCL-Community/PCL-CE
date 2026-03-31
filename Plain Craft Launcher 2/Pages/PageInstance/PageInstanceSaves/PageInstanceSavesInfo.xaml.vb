Imports PCL.Core.Minecraft.Saves

Class PageInstanceSavesInfo
    Implements IRefreshable

    Private ReadOnly _viewModel As New LevelDataViewModel()
    Private _loaded As Boolean

    Private Sub IRefreshable_Refresh() Implements IRefreshable.Refresh
        Refresh()
    End Sub

    Public Async Sub Refresh()
        If _loaded Then
            Await _viewModel.LoadAsync(IO.Path.Combine(PageInstanceSavesLeft.CurrentSave, "level.dat"))
        End If
    End Sub

    Private Async Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        _loaded = True
        
        ' 订阅事件
        AddHandler _viewModel.OnDataChanged, AddressOf OnDataChanged
        
        ' 加载数据
        Await _viewModel.LoadAsync(IO.Path.Combine(PageInstanceSavesLeft.CurrentSave, "level.dat"))
    End Sub

    Private Sub OnDataChanged()
        UpdateUI()
    End Sub
    
    Private Sub UpdateUI()
        ' 设置可见性
        PanContent.Visibility = If(_viewModel.HasData, Visibility.Visible, Visibility.Collapsed)
        PanSettings.Visibility = If(_viewModel.ShowSettings, Visibility.Visible, Visibility.Collapsed)
        
        ' 版本提示
        Hintversion1_9.Visibility = Visibility.Collapsed
        Hintversion1_8.Visibility = Visibility.Collapsed
        Hintversion1_3.Visibility = Visibility.Collapsed
        
        If _viewModel.ShowVersionHint Then
            Dim hintText = _viewModel.VersionHint
            If hintText.Contains("1.9") Then
                Hintversion1_9.Text = hintText
                Hintversion1_9.Visibility = Visibility.Visible
            ElseIf hintText.Contains("1.8") Then
                Hintversion1_8.Text = hintText
                Hintversion1_8.Visibility = Visibility.Visible
            ElseIf hintText.Contains("1.3") Then
                Hintversion1_3.Text = hintText
                Hintversion1_3.Visibility = Visibility.Visible
            End If
        End If
        
        ' 数据包按钮
        FrmInstanceSavesLeft.ItemDatapack.Visibility = If(_viewModel.ShowDatapackButton, Visibility.Visible, Visibility.Collapsed)
        
        ' 清空并重建 UI
        ClearUI()
        BuildInfoRows()
        BuildSettingRows()
    End Sub

    Private Sub ClearUI()
        PanList.Children.Clear()
        PanList.RowDefinitions.Clear()
        PanSettingsList.Children.Clear()
        PanSettingsList.RowDefinitions.Clear()
    End Sub

    Private Sub BuildInfoRows()
        AddInfoRow("存档名称", _viewModel.LevelName)
        
        If Not String.IsNullOrEmpty(_viewModel.VersionDisplay) Then
            AddInfoRow("存档版本", _viewModel.VersionDisplay)
        End If
        
        AddInfoRow("种子", _viewModel.Seed, isSeed:=True)
        AddInfoRow("最后一次游玩", _viewModel.LastPlayed)
        AddInfoRow("出生点 (X/Y/Z)", _viewModel.SpawnPoint)
        AddInfoRow("游戏模式", _viewModel.GameType)
        AddInfoRow("游戏时长", _viewModel.PlayTime)
    End Sub

    Private Sub BuildSettingRows()
        If _viewModel.HasAllowCommands Then
            AddAllowCommandsControl()
        End If
        
        If _viewModel.HasDifficulty Then
            AddDifficultyControl()
        End If
    End Sub

    Private Sub AddAllowCommandsControl()
        Dim combo = New MyComboBox() With {
            .Width = 100,
            .HorizontalAlignment = HorizontalAlignment.Left,
            .ToolTip = "修改设置前请确保该存档未在游戏中打开，否则会导致设置无效"
        }
        
        combo.ItemsSource = _viewModel.AllowCommandsOptions
        combo.SelectedValuePath = "Value"
        combo.DisplayMemberPath = "Display"
        combo.SelectedValue = _viewModel.AllowCommands
        
        AddHandler combo.SelectionChanged,
            Async Sub(s, e)
                If combo.SelectedValue IsNot Nothing Then
                    Await _viewModel.SaveAllowCommandsAsync(CInt(combo.SelectedValue))
                    ' 刷新显示
                    combo.SelectedValue = _viewModel.AllowCommands
                End If
            End Sub
        
        AddSettingRow("是否允许作弊", combo)
    End Sub

    Private Sub AddDifficultyControl()
        Dim combo = New MyComboBox() With {
                .Width = 100,
                .HorizontalAlignment = HorizontalAlignment.Left,
                .ToolTip = "修改设置前请确保该存档未在游戏中打开，否则会导致设置无效"
                }
    
        combo.ItemsSource = _viewModel.DifficultyOptions
        combo.SelectedValuePath = "Display"
        combo.DisplayMemberPath = "Display"
        combo.SelectedValue = _viewModel.DifficultyDisplay
    
        Dim lockBox As New MyCheckBox() With {
                .Text = "锁定难度",
                .ToolTip = "锁定后无法在游戏中更改游戏难度",
                .VerticalAlignment = VerticalAlignment.Center,
                .Margin = New Thickness(10, 0, 0, 0),
                .Visibility = If(_viewModel.ShowDifficultyLock, Visibility.Visible, Visibility.Collapsed),
                .Checked = _viewModel.IsDifficultyLocked
                }
    
        Dim panel As New StackPanel() With {
                .Orientation = Orientation.Horizontal,
                .HorizontalAlignment = HorizontalAlignment.Left
                }
        panel.Children.Add(combo)
        panel.Children.Add(lockBox)
    
        ' 定义保存函数
        Dim saveDifficulty As Func(Of Task) = Async Function()
            Dim selectedItem = TryCast(combo.SelectedItem, ComboItem)
            If selectedItem IsNot Nothing Then
                Await _viewModel.SaveDifficultyAsync(selectedItem.Value, lockBox.Checked)
                combo.SelectedValue = _viewModel.DifficultyDisplay
                lockBox.Checked = _viewModel.IsDifficultyLocked
            End If
        End Function
    
        ' 绑定事件
        AddHandler combo.SelectionChanged, Sub(s, e) 
            Dim ignore = saveDifficulty()
        End Sub
    
        AddHandler lockBox.Change, Sub(sender, user) 
            Dim ignore = saveDifficulty()
        End Sub
    
        AddSettingRow("游戏难度", panel)
    End Sub

    Private Sub AddInfoRow(head As String, content As String, Optional isSeed As Boolean = False)
        Dim headBlock = New TextBlock() With {.Text = head, .Margin = New Thickness(0, 3, 0, 3)}
        Dim panel = New StackPanel() With {.Orientation = Orientation.Horizontal}
        
        Dim displayContent = If(String.IsNullOrEmpty(content), "获取失败", content)
        
        If isSeed AndAlso displayContent <> "获取失败" Then
            Dim btn = New MyTextButton() With {.Text = displayContent, .Margin = New Thickness(0, 3, 0, 3)}
            AddHandler btn.Click,
                Sub()
                    _viewModel.CopySeedToClipboard()
                End Sub
            panel.Children.Add(btn)
            
            If Not String.IsNullOrEmpty(_viewModel.Seed) Then
                Dim chunkbaseBtn = New MyIconButton() With {
                    .Logo = Logo.IconButtonlink,
                    .ToolTip = "跳转到 Chunkbase 查看地图",
                    .Width = 22,
                    .Height = 22,
                    .Margin = New Thickness(5, 0, 0, 0)
                }
                AddHandler chunkbaseBtn.Click,
                    Sub()
                        _viewModel.OpenChunkbase()
                    End Sub
                panel.Children.Add(chunkbaseBtn)
            End If
        Else
            panel.Children.Add(New TextBlock() With {.Text = displayContent, .Margin = New Thickness(0, 3, 0, 3)})
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
End Class
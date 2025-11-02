Imports fNbt

Class PageInstanceSavesInfo
    Implements IRefreshable

    Private levelData As XElement
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
        If _loaded Then Return

    End Sub

    Private Sub RefreshInfo()
        Try
            Dim saveDatPath = IO.Path.Combine(PageInstanceSavesLeft.CurrentSave, "level.dat")
            Using fs As New FileStream(saveDatPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                Dim saveInfo As New NbtFile()
                saveInfo.LoadFromStream(fs, NbtCompression.AutoDetect)
                ClearInfoTable()
                PanSettingsList.Children.Clear()
                PanSettingsList.RowDefinitions.Clear()

                Hintversion1_9.Visibility = Visibility.Collapsed
                Hintversion1_8.Visibility = Visibility.Collapsed
                Hintversion1_3.Visibility = Visibility.Collapsed
                PanSettings.Visibility = Visibility.Collapsed

                Dim gameLevel = saveInfo.RootTag.Get(Of NbtCompound)("Data")
                AddInfoTable("存档名称", gameLevel.Get(Of NbtString)("LevelName").Value)
                Dim versionName As NbtString = Nothing
                Dim versionId As NbtInt = Nothing
                Dim gameVersion = gameLevel.Get(Of NbtCompound)("Version")
                If gameVersion IsNot Nothing Then
                    gameVersion.TryGet(Of NbtString)("Name", versionName)
                    gameVersion.TryGet(Of NbtInt)("Id", versionId)
                End If
                Dim hasDifficulty = gameLevel.Contains("Difficulty")
                Dim hasAllowCommands = gameLevel.Contains("allowCommands")

                If versionName Is Nothing Then
                    If hasDifficulty Then
                        Hintversion1_9.Visibility = Visibility.Visible
                        Hintversion1_9.Text = $"1.9 以下的版本无法获取存档版本"
                    Else
                        If hasAllowCommands Then
                            Hintversion1_8.Visibility = Visibility.Visible
                            Hintversion1_8.Text = $"1.8 以下的版本无法获取存档版本和游戏难度"
                        Else
                            Hintversion1_3.Visibility = Visibility.Visible
                            Hintversion1_3.Text = $"1.3 以下的版本无法获取存档版本、游戏难度和是否允许作弊"
                        End If
                    End If
                Else
                    AddInfoTable("存档版本", $"{versionName.Value} ({versionId.Value})")
                End If

                Dim seedNbt As NbtLong = Nothing
                Dim seed As String
                If gameLevel.TryGet(Of NbtLong)("RandomSeed", seedNbt) Then
                    seed = seedNbt.Value().ToString()
                Else
                    seed = gameLevel.Get(Of NbtCompound)("WorldGenSettings").Get(Of NbtLong)("seed").Value.ToString()
                End If

                AddInfoTable("种子", seed, True, versionName?.Value, True)

                If hasAllowCommands Then
                    PanSettings.Visibility = Visibility.Visible
                    Dim allowCommandValue As Integer = Integer.Parse(gameLevel.Get(Of NbtByte)("allowCommands").Value)
                    Dim combo As New MyComboBox() With {.Width = 100, .HorizontalAlignment = HorizontalAlignment.Left, .ToolTip = "修改设置前请确保该存档未在游戏中打开，否则会导致设置无效"}
                    combo.Items.Add(New With {.Value = 0, .Display = "不允许"})
                    combo.Items.Add(New With {.Value = 1, .Display = "允许"})
                    combo.SelectedValuePath = "Value"
                    combo.DisplayMemberPath = "Display"
                    combo.SelectedValue = allowCommandValue

                    AddHandler combo.SelectionChanged, Sub(s, e)
                                                           Try
                                                               Dim newVal As Integer = CInt(combo.SelectedValue)
                                                               gameLevel.Get(Of NbtByte)("allowCommands").Value = CByte(newVal)
                                                               Using fileStream As New FileStream(saveDatPath, FileMode.Create, FileAccess.Write, FileShare.None)
                                                                   saveInfo.SaveToStream(fileStream, NbtCompression.GZip)
                                                               End Using
                                                               Hint("作弊设置修改成功", HintType.Finish)
                                                           Catch ex As Exception
                                                               Log(ex, "作弊设置修改失败", LogLevel.Hint)
                                                           End Try
                                                       End Sub
                    Dim rowIndex = PanSettingsList.RowDefinitions.Count
                    PanSettingsList.RowDefinitions.Add(New RowDefinition() With {.Height = New GridLength(1, GridUnitType.Auto)})

                    Dim headTextBlock As New TextBlock With {.Text = "是否允许作弊", .Margin = New Thickness(0, 3, 0, 3)}
                    Grid.SetRow(headTextBlock, rowIndex)
                    Grid.SetColumn(headTextBlock, 0)

                    Grid.SetRow(combo, rowIndex)
                    Grid.SetColumn(combo, 2)

                    PanSettingsList.Children.Add(headTextBlock)
                    PanSettingsList.Children.Add(combo)
                    PanSettingsList.RowDefinitions.Add(New RowDefinition() With {.Height = New GridLength(8, GridUnitType.Pixel)})
                End If

                If hasDifficulty Then
                    PanSettings.Visibility = Visibility.Visible
                    Dim difficultyElement = gameLevel.Get(Of NbtByte)("Difficulty")
                    Dim difficultyValue As Integer = Integer.Parse(difficultyElement.Value)

                    Dim difficultyCombo As New MyComboBox() With {.Width = 100, .HorizontalAlignment = HorizontalAlignment.Left, .ToolTip = "修改设置前请确保该存档未在游戏中打开，否则会导致设置无效"}
                    difficultyCombo.Items.Add(New With {.Value = 0, .Display = "和平"})
                    difficultyCombo.Items.Add(New With {.Value = 1, .Display = "简单"})
                    difficultyCombo.Items.Add(New With {.Value = 2, .Display = "普通"})
                    difficultyCombo.Items.Add(New With {.Value = 3, .Display = "困难"})
                    difficultyCombo.SelectedValuePath = "Value"
                    difficultyCombo.DisplayMemberPath = "Display"
                    difficultyCombo.SelectedValue = difficultyValue

                    Dim isHardcoreCheck = gameLevel.Get(Of NbtByte)("hardcore")
                    Dim isHardcoreMode As Boolean = (isHardcoreCheck.Value = "1")

                    Dim lockCheckBox As New MyCheckBox() With {.Text = "锁定难度", .ToolTip = "锁定当前难度设置，锁定后无法在游戏中更改游戏难度", .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(10, 0, 0, 0)
                    }

                    If isHardcoreMode Then
                        lockCheckBox.Visibility = Visibility.Collapsed
                    Else
                        Dim lockedElement = gameLevel.Get(Of NbtByte)("DifficultyLocked")
                        Dim isLocked As Boolean = (lockedElement IsNot Nothing AndAlso lockedElement.Value = "1")
                        lockCheckBox.Checked = isLocked
                    End If

                    Dim difficultyPanel As New StackPanel() With {
                        .Orientation = Orientation.Horizontal,
                        .HorizontalAlignment = HorizontalAlignment.Left
                    }
                    difficultyPanel.Children.Add(difficultyCombo)
                    difficultyPanel.Children.Add(lockCheckBox)

                    AddHandler difficultyCombo.SelectionChanged, Sub(s, e)
                                                                     Try
                                                                         If difficultyCombo.SelectedValue Is Nothing Then Return

                                                                         Dim newDifficulty As Integer = CInt(difficultyCombo.SelectedValue)

                                                                         gameLevel.Get(Of NbtByte)("Difficulty").Value = CByte(newDifficulty)

                                                                         If Not isHardcoreMode Then
                                                                             Dim newLocked As Integer = If(lockCheckBox.Checked, 1, 0)
                                                                             If gameLevel.Contains("DifficultyLocked") Then
                                                                                 gameLevel.Get(Of NbtByte)("DifficultyLocked").Value = CByte(newLocked)
                                                                             ElseIf newLocked = 1 Then
                                                                                 gameLevel.Add(New NbtByte("DifficultyLocked", CByte(newLocked)))
                                                                             End If
                                                                         End If

                                                                         Using fileStream As New FileStream(saveDatPath, FileMode.Create, FileAccess.Write, FileShare.None)
                                                                             saveInfo.SaveToStream(fileStream, NbtCompression.GZip)
                                                                         End Using
                                                                         Hint("难度设置修改成功", HintType.Finish)
                                                                     Catch ex As Exception
                                                                         Log(ex, "难度设置修改失败", LogLevel.Hint)
                                                                     End Try
                                                                 End Sub

                    AddHandler lockCheckBox.Change, Sub(sender, user)
                                                        Try
                                                            If difficultyCombo.SelectedValue Is Nothing Then Return

                                                            Dim newDifficulty As Integer = CInt(difficultyCombo.SelectedValue)

                                                            gameLevel.Get(Of NbtByte)("Difficulty").Value = CByte(newDifficulty)

                                                            If Not isHardcoreMode Then
                                                                Dim newLocked As Integer = If(lockCheckBox.Checked, 1, 0)
                                                                If gameLevel.Contains("DifficultyLocked") Then
                                                                    gameLevel.Get(Of NbtByte)("DifficultyLocked").Value = CByte(newLocked)
                                                                ElseIf newLocked = 1 Then
                                                                    gameLevel.Add(New NbtByte("DifficultyLocked", CByte(newLocked)))
                                                                End If
                                                            End If

                                                            Using fileStream As New FileStream(saveDatPath, FileMode.Create, FileAccess.Write, FileShare.None)
                                                                saveInfo.SaveToStream(fileStream, NbtCompression.GZip)
                                                            End Using
                                                            Hint("难度设置修改成功", HintType.Finish)
                                                        Catch ex As Exception
                                                            Log(ex, "难度设置修改失败", LogLevel.Hint)
                                                        End Try
                                                    End Sub

                    Dim rowIndex = PanSettingsList.RowDefinitions.Count
                    PanSettingsList.RowDefinitions.Add(New RowDefinition() With {.Height = New GridLength(1, GridUnitType.Auto)})

                    Dim headTextBlock As New TextBlock With {.Text = "游戏难度", .Margin = New Thickness(0, 3, 0, 3)}
                    Grid.SetRow(headTextBlock, rowIndex)
                    Grid.SetColumn(headTextBlock, 0)

                    Grid.SetRow(difficultyPanel, rowIndex)
                    Grid.SetColumn(difficultyPanel, 2)

                    PanSettingsList.Children.Add(headTextBlock)
                    PanSettingsList.Children.Add(difficultyPanel)
                End If

                AddInfoTable("最后一次游玩", New DateTime(1970, 1, 1, 0, 0, 0).AddMilliseconds(Long.Parse(gameLevel.Get(Of NbtLong)("LastPlayed").Value)).ToLocalTime().ToString())

                Dim spawnX As NbtInt = Nothing
                If gameLevel.TryGet(Of NbtInt)("SpawnX", spawnX) Then
                    Dim spawnY = gameLevel.Get(Of NbtInt)("SpawnY")
                    Dim spawnZ = gameLevel.Get(Of NbtInt)("SpawnZ")
                    AddInfoTable("出生点 (X/Y/Z)", $"{spawnX.Value} / {spawnY.Value} / {spawnZ.Value}")
                Else
                    Dim spawnPos = gameLevel.Get(Of NbtCompound)("spawn").Get(Of NbtIntArray)("pos")
                    Dim spawnXPos = spawnPos.Item(0)
                    Dim spawnYPos = spawnPos.Item(1)
                    Dim spawnZPos = spawnPos.Item(2)
                    AddInfoTable("出生点 (X/Y/Z)", $"{spawnXPos} / {spawnYPos} / {spawnZPos}")
                End If
                Dim gameTypeName As String = "获取失败"

                Dim isHardcore = gameLevel.Get(Of NbtByte)("hardcore")
                If isHardcore.Value = "1" Then
                    gameTypeName = "极限模式"
                Else
                    Dim gameType = gameLevel.Get(Of NbtInt)("GameType")
                    Select Case gameType.Value
                        Case 0
                            gameTypeName = "生存模式"
                        Case 1
                            gameTypeName = "创造模式"
                        Case 2
                            gameTypeName = "冒险模式"
                        Case 3
                            gameTypeName = "旁观模式"
                        Case Else
                            gameTypeName = "生存模式"
                    End Select
                End If
                AddInfoTable("游戏模式", gameTypeName)

                If hasDifficulty Then
                    Dim difficultyElement = gameLevel.Get(Of NbtByte)("Difficulty")
                    Dim difficultyName As String = "获取失败"
                    Dim difficultyValue As Integer = Integer.Parse(difficultyElement.Value)
                    Select Case difficultyValue
                        Case 0
                            difficultyName = "和平"
                        Case 1
                            difficultyName = "简单"
                        Case 2
                            difficultyName = "普通"
                        Case 3
                            difficultyName = "困难"
                    End Select
                    Dim lockedElement = gameLevel.Get(Of NbtByte)("DifficultyLocked")
                    Dim isDifficultyLocked As String = If((lockedElement IsNot Nothing AndAlso lockedElement.Value = "1") OrElse isHardcore.Value = "1", "是", If(lockedElement IsNot Nothing, "否", "获取失败"))
                    If Hintversion1_8.Visibility <> Visibility.Visible Then
                        AddInfoTable("困难度", $"{difficultyName} (是否已锁定难度：{isDifficultyLocked})")
                    End If
                End If

                Dim totalTicks As Long = Long.Parse(gameLevel.Get(Of NbtLong)("Time").Value)
                Dim totalSeconds As Double = totalTicks / 20.0
                Dim playTime As TimeSpan = TimeSpan.FromSeconds(totalSeconds)
                Dim formattedPlayTime As String = $"{playTime.Days} 天 {playTime.Hours} 小时 {playTime.Minutes} 分钟"
                AddInfoTable("游戏时长", formattedPlayTime)
                PanContent.Visibility = Visibility.Visible
            End Using
        Catch ex As Exception
            Log(ex, $"获取存档信息失败", LogLevel.Msgbox)
            PanContent.Visibility = Visibility.Collapsed
            PanSettings.Visibility = Visibility.Collapsed
            Hintversion1_9.Visibility = Visibility.Collapsed
            Hintversion1_8.Visibility = Visibility.Collapsed
            Hintversion1_3.Visibility = Visibility.Collapsed
        End Try
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
            Dim thisBtn = New MyTextButton With {.Text = content, .Margin = New Thickness(0, 3, 0, 3)}
            contentTextBlock = thisBtn
            AddHandler thisBtn.Click, Sub()
                                          Try
                                              ClipboardSet(content)
                                          Catch ex As Exception
                                              Log(ex, "复制到剪贴板失败", LogLevel.Hint)
                                          End Try
                                      End Sub
        Else
            contentTextBlock = New TextBlock With {.Text = content, .Margin = New Thickness(0, 3, 0, 3)}
        End If
        contentStack.Children.Add(contentTextBlock)

        If isSeed AndAlso content <> "获取失败" Then
            Dim BtnChunkbase As New MyIconButton With {
            .Logo = Logo.IconButtonlink,
            .ToolTip = "跳转到 Chunkbase",
            .Width = 24,
            .Height = 24
        }
            contentStack.Children.Add(BtnChunkbase)

            AddHandler BtnChunkbase.Click, Sub()
                                               Try
                                                   If versionName Is Nothing Then
                                                       Log($"当前存档版本无法确定，因此无法跳转到 Chunkbase", LogLevel.Hint)
                                                       Return
                                                   End If

                                                   If versionName.Any(Function(c) Char.IsLetter(c)) Then
                                                       Log($"当前存档版本 '{versionName}' 可能是预览版，不受支持，无法跳转到 Chunkbase", LogLevel.Hint)
                                                       Return
                                                   End If

                                                   Dim versionParts = versionName.Split("."c)
                                                   Dim usedVersion As String
                                                   If versionName.StartsWith("1.21") Then
                                                       usedVersion = versionName.Replace(".", "_")
                                                   ElseIf versionName.Contains(".") Then
                                                       usedVersion = String.Join("_", versionName.Split("."c).Take(2))
                                                   Else
                                                       usedVersion = versionName.Replace(".", "_")
                                                   End If

                                                   Dim cbUri = $"https://www.chunkbase.com/apps/seed-map#seed={content}&platform=java_{usedVersion}&dimension=overworld"
                                                   OpenWebsite(cbUri)
                                               Catch ex As Exception
                                                   Log(ex, "跳转到 Chunkbase 失败", LogLevel.Hint)
                                               End Try
                                           End Sub
        End If

        PanList.Children.Add(headTextBlock)
        PanList.Children.Add(contentStack)
        Dim targetRow = New RowDefinition
        PanList.RowDefinitions.Add(targetRow)
        Dim rowIndex = PanList.RowDefinitions.IndexOf(targetRow)
        Grid.SetRow(headTextBlock, rowIndex)
        Grid.SetColumn(headTextBlock, 0)
        Grid.SetRow(contentTextBlock, rowIndex)
        Grid.SetColumn(contentTextBlock, 2)
        Grid.SetRow(contentStack, rowIndex)
        Grid.SetColumn(contentStack, 2)
    End Sub
End Class
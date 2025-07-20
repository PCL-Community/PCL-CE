Imports System.Xml.XPath
Imports PlainNamedBinaryTag

Class PageVersionSavesInfo
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
            Dim saveDatPath = IO.Path.Combine(PageVersionSavesLeft.CurrentSave, "level.dat")
            Dim isCompressed As Boolean
            Using fs As New FileStream(saveDatPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                Using saveInfo = VbNbtReaderCreator.FromStreamAutoDetect(fs, isCompressed)
                    Dim outRes As NbtType
                    Dim xData = saveInfo.ReadNbtAsXml(outRes)
                    Dim levelData = xData.XPathSelectElement("//TCompound[@Name='Data']")
                    ClearInfoTable()

                    Hintversion1_9.Visibility = Visibility.Collapsed
                    Hintversion1_8.Visibility = Visibility.Collapsed

                    Dim GetDataInfoByPath = Function(path As String) As String
                                                Dim element = levelData.XPathSelectElement(path)
                                                Return If(element IsNot Nothing, element.Value, "获取失败")
                                            End Function
                    Dim GetDataInfoByPathWithFallback = Function(path As String, fallbackPath As String) As String
                                                            Dim element = levelData.XPathSelectElement(path)
                                                            If element Is Nothing Then
                                                                element = levelData.XPathSelectElement(fallbackPath)
                                                            End If
                                                            Return If(element IsNot Nothing, element.Value, "获取失败")
                                                        End Function
                    AddInfoTable("存档名称", GetDataInfoByPath("//TString[@Name='LevelName']"))
                    Dim versionName As String = "获取失败"
                    Dim versionId As String = "获取失败"
                    versionName = GetDataInfoByPath("//TCompound[@Name='Version']/TString[@Name='Name']")
                    versionId = GetDataInfoByPath("//TCompound[@Name='Version']/TInt32[@Name='Id']")
                    Dim hasDifficulty = levelData.XPathSelectElement("//TInt8[@Name='Difficulty']") IsNot Nothing
                    If versionName = "获取失败" Then
                        If hasDifficulty Then
                            Hintversion1_9.Visibility = Visibility.Visible
                            Hintversion1_9.Text = $"1.9 以下的版本无法获取存档版本"
                        Else
                            Hintversion1_8.Visibility = Visibility.Visible
                            Hintversion1_8.Text = $"1.8 以下的版本无法获取存档版本和游戏难度"
                        End If
                    Else
                        AddInfoTable("存档版本", $"{versionName} ({versionId})")
                    End If
                    Dim seed As String = GetDataInfoByPathWithFallback("//TCompound[@Name='WorldGenSettings']/TInt64[@Name='seed']", "//TInt64[@Name='RandomSeed']")
                    AddInfoTable("种子", seed, True, versionName, True)
                    Dim allowCommandValue As Integer = Integer.Parse(GetDataInfoByPath("//TInt8[@Name='allowCommands']"))
                    Dim allowCommandName As String = "获取失败"
                    Select Case allowCommandValue
                        Case 0
                            allowCommandName = "不允许"
                        Case 1
                            allowCommandName = "允许"
                    End Select
                    AddInfoTable("是否允许作弊", allowCommandName)
                    AddInfoTable("最后一次游玩", New DateTime(1970, 1, 1, 0, 0, 0).AddMilliseconds(Long.Parse(GetDataInfoByPath("//TInt64[@Name='LastPlayed']"))).ToLocalTime().ToString())
                    Dim spawnX = GetDataInfoByPath("//TInt32[@Name='SpawnX']")
                    Dim spawnY = GetDataInfoByPath("//TInt32[@Name='SpawnY']")
                    Dim spawnZ = GetDataInfoByPath("//TInt32[@Name='SpawnZ']")
                    AddInfoTable("出生点 (X/Y/Z)", $"{spawnX} / {spawnY} / {spawnZ}")
                    If hasDifficulty Then
                        Dim difficultyElement = levelData.XPathSelectElement("//TInt8[@Name='Difficulty']")
                        Dim difficultyName As String = "获取失败"
                        If difficultyElement IsNot Nothing Then
                            Dim difficultyValue As Integer
                            If Integer.TryParse(difficultyElement.Value, difficultyValue) Then
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
                            End If
                        End If
                        Dim lockedElement = levelData.XPathSelectElement("//TInt8[@Name='DifficultyLocked']")
                        Dim isDifficultyLocked As String = If(lockedElement IsNot Nothing AndAlso lockedElement.Value = "1", "是", If(lockedElement IsNot Nothing, "否", "获取失败"))
                        If Hintversion1_8.Visibility <> Visibility.Visible Then
                            AddInfoTable("困难度", $"{difficultyName} (是否已锁定难度：{isDifficultyLocked})")
                        End If
                    End If
                    Dim totalTicks As Long = Long.Parse(GetDataInfoByPath("//TInt64[@Name='Time']"))
                    Dim dayTimeTicks As Long = Long.Parse(GetDataInfoByPath("//TInt64[@Name='DayTime']"))
                    Dim totalSeconds As Double = totalTicks / 20.0
                    Dim playTime As TimeSpan = TimeSpan.FromSeconds(totalSeconds)
                    Dim formattedPlayTime As String = $"{playTime.Days} 天 {playTime.Hours} 小时 {playTime.Minutes} 分钟"
                    AddInfoTable("游戏时长", formattedPlayTime)
                    PanContent.Visibility = Visibility.Visible
                End Using
            End Using
        Catch ex As Exception
            Log(ex, $"获取存档信息失败", LogLevel.Msgbox)
            PanContent.Visibility = Visibility.Collapsed
            Hintversion1_9.Visibility = Visibility.Collapsed
            Hintversion1_8.Visibility = Visibility.Collapsed
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
                                                   If versionName = "获取失败" Then
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

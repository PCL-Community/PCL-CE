Imports System.Threading.Tasks
Imports System.Xml.XPath
Imports PlainNamedBinaryTag

Class PageInstanceSavesInfo
    Implements IRefreshable

    Private _cacheKey As String = ""
    Private _cacheData As XElement = Nothing
    Private _isRefreshing As Boolean = False
    Private _cancellationTokenSource As Threading.CancellationTokenSource
    Private _currentRefreshId As Integer = 0
    
    Private Sub IRefreshable_Refresh() Implements IRefreshable.Refresh
        Refresh()
    End Sub
    
    Public Sub Refresh()
        RefreshInfoAsync()
    End Sub

    Private _loaded As Boolean = False
    Private _isInitialized As Boolean = False
    
    Private Sub Init() Handles Me.Loaded
        If _isInitialized Then Return
        _isInitialized = True
        
        PanBack.ScrollToHome()
        _loaded = True
        
        ' 延迟一点再执行异步操作，确保页面完全加载
        Dispatcher.BeginInvoke(Sub() RefreshInfoAsync(), Threading.DispatcherPriority.Background)
    End Sub
    
    Private Sub PageUnloaded() Handles Me.Unloaded
        _loaded = False
        _isInitialized = False
        ' 页面卸载时取消所有异步操作
        If _cancellationTokenSource IsNot Nothing Then
            _cancellationTokenSource.Cancel()
            _cancellationTokenSource.Dispose()
            _cancellationTokenSource = Nothing
        End If
    End Sub

    Private Async Sub RefreshInfoAsync()
        ' 确保页面已完全加载
        If Not _loaded OrElse Not _isInitialized Then Return
        
        ' 立即隐藏所有提示，避免在任何情况下闪烁
        If Dispatcher.CheckAccess() Then
            HideAllHints()
        Else
            Dispatcher.Invoke(Sub() HideAllHints())
        End If
        
        ' 取消之前的操作
        If _cancellationTokenSource IsNot Nothing Then
            _cancellationTokenSource.Cancel()
            _cancellationTokenSource.Dispose()
        End If
        
        _cancellationTokenSource = New Threading.CancellationTokenSource()
        Dim cancellationToken = _cancellationTokenSource.Token
        Dim currentRefreshId = Threading.Interlocked.Increment(_currentRefreshId)
        
        Try
            ' 再次检查页面状态
            If Not _loaded OrElse Not _isInitialized OrElse cancellationToken.IsCancellationRequested Then Return
            
            ' 不立即隐藏UI，而是先检查是否有缓存数据
            Dim saveData As XElement = Nothing
            Dim hasCache = False
            
            ' 先快速检查缓存
            Try
                Dim saveDatPath = IO.Path.Combine(PageInstanceSavesLeft.CurrentSave, "level.dat")
                If IO.File.Exists(saveDatPath) Then
                    Dim instanceKey = If(PageInstanceSavesLeft.CurrentSave, "unknown")
                    Dim currentCacheKey = $"{instanceKey}_{saveDatPath}_{IO.File.GetLastWriteTime(saveDatPath).Ticks}"
                    If _cacheKey = currentCacheKey AndAlso _cacheData IsNot Nothing Then
                        saveData = _cacheData
                        hasCache = True
                    End If
                End If
            Catch
                ' 忽略缓存检查错误
            End Try
            
            ' 只有在没有缓存数据时才显示加载状态
            If Not hasCache Then
                If Dispatcher.CheckAccess() Then
                    ' 使用淡出效果而不是立即隐藏
                    PanContent.Opacity = 0.5
                Else
                    Dispatcher.Invoke(Sub() PanContent.Opacity = 0.5)
                End If
            End If
            
            ' 如果没有缓存，异步加载数据
            If Not hasCache Then
                saveData = Await Task.Run(Function() LoadSaveDataAsync(cancellationToken), cancellationToken)
            End If
            
            ' 检查操作是否被取消或过期
            If cancellationToken.IsCancellationRequested OrElse currentRefreshId <> _currentRefreshId OrElse Not _loaded Then
                Return
            End If
            
            If saveData Is Nothing Then 
                If Dispatcher.CheckAccess() Then
                    PanContent.Visibility = Visibility.Collapsed
                    PanContent.Opacity = 1.0
                Else
                    Dispatcher.Invoke(Sub()
                                          PanContent.Visibility = Visibility.Collapsed
                                          PanContent.Opacity = 1.0
                                      End Sub)
                End If
                Return
            End If
            
            ' 在UI线程中更新界面
            If Dispatcher.CheckAccess() Then
                UpdateUISmooth(saveData)
            Else
                Dispatcher.Invoke(Sub() UpdateUISmooth(saveData))
            End If
            
        Catch ex As OperationCanceledException
            ' 操作被取消，正常情况，不需要处理
        Catch ex As Exception
            ' 检查操作是否被取消
            If Not cancellationToken.IsCancellationRequested Then
                Log(ex, $"获取存档信息失败", LogLevel.Msgbox)
                If _loaded Then
                    If Dispatcher.CheckAccess() Then
                        PanContent.Visibility = Visibility.Collapsed
                        PanContent.Opacity = 1.0
                        HideAllHints()
                    Else
                        Dispatcher.Invoke(Sub()
                                              PanContent.Visibility = Visibility.Collapsed
                                              PanContent.Opacity = 1.0
                                              HideAllHints()
                                          End Sub)
                    End If
                End If
            End If
        End Try
    End Sub
    
    Private Function LoadSaveDataAsync(cancellationToken As Threading.CancellationToken) As XElement
        Try
            cancellationToken.ThrowIfCancellationRequested()
            
            Dim saveDatPath = IO.Path.Combine(PageInstanceSavesLeft.CurrentSave, "level.dat")
            If Not IO.File.Exists(saveDatPath) Then
                Return Nothing
            End If
            
            ' 包含实例信息的缓存键，避免不同实例间的冲突
            Dim instanceKey = If(PageInstanceSavesLeft.CurrentSave, "unknown")
            Dim currentCacheKey = $"{instanceKey}_{saveDatPath}_{IO.File.GetLastWriteTime(saveDatPath).Ticks}"
            
            ' 检查缓存
            If _cacheKey = currentCacheKey AndAlso _cacheData IsNot Nothing Then
                Return _cacheData
            End If
            
            cancellationToken.ThrowIfCancellationRequested()
            
            Dim isCompressed As Boolean
            Using fs As New FileStream(saveDatPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                cancellationToken.ThrowIfCancellationRequested()
                
                Using saveInfo = VbNbtReaderCreator.FromStreamAutoDetect(fs, isCompressed)
                    Dim outRes As NbtType
                    Dim xData = saveInfo.ReadNbtAsXml(outRes)
                    Dim parsedLevelData = xData.XPathSelectElement("//TCompound[@Name='Data']")
                    
                    cancellationToken.ThrowIfCancellationRequested()
                    
                    ' 更新缓存
                    _cacheKey = currentCacheKey
                    _cacheData = parsedLevelData
                    
                    Return parsedLevelData
                End Using
            End Using
        Catch ex As OperationCanceledException
            Throw
        Catch ex As Exception
            Log(ex, $"加载存档数据失败", LogLevel.Debug)
            Return Nothing
        End Try
    End Function
    
    Private Sub UpdateUISmooth(levelData As XElement)
        ' 确保在UI线程中执行
        If Not Dispatcher.CheckAccess() Then
            Dispatcher.Invoke(Sub() UpdateUISmooth(levelData))
            Return
        End If
        
        ' 检查页面是否仍然有效
        If Not _loaded OrElse levelData Is Nothing Then Return
        
        Try
            ' 暂时隐藏控件以避免闪烁，但保持可见性
            PanList.Opacity = 0
            PanList.IsEnabled = False
            
            ClearInfoTable()

            Dim GetDataInfoByPath = Function(path As String) As String
                                        Try
                                            Dim element = levelData.XPathSelectElement(path)
                                            Return If(element IsNot Nothing, element.Value, "获取失败")
                                        Catch
                                            Return "获取失败"
                                        End Try
                                    End Function
            Dim GetDataInfoByPathWithFallback = Function(path As String, fallbackPath As String) As String
                                                    Try
                                                        Dim element = levelData.XPathSelectElement(path)
                                                        If element Is Nothing Then
                                                            element = levelData.XPathSelectElement(fallbackPath)
                                                        End If
                                                        Return If(element IsNot Nothing, element.Value, "获取失败")
                                                    Catch
                                                        Return "获取失败"
                                                    End Try
                                                End Function

            ' 预先收集所有信息以减少XPath查询
            Dim infoData As New Dictionary(Of String, String)()
            
            ' 基本信息
            infoData("存档名称") = GetDataInfoByPath("//TString[@Name='LevelName']")
            
            ' 版本信息
            Dim versionName As String = GetDataInfoByPath("//TCompound[@Name='Version']/TString[@Name='Name']")
            Dim versionId As String = GetDataInfoByPath("//TCompound[@Name='Version']/TInt32[@Name='Id']")
            
            ' 检查版本兼容性
            Dim hasDifficulty = levelData.XPathSelectElement("//TInt8[@Name='Difficulty']") IsNot Nothing
            Dim hasAllowCommands = levelData.XPathSelectElement("//TInt8[@Name='allowCommands']") IsNot Nothing
            
            ShowVersionHints(versionName, hasDifficulty, hasAllowCommands)
            
            If versionName <> "获取失败" Then
                infoData("存档版本") = $"{versionName} ({versionId})"
            End If
            
            ' 其他信息
            Dim seed As String = GetDataInfoByPathWithFallback("//TCompound[@Name='WorldGenSettings']/TInt64[@Name='seed']", "//TInt64[@Name='RandomSeed']")
            infoData("种子") = seed
            
            If hasAllowCommands Then
                Try
                    Dim allowCommandValue As Integer = Integer.Parse(GetDataInfoByPath("//TInt8[@Name='allowCommands']"))
                    infoData("是否允许作弊") = If(allowCommandValue = 1, "允许", "不允许")
                Catch
                    infoData("是否允许作弊") = "获取失败"
                End Try
            End If
            
            ' 时间相关信息
            Try
                Dim lastPlayed = Long.Parse(GetDataInfoByPath("//TInt64[@Name='LastPlayed']"))
                infoData("最后一次游玩") = New DateTime(1970, 1, 1, 0, 0, 0).AddMilliseconds(lastPlayed).ToLocalTime().ToString()
            Catch
                infoData("最后一次游玩") = "获取失败"
            End Try
            
            ' 坐标信息
            Dim spawnX = GetDataInfoByPath("//TInt32[@Name='SpawnX']")
            Dim spawnY = GetDataInfoByPath("//TInt32[@Name='SpawnY']")
            Dim spawnZ = GetDataInfoByPath("//TInt32[@Name='SpawnZ']")
            infoData("出生点 (X/Y/Z)") = $"{spawnX} / {spawnY} / {spawnZ}"
            
            ' 难度信息
            If hasDifficulty Then
                Try
                    Dim difficultyElement = levelData.XPathSelectElement("//TInt8[@Name='Difficulty']")
                    If difficultyElement IsNot Nothing Then
                        Dim difficultyValue As Integer = Integer.Parse(difficultyElement.Value)
                        Dim difficultyNames = {"和平", "简单", "普通", "困难"}
                        Dim difficultyName = If(difficultyValue >= 0 AndAlso difficultyValue < difficultyNames.Length, difficultyNames(difficultyValue), "未知")
                        
                        Dim lockedElement = levelData.XPathSelectElement("//TInt8[@Name='DifficultyLocked']")
                        Dim isDifficultyLocked As String = If(lockedElement IsNot Nothing AndAlso lockedElement.Value = "1", "是", If(lockedElement IsNot Nothing, "否", "获取失败"))
                        
                        If Hintversion1_8.Visibility <> Visibility.Visible Then
                            infoData("困难度") = $"{difficultyName} (是否已锁定难度：{isDifficultyLocked})"
                        End If
                    End If
                Catch
                    ' 忽略难度解析错误
                End Try
            End If
            
            ' 游戏时长
            Try
                Dim totalTicks As Long = Long.Parse(GetDataInfoByPath("//TInt64[@Name='Time']"))
                Dim playTime As TimeSpan = TimeSpan.FromSeconds(totalTicks / 20.0)
                infoData("游戏时长") = $"{playTime.Days} 天 {playTime.Hours} 小时 {playTime.Minutes} 分钟"
            Catch
                infoData("游戏时长") = "获取失败"
            End Try
            
            ' 批量添加信息到界面
            AddInfoTableBatch(infoData, seed, versionName)
            
            ' 平滑显示界面
            PanContent.Visibility = Visibility.Visible
            PanContent.Opacity = 1.0
            PanList.Opacity = 1.0
            
        Finally
            PanList.IsEnabled = True
        End Try
    End Sub
    
    Private Sub ShowVersionHints(versionName As String, hasDifficulty As Boolean, hasAllowCommands As Boolean)
        If versionName = "获取失败" Then
            If hasDifficulty Then
                Hintversion1_9.Visibility = Visibility.Visible
                Hintversion1_9.Text = "1.9 以下的版本无法获取存档版本"
            Else
                If hasAllowCommands Then
                    Hintversion1_8.Visibility = Visibility.Visible
                    Hintversion1_8.Text = "1.8 以下的版本无法获取存档版本和游戏难度"
                Else
                    HintVersion1_3.Visibility = Visibility.Visible
                    HintVersion1_3.Text = "1.3 以下的版本无法获取存档版本、游戏难度和是否允许作弊"
                End If
            End If
        End If
    End Sub
    
    Private Sub HideAllHints()
        Hintversion1_9.Visibility = Visibility.Collapsed
        Hintversion1_8.Visibility = Visibility.Collapsed
        HintVersion1_3.Visibility = Visibility.Collapsed
    End Sub

    Private Sub ClearInfoTable()
        PanList.Children.Clear()
        PanList.RowDefinitions.Clear()
    End Sub
    
    Private Sub AddInfoTableBatch(infoData As Dictionary(Of String, String), seed As String, versionName As String)
        ' 预先创建所有行定义
        Dim rowCount = infoData.Count
        For i = 0 To rowCount - 1
            PanList.RowDefinitions.Add(New RowDefinition())
        Next
        
        Dim currentRow = 0
        For Each kvp In infoData
            Dim isSeed = kvp.Key = "种子"
            AddInfoTableRow(kvp.Key, kvp.Value, currentRow, isSeed, versionName, isSeed)
            currentRow += 1
        Next
    End Sub
    
    Private Sub AddInfoTableRow(head As String, content As String, rowIndex As Integer, isSeed As Boolean, versionName As String, allowCopy As Boolean)
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
            Dim btnChunkbase As New MyIconButton With {
                .Logo = Logo.IconButtonlink,
                .ToolTip = "跳转到 Chunkbase",
                .Width = 24,
                .Height = 24
            }
            contentStack.Children.Add(btnChunkbase)

            AddHandler btnChunkbase.Click, Sub() OpenChunkbase(content, versionName)
        End If

        ' 批量设置Grid属性
        Grid.SetRow(headTextBlock, rowIndex)
        Grid.SetColumn(headTextBlock, 0)
        Grid.SetRow(contentStack, rowIndex)
        Grid.SetColumn(contentStack, 2)
        
        PanList.Children.Add(headTextBlock)
        PanList.Children.Add(contentStack)
    End Sub
    
    Private Sub OpenChunkbase(seed As String, versionName As String)
        Try
            If versionName = "获取失败" Then
                Log("当前存档版本无法确定，因此无法跳转到 Chunkbase", LogLevel.Hint)
                Return
            End If

            If versionName.Any(Function(c) Char.IsLetter(c)) Then
                Log($"当前存档版本 '{versionName}' 可能是预览版，不受支持，无法跳转到 Chunkbase", LogLevel.Hint)
                Return
            End If

            Dim usedVersion As String
            If versionName.StartsWith("1.21") Then
                usedVersion = versionName.Replace(".", "_")
            ElseIf versionName.Contains(".") Then
                usedVersion = String.Join("_", versionName.Split("."c).Take(2))
            Else
                usedVersion = versionName.Replace(".", "_")
            End If

            Dim cbUri = $"https://www.chunkbase.com/apps/seed-map#seed={seed}&platform=java_{usedVersion}&dimension=overworld"
            OpenWebsite(cbUri)
        Catch ex As Exception
            Log(ex, "跳转到 Chunkbase 失败", LogLevel.Hint)
        End Try
    End Sub
End Class

Imports System.Globalization
Imports System.IO.Compression
Imports System.Text.RegularExpressions
Imports PCL.Core.Service

Class PageOtherLog
    Private Sub PageOtherLog_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        '重复加载部分
        PanBack.ScrollToHome()
        LoadList()
        '非重复加载部分
        If IsLoaded Then Exit Sub

    End Sub

    Private Shared ReadOnly Property LogDirectory As String
        Get
            Return LogService.Logger.Configuration.StoreFolder
        End Get
    End Property

    Private Shared ReadOnly Property CurrentLogs As List(Of String)
        Get
            Dim logs = LogService.Logger.LogFiles
            Return logs.ConvertAll(Function(item) IO.Path.GetFullPath(item))
        End Get
    End Property

    Public Sub LoadList()
        PanList.Children.Clear()
        Dim current = CurrentLogs
        For Each item In Directory.GetFiles(LogDirectory)
            Dim fullPath = IO.Path.GetFullPath(item)
            Dim title = IO.Path.GetFileName(item)
            If title.StartsWith("Launch") Then
                title = title.Substring(7, title.Length - 11)
                Dim dt As DateTime
                Dim r = DateTime.TryParseExact(title, "yyyy-M-d-HHmmssfff",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, dt)
                If r Then title = dt.ToString("yyyy 年 M 月 d 日 HH:mm:ss.fff")
                If current.Any(Function(log) log.Equals(fullPath)) Then title = title & " (当前)"
            ElseIf title.StartsWith("LastPending") Then
                title = title.Substring(11, title.Length - 15)
                If title.Length > 1 Then
                    title = "临时存储的日志 (" & title.Substring(1) & ")"
                Else
                    title = "临时存储的未输出日志"
                End If
            End If
            Dim ele As New MyListItem With {
                    .Type = MyListItem.CheckType.Clickable,
                    .Title = title,
                    .Info = fullPath, .Tag = fullPath}
            AddHandler ele.Click,
                Sub(sender, e)
                    Dim s = CType(sender, MyListItem)
                    Dim file = CType(s.Tag, String)
                    Process.Start(file)
                End Sub
            PanList.Children.Add(ele)
        Next
    End Sub

    Private Shared Sub ExportLog(sourceFiles As IEnumerable(Of String))
        Const filter = "PCL CE 日志压缩包|*.zip"
        Dim desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        Dim baseName = "PCL_CE_Logs_" & DateTime.Now.ToString("yyyyMMddHHmmss")
        Dim tempDirName = baseName & ".tmp"
        Dim fileName = baseName & ".zip"
        Dim selectedPath = SelectSaveFile("导出日志文件", fileName, filter, desktopPath)
        If String.IsNullOrEmpty(selectedPath) Then Exit Sub
        Try
            Directory.CreateDirectory(tempDirName)
            If File.Exists(selectedPath) Then File.Delete(selectedPath)
            Using zip = ZipFile.Open(selectedPath, ZipArchiveMode.Create)
                For Each item In sourceFiles
                    Dim itemFileName = IO.Path.GetFileName(item)
                    Dim tempPath = IO.Path.Combine(tempDirName, itemFileName)
                    File.Copy(item, tempPath)
                    zip.CreateEntryFromFile(tempPath, itemFileName, CompressionLevel.Fastest)
                    File.Delete(tempPath)
                Next
            End Using
            Hint("日志保存成功！", HintType.Finish)
        Catch ex As Exception
            Log(ex, "日志保存失败", LogLevel.Hint)
        Finally
            If Directory.Exists(tempDirName) Then Directory.Delete(tempDirName)
        End Try
    End Sub

    Private Sub ButtonOpenDir_OnClick(sender As Object, e As MouseButtonEventArgs)
        Process.Start(LogDirectory)
    End Sub

    Private Sub ButtonClean_OnClick(sender As Object, e As MouseButtonEventArgs)
        Dim r = MyMsgBox("是否删除所有历史日志？", "清理历史日志", "确定", "取消", IsWarn:=True)
        If r <> 1 Then Exit Sub
        Dim currentSet As New HashSet(Of String)(CurrentLogs)
        For Each item In Directory.GetFiles(LogDirectory)
            If Not currentSet.Contains(item) Then File.Delete(item)
        Next
        Hint("清理日志文件成功！", HintType.Finish)
        LoadList()
    End Sub

    Private Sub ButtonExportAll_OnClick(sender As Object, e As MouseButtonEventArgs)
        ExportLog(Directory.GetFiles(LogDirectory))
    End Sub
    
    Private Shared ReadOnly PatternPendingLogPath As New Regex("LastPending[_]?[^\\]*\.log$")

    Private Sub ButtonExport_OnClick(sender As Object, e As MouseButtonEventArgs)
        Dim pendingLogs = Array.FindAll(
            Directory.GetFiles(LogDirectory), Function(s) PatternPendingLogPath.IsMatch(s))
        ExportLog(CurrentLogs.Concat(pendingLogs))
    End Sub
End Class

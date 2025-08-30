Imports System.Globalization
Imports System.IO.Compression
Imports System.Text.RegularExpressions
Imports PCL.Core.App
Imports PCL.Core.Logging
Imports PCL.Core.UI
Imports PCL.Core.Utils
Imports PCL.Core.Utils.Exts
Imports PCL.Core.Utils.OS

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
                    Basics.OpenPath(file)
                End Sub
            PanList.Children.Add(ele)
        Next
    End Sub

    Private Shared Async Function ExportLog(sourceFiles As IEnumerable(Of String)) As Task
        If Await LogManager.ExportLogAsync(sourceFiles)
            Hint("日志保存成功！", HintType.Finish)
        Else
            Hint("日志保存失败！", HintType.Critical)
        End If
    End Sub

    Private Sub ButtonOpenDir_OnClick(sender As Object, e As MouseButtonEventArgs)
        Basics.OpenPath(LogDirectory)
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

    Private Sub ButtonExport_OnClick(sender As Object, e As MouseButtonEventArgs)
        Dim pendingLogs = Array.FindAll(
            Directory.GetFiles(LogDirectory), Function(s) s.IsMatch(RegexPatterns.LastPendingLogPath))
        ExportLog(CurrentLogs.Concat(pendingLogs))
    End Sub
End Class

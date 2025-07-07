
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports Windows.ApplicationModel

Class PageOtherLog
    Private Sub PageOtherLogk_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        '重复加载部分
        PanBack.ScrollToHome()
        LoadList()
        '非重复加载部分
        If IsLoaded Then Exit Sub

    End Sub
    Public Sub LoadList()
        PanList.Children.Clear()
        For Each item In Directory.GetFiles(IO.Path.Combine(Path, "PCL/Log"))
            Dim ele As MyListItem = New MyListItem With {.Type = MyListItem.CheckType.Clickable, .Title = $"{IO.Path.GetFileName(item).Replace("Launch-", "").Replace(".log", "")} 的日志", .Info = IO.Path.GetFullPath(item)}
            AddHandler ele.Click, Sub()
                                      SaveLogFile(item)
                                  End Sub
            PanList.Children.Add(ele)
        Next
    End Sub

    Private Sub SaveLogFile(srcFile As String)
        ' 1. 创建 SaveFileDialog 实例
        Dim saveDialog As New SaveFileDialog()

        ' 2. 配置对话框选项
        saveDialog.Filter = "PCL2 日志文件 (*.log)|*.log" ' 文件类型筛选
        saveDialog.DefaultExt = ".log" ' 默认扩展名
        saveDialog.FileName = IO.Path.GetFileName(srcFile)
        saveDialog.OverwritePrompt = True ' 如果文件已存在，提示是否覆盖

        ' 3. 显示对话框，并检查用户是否点击"保存"
        If saveDialog.ShowDialog() = DialogResult.OK Then
            Dim filePath As String = saveDialog.FileName

            ' 4. 使用 File.WriteAllText 保存文件
            Try
                File.Copy(filePath, srcFile)
                Hint("日志文件保存成功！"， HintType.Finish)
            Catch ex As Exception
                Hint($"保存失败: {ex.Message}"， HintType.Critical)
            End Try
        End If
    End Sub

End Class

Public Class PageOtherFeedback

    Public Class Feedback
        Public Property User As String
        Public Property Title As String
        Public Property Time As Date
        Public Property Content As String
        Public Property Url As String
        Public Property ID As String
        Public Property Tags As New List(Of String)
        Public Property Open As Boolean = True
    End Class

    Enum TagID As Int64
        Processing = 6820804544 '处理中
        WaitingProcess = 6820804546 '等待处理
        Completed = 6820804547 '完成
        Decline = 6820804539 '拒绝
        Ignored = 8064650117 '忽略
        Duplicate = 6820804541 '重复
        Wait = 8743070786
    End Enum

    Private Shadows IsLoaded As Boolean = False
    Private Sub PageOtherFeedback_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PageLoaderInit(Load, PanLoad, PanContent, PanInfo, Loader, AddressOf RefreshList, AddressOf LoaderInput)
        '重复加载部分
        PanBack.ScrollToHome()
        '非重复加载部分
        If IsLoaded Then Exit Sub
        IsLoaded = True

    End Sub

    Public Loader As New LoaderTask(Of Integer, List(Of Feedback))("FeedbackList", AddressOf FeedbackListGet, AddressOf LoaderInput)

    Private Function LoaderInput() As Integer
        Return 0 ' awa?
    End Function

    Public Sub FeedbackListGet(Task As LoaderTask(Of Integer, List(Of Feedback)))
        Dim list As JArray
        list = NetGetCodeByRequestRetry("https://api.github.com/repos/PCL-Community/PCL2-CE/issues?state=all&sort=created&per_page=200", BackupUrl:="https://api.kkgithub.com/repos/PCL-Community/PCL2-CE/issues?state=all&sort=created&per_page=200", IsJson:=True, UseBrowserUserAgent:=True) ' 获取近期 200 条数据就够了
        If list Is Nothing Then Throw New Exception("无法获取到内容")
        Dim res As List(Of Feedback) = New List(Of Feedback)
        For Each i As JObject In list
            Dim item As Feedback = New Feedback With {.Title = i("title").ToString(),
                .Url = i("html_url").ToString(),
                .Content = i("body").ToString(),
                .Time = Date.Parse(i("created_at").ToString()),
                .User = i("user")("login").ToString(),
                .ID = i("number"),
                .Open = i("state").ToString().Equals("open")}
            Dim thisTags As JArray = i("labels")
            For Each thisTag As JObject In thisTags
                item.Tags.Add(thisTag("id"))
            Next
            res.Add(item)
        Next
        Task.Output = res
    End Sub

    Public Sub RefreshList()
        PanListCompleted.Children.Clear()
        PanListProcessing.Children.Clear()
        PanListWaitingProcess.Children.Clear()
        PanListDecline.Children.Clear()
        For Each item In Loader.Output
            Dim ele As New MyListItem With {.Title = item.Title, .Type = MyListItem.CheckType.Clickable}
            Dim StatusDesc As String = "???"
            If item.Tags.Contains(TagID.Duplicate) Then
            End If
            If item.Open Then
                If item.Tags.Contains(TagID.Processing) Then
                    ele.Logo = PathImage & "Blocks/CommandBlock.png"
                    StatusDesc = "处理中"
                End If
            End If
            If item.Tags.Contains(TagID.WaitingProcess) Then
                ele.Logo = PathImage & "Blocks/Anvil.png"
                StatusDesc = "已确认，等待社区开发者接管该内容的处理"
            End If
            If item.Tags.Contains(TagID.Wait) Then
                ele.Logo = PathImage & "Blocks/RedstoneBlock.png"
                StatusDesc = "等待处理"
            End If
            If item.Tags.Contains(TagID.Completed) Then
                ele.Logo = PathImage & "Blocks/Grass.png"
                StatusDesc = "已完成"
            End If
            If item.Tags.Contains(TagID.Decline) Then
                ele.Logo = PathImage & "Blocks/CobbleStone.png"
                StatusDesc = "已拒绝"
            End If
            If item.Tags.Contains(TagID.Ignored) Then
                ele.Logo = PathImage & "Blocks/CobbleStone.png"
                StatusDesc = "已忽略"
            End If
            ele.Info = item.User & " | " & item.Time
            ele.Tags = StatusDesc
            AddHandler ele.Click, Sub()
                                      Select Case MyMsgBox($"提交者：{item.User}（{GetTimeSpanString(item.Time - DateTime.Now, False)}）{vbCrLf}状态：{StatusDesc}{vbCrLf}{vbCrLf}{item.Content}",
                                               "#" & item.ID & " " & item.Title,
                                               Button2:="查看详情")
                                          Case 2
                                              OpenWebsite(item.Url)
                                      End Select
                                  End Sub
            If StatusDesc.StartsWithF("处理中") Then
                PanListProcessing.Children.Add(ele)
            ElseIf StatusDesc.Equals("等待处理") Then
                PanListWaitingProcess.Children.Add(ele)
            ElseIf StatusDesc.Equals("已完成") Then
                PanListCompleted.Children.Add(ele)
            ElseIf StatusDesc.Equals("已拒绝") Then
                PanListDecline.Children.Add(ele）
            ElseIf StatusDesc.Equals("已忽略") Then
                PanListIgnored.Children.Add(ele)
            ElseIf StatusDesc.Equals("已确认，等待社区开发者接管该内容的处理") Then
                PanListWait.Children.Add(ele)
            End If
            PanContentDecline.Visibility = If(PanListDecline.Children.Count.Equals(0), Visibility.Collapsed, Visibility.Visible)
            PanContentCompleted.Visibility = If(PanListCompleted.Children.Count.Equals(0), Visibility.Collapsed, Visibility.Visible)
            PanContentWaitingProcess.Visibility = If(PanListWaitingProcess.Children.Count.Equals(0), Visibility.Collapsed, Visibility.Visible)
            PanContentProcessing.Visibility = If(PanListProcessing.Children.Count.Equals(0), Visibility.Collapsed, Visibility.Visible）
            PanContentIgnored.Visibility = If(PanListIgnored.Children.Count.Equals(0), Visibility.Collapsed, Visibility.Visible)
        Next
    End Sub

    Private Sub Feedback_Click(sender As Object, e As MouseButtonEventArgs)
        PageOtherLeft.TryFeedback()
    End Sub
End Class

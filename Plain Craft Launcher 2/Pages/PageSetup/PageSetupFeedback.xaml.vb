Imports PCL.Core.Utils

Public Class PageSetupFeedback

    Public Class Feedback
        Public Property User As String
        Public Property Title As String
        Public Property Time As Date
        Public Property Content As String
        Public Property Url As String
        Public Property ID As String
        Public Property Tags As New List(Of String)
        Public Property Open As Boolean = True
        Public Property Type As String
        Public Property IsPullRequest As Boolean = False
    End Class

    Enum TagID As Int64
        Processing = 6820804544
        WaitingProcess = 6820804546
        Completed = 6820804547
        Decline = 6820804539
        Ignored = 8064650117
        Duplicate = 6820804541
        Wait = 8743070786
        Pause = 8558220235
        Upnext = 8550609020
    End Enum

    Private Shadows IsLoaded As Boolean = False
    Private Sub PageOtherFeedback_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PageLoaderInit(Load, PanLoad, PanContent, PanInfo, Loader, AddressOf RefreshList)
        '重复加载部分
        PanBack.ScrollToHome()
        '非重复加载部分
        If IsLoaded Then Exit Sub
        IsLoaded = True

    End Sub

    Public Loader As New LoaderTask(Of Integer, List(Of Feedback))("FeedbackList", AddressOf FeedbackListGet)

    Public Sub FeedbackListGet(Task As LoaderTask(Of Integer, List(Of Feedback)))
        Dim list As JArray
        list = NetGetCodeByRequestRetry("https://api.github.com/repos/PCL-Community/PCL2-CE/issues?state=all&sort=created&per_page=200", IsJson:=True, UseBrowserUserAgent:=True) '获取近期 200 条数据就够了
        If list Is Nothing Then Throw New Exception("无法获取到内容")
        Dim res As List(Of Feedback) = New List(Of Feedback)
        For Each i As JObject In list
            Dim pullRequestToken As JToken = i("pull_request")
            If pullRequestToken IsNot Nothing AndAlso pullRequestToken.Type <> JTokenType.Null Then
                Continue For
            End If

            Dim item As Feedback = New Feedback With {
                .Title = i("title").ToString(),
                .Url = i("html_url").ToString(),
                .Content = i("body").ToString(),
                .Time = Date.Parse(i("created_at").ToString()),
                .User = i("user")("login").ToString(),
                .ID = i("number"),
                .Open = i("state").ToString().Equals("open"),
                .IsPullRequest = False
            }

            Dim issueType As String = "未分类"
            Dim typeToken As JToken = i("type")
            If typeToken IsNot Nothing AndAlso typeToken.Type = JTokenType.Object Then
                Dim typeNameToken As JToken = typeToken("name")
                If typeNameToken IsNot Nothing Then
                    issueType = typeNameToken.ToString().ToLower()
                End If
            End If
            item.Type = issueType

            Dim thisTags As JArray = i("labels")
            For Each thisTag As JObject In thisTags
                item.Tags.Add(thisTag("id"))
            Next
            res.Add(item)
        Next
        Task.Output = res
    End Sub

    Private Function CreateFeedbackItem(item As Feedback, logo As String) As MyListItem
        Dim commonInfo = $"{item.User} | {item.Time:yyyy-MM-dd HH:mm:ss}"

        Dim li As New MyListItem()
        With li
            .Title = item.Title
            .Type = MyListItem.CheckType.Clickable
            .Info = commonInfo
            .Logo = PathImage & logo
            .Tags = item.Type
        End With

        AddHandler li.Click, Sub(sender As Object, e As RoutedEventArgs)
                                 ShowFeedbackDetail(item)
                             End Sub

        Return li
    End Function

    Private Sub ShowFeedbackDetail(item As Feedback)
        Dim timeSpanText = TimeUtils.GetTimeSpanString(item.Time - DateTime.Now, False)
        Select Case MyMsgBoxMarkdown(
            $"提交者：{item.User}（{timeSpanText}）" & vbCrLf &
            $"类型：{item.Type}" & vbCrLf & vbCrLf &
            $"{item.Content}",
            $"#{item.ID} {item.Title}",
            Button2:="查看详情")
            Case 2
                OpenWebsite(item.Url)
        End Select
    End Sub

    Private Sub SetPanelVisibility(panel As StackPanel, card As MyCard)
        card.Visibility = If(panel.Children.Count = 0, Visibility.Collapsed, Visibility.Visible)
    End Sub

    Public Sub RefreshList()
        PanListProcessing.Children.Clear()
        PanListWaitingProcess.Children.Clear()
        PanListWait.Children.Clear()
        PanListPause.Children.Clear()
        PanListUpnext.Children.Clear()
        PanListCompleted.Children.Clear()
        PanListDecline.Children.Clear()
        PanListIgnored.Children.Clear()
        PanListDuplicate.Children.Clear()

        For Each item In Loader.Output
            If item.Tags.Contains(TagID.Processing) Then
                PanListProcessing.Children.Add(CreateFeedbackItem(item, "Blocks/CommandBlock.png"))
            End If

            If item.Tags.Contains(TagID.WaitingProcess) Then
                PanListWaitingProcess.Children.Add(CreateFeedbackItem(item, "Blocks/RedstoneBlock.png"))
            End If

            If item.Tags.Contains(TagID.Wait) Then
                PanListWait.Children.Add(CreateFeedbackItem(item, "Blocks/Anvil.png"))
            End If

            If item.Tags.Contains(TagID.Pause) Then
                PanListPause.Children.Add(CreateFeedbackItem(item, "Blocks/RedstoneLampOff.png"))
            End If

            If item.Tags.Contains(TagID.Upnext) Then
                PanListUpnext.Children.Add(CreateFeedbackItem(item, "Blocks/RedstoneLampOn.png"))
            End If

            If item.Tags.Contains(TagID.Completed) Then
                PanListCompleted.Children.Add(CreateFeedbackItem(item, "Blocks/Grass.png"))
            End If

            If item.Tags.Contains(TagID.Decline) Then
                PanListDecline.Children.Add(CreateFeedbackItem(item, "Blocks/CobbleStone.png"))
            End If

            If item.Tags.Contains(TagID.Ignored) Then
                PanListIgnored.Children.Add(CreateFeedbackItem(item, "Blocks/CobbleStone.png"))
            End If

            If item.Tags.Contains(TagID.Duplicate) Then
                PanListDuplicate.Children.Add(CreateFeedbackItem(item, "Blocks/CobbleStone.png"))
            End If
        Next

        SetPanelVisibility(PanListProcessing, PanContentProcessing)
        SetPanelVisibility(PanListWaitingProcess, PanContentWaitingProcess)
        SetPanelVisibility(PanListWait, PanContentWait)
        SetPanelVisibility(PanListPause, PanContentPause)
        SetPanelVisibility(PanListUpnext, PanContentUpnext)
        SetPanelVisibility(PanListCompleted, PanContentCompleted)
        SetPanelVisibility(PanListDecline, PanContentDecline)
        SetPanelVisibility(PanListIgnored, PanContentIgnored)
        SetPanelVisibility(PanListDuplicate, PanContentDuplicate)
    End Sub

    Private Sub Feedback_Click(sender As Object, e As MouseButtonEventArgs)
        PageSetupLeft.TryFeedback()
    End Sub
End Class

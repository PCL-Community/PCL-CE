Imports PCL.Core.App
Imports PCL.Core.App.Basics
Imports PCL.Core.Utils.OS
Imports PCL.Core.Utils.Exts.StringExtension

#Region "附加属性"

''' <summary>
''' 用于在 XAML 中初始化列表对象。
''' 附加属性无法在 XAML 中为每个对象初始化独立的列表对象，因此需要一个包装类，然后在 XAML 中显式初始化。
''' </summary>
<Markup.ContentProperty("Events")>
Public Class CustomEventCollection
    Implements IEnumerable(Of CustomEvent)
    Dim _events As New List(Of CustomEvent)
    Public ReadOnly Property Events As List(Of CustomEvent)
        Get
            Return _events
        End Get
    End Property
    Public Function GetEnumerator() As IEnumerator(Of CustomEvent) Implements IEnumerable(Of CustomEvent).GetEnumerator
        Return DirectCast(Events, IEnumerable(Of CustomEvent)).GetEnumerator()
    End Function
    Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Return DirectCast(Events, IEnumerable).GetEnumerator()
    End Function
End Class

''' <summary>
''' 提供自定义事件的附加属性。
''' </summary>
Public Class CustomEventService

    'Events
    Public Shared ReadOnly EventsProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("Events", GetType(CustomEventCollection), GetType(CustomEventService), New PropertyMetadata(Nothing))
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Sub SetEvents(d As DependencyObject, value As CustomEventCollection)
        d.SetValue(EventsProperty, value)
    End Sub
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Function GetEvents(d As DependencyObject) As CustomEventCollection
        If d.GetValue(EventsProperty) Is Nothing Then d.SetValue(EventsProperty, New CustomEventCollection)
        Return d.GetValue(EventsProperty)
    End Function

    'EventType
    Public Shared ReadOnly EventTypeProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("EventType", GetType(CustomEvent.EventType), GetType(CustomEventService), New PropertyMetadata(Nothing))
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Sub SetEventType(d As DependencyObject, value As CustomEvent.EventType)
        d.SetValue(EventTypeProperty, value)
    End Sub
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Function GetEventType(d As DependencyObject) As CustomEvent.EventType
        Return d.GetValue(EventTypeProperty)
    End Function

    'EventData
    Public Shared ReadOnly EventDataProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("EventData", GetType(String), GetType(CustomEventService), New PropertyMetadata(Nothing))
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Sub SetEventData(d As DependencyObject, value As String)
        d.SetValue(EventDataProperty, value)
    End Sub
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Function GetEventData(d As DependencyObject) As String
        Return d.GetValue(EventDataProperty)
    End Function

End Class

Partial Public Module ModMain

    ''' <summary>
    ''' 触发该控件上的自定义事件。
    ''' 事件会在新线程中执行。
    ''' </summary>
    <Runtime.CompilerServices.Extension>
    Public Sub RaiseCustomEvent(control As DependencyObject)
        '收集事件列表
        Dim events = CustomEventService.GetEvents(control).ToList
        Dim eventType = CustomEventService.GetEventType(control)
        If eventType <> CustomEvent.EventType.None Then events.Add(New CustomEvent(eventType, CustomEventService.GetEventData(control)))
        '执行事件
        If Not events.Any Then Return
        RunInNewThread(
        Sub()
            For Each e In events
                e.Raise()
            Next
        End Sub, "执行自定义事件 " & GetUuid())
    End Sub

End Module

#End Region

''' <summary>
''' 自定义事件。
''' </summary>
Public Class CustomEvent

#Region "属性与触发"

    Public Property Type As EventType = EventType.None
    Public Property Data As String = Nothing
    Public Sub New()
    End Sub
    Public Sub New(type As EventType, data As String)
        Me.Type = type
        Me.Data = data
    End Sub

    ''' <summary>
    ''' 在当前线程中触发该自定义事件。
    ''' </summary>
    Public Sub Raise()
        Raise(Type, Data)
    End Sub

#End Region
    
    Public Enum EventType
        None = 0
        打开网页
        打开文件
        打开帮助
        执行命令
        启动游戏
        复制文本
        刷新主页
        刷新主页市场
        刷新页面
        刷新帮助
        今日人品
        内存优化
        清理垃圾
        弹出窗口
        弹出提示
        切换页面
        导入整合包
        安装整合包
        下载文件
        修改设置
        写入设置
        修改变量
        写入变量
    End Enum
    
    ''' <summary>
    ''' 在当前线程中触发一个自定义事件。
    ''' </summary>
    Public Shared Sub Raise(type As EventType, arg As String)
        If type = EventType.None Then Return
        Log($"[Control] 执行自定义事件：{type}, {arg}")
        Try
            Dim args As String() = If(arg?.Split("|"), {""})
            Select Case type

                Case EventType.打开网页
                    arg = arg.Replace("\", "/")
                    If Not arg.Contains("://") OrElse arg.StartsWithF("file", True) Then '为了支持更多协议（#2200）
                        MyMsgBox("EventData 必须为一个网址。" & vbCrLf & "如果想要启动程序，请将 EventType 改为 打开文件。", "事件执行失败")
                        Return
                    End If
                    Hint("正在开启中，请稍候：" & arg)
                    RunInThread(Sub() OpenWebsite(arg))

                Case EventType.打开文件, EventType.打开帮助, EventType.执行命令
                    RunInThread(
                    Sub()
                        Try
                            '确认实际路径
                            Dim actualPaths = GetAbsoluteUrls(args(0), type)
                            Dim location = actualPaths(0), workingDir = actualPaths(1)
                            Log($"[Control] 打开类自定义事件实际路径：{location}，工作目录：{workingDir}")
                            '执行
                            If Type = EventType.打开帮助 Then
                                PageToolsHelp.EnterHelpPage(Location)
                            Else
                                If Not EventSafetyConfirm("即将执行：" & location & If(args.Length >= 2, " " & args(1), "")) Then Return
                                ProcessInterop.Start(location, If(args.Length >= 2, args(1), ""))
                            End If
                        Catch ex As Exception
                            Log(ex, "执行打开类自定义事件失败", LogLevel.Msgbox)
                        End Try
                    End Sub)

                Case EventType.启动游戏
                    If args(0) = "\current" Then
                        If McInstanceSelected Is Nothing Then
                            Hint("请先选择一个 Minecraft 版本！", HintType.Critical)
                            Return
                        Else
                            args(0) = McInstanceSelected.Name
                        End If
                    End If
                    RunInUi(
                    Sub()
                        If McLaunchStart(New McLaunchOptions With
                                {.ServerIp = If(args.Length >= 2, args(1), Nothing), .Instance = New McInstance(args(0))}) Then
                            Hint($"正在启动 {args(0)}……")
                        End If
                    End Sub)

                Case EventType.复制文本
                    ClipboardSet(arg)

                Case EventType.刷新主页, EventType.刷新页面
                    If TypeOf FrmMain.PageRight Is IRefreshable Then
                        RunInUiWait(Sub() CType(FrmMain.PageRight, IRefreshable).Refresh())
                        If String.IsNullOrEmpty(arg) Then Hint("已刷新！", HintType.Finish)
                    Else
                        Hint("当前页面不支持刷新操作！", HintType.Critical)
                    End If

                Case EventType.刷新主页市场
                    FrmHomePageMarket.Refresh()
                    If args(0) = "" Then Hint("已刷新主页市场！", HintType.Finish)

                Case EventType.刷新帮助
                    PageToolsLeft.RefreshHelp()
                Case EventType.刷新帮助
                    RunInUiWait(Sub() PageToolsLeft.RefreshHelp())
                    If String.IsNullOrEmpty(arg) Then Hint("已刷新！", HintType.Finish)

                Case EventType.今日人品
                    PageToolsTest.Jrrp()

                Case EventType.内存优化
                    If PageToolsTest.AskTrulyWantMemoryOptimize() Then
                        RunInThread(Sub() PageToolsTest.MemoryOptimize(True))
                    End If

                Case EventType.清理垃圾
                    RunInThread(Sub() PageToolsTest.RubbishClear())

                Case EventType.弹出窗口
                    If args.Length = 1 Then Throw New Exception($"EventType {type} 需要至少 2 个以 | 分割的参数，例如 弹窗标题|弹窗内容")
                    MyMsgBox(args(1).Replace("\n", vbCrLf), args(0).Replace("\n", vbCrLf), If(args.Length > 2, args(2), "确定"))

                Case EventType.弹出提示
                    Hint(args(0).Replace("\n", vbCrLf), If(args.Length = 1, HintType.Info, args(1).ParseToEnum(Of HintType)))

                Case EventType.切换页面
                    RunInUi(Sub() FrmMain.PageChange(
                                args(0).ParseToEnum(Of FormMain.PageType),
                                If(args.Length = 1, FormMain.PageSubType.Default, args(1).ParseToEnum(Of FormMain.PageSubType))))

                Case EventType.导入整合包, EventType.安装整合包
                    RunInUi(Sub() ModpackInstall())

                Case EventType.下载文件
                    args(0) = args(0).Replace("\", "/")
                    If Not (args(0).StartsWithF("http://", True) OrElse args(0).StartsWithF("https://", True)) Then
                        MyMsgBox("EventData 必须为以 http:// 或 https:// 开头的网址。" & vbCrLf & "PCL 不支持其他乱七八糟的下载协议。", "事件执行失败")
                        Return
                    End If
                    If Not EventSafetyConfirm("即将从该网址下载文件：" & vbCrLf & args(0)) Then Return
                    Try
                        Select Case args.Length
                            Case 1
                                PageToolsTest.StartCustomDownload(args(0), GetFileNameFromPath(args(0)))
                            Case 2
                                PageToolsTest.StartCustomDownload(args(0), args(1))
                            Case Else
                                PageToolsTest.StartCustomDownload(args(0), args(1), args(2))
                        End Select
                    Catch
                        PageToolsTest.StartCustomDownload(args(0), "未知")
                    End Try

                Case EventType.修改设置, EventType.写入设置
                    If args.Length = 1 Then Throw New Exception($"EventType {type} 需要至少 2 个以 | 分割的参数，例如 UiLauncherTransparent|400")
                    Setup.SetSafe(args(0), args(1), instance:=McInstanceSelected)
                    If args.Length = 2 Then Hint($"已写入设置：{args(0)} → {args(1)}", HintType.Finish)

                Case EventType.修改变量, EventType.写入变量
                    If args.Length = 1 Then Throw New Exception($"EventType {type} 需要至少 2 个以 | 分割的参数，例如 VariableName|Value")
                    States.CustomVariables.Add(args(0), args(1))
                    States.CustomVariables = States.CustomVariables
                    If args.Length = 2 Then Hint($"已写入变量：{args(0)} → {args(1)}", HintType.Finish)
                Case Else
                    MyMsgBox("未知的事件类型：" & type & vbCrLf & "请检查事件类型填写是否正确，或者 PCL 是否为最新版本。", "事件执行失败")
            End Select
        Catch ex As Exception
            Log(ex, $"事件执行失败（{type}, {arg}）", LogLevel.Msgbox)
        End Try
    End Sub
    
    ''' <summary>
    ''' 获取自定义变量的值。若不存在这个变量则返回 Nothing。
    ''' </summary>
    Public Shared Function GetCustomVariable(name As String) As String
        If States.CustomVariables.ContainsKey(name) Then Return States.CustomVariables(name)
        Return Nothing
    End Function
    
    ''' <summary>
    ''' 返回自定义事件的绝对 Url。实际返回 {绝对 Url, WorkingDir}。
    ''' 失败会抛出异常。
    ''' </summary>
    Public Shared Function GetAbsoluteUrls(relativeUrl As String, type As EventType) As String()
    
        '网页确认
        If relativeUrl.StartsWithF("http", True) Then
            If RunInUi() Then
                Throw New Exception("能打开联网帮助页面的 MyListItem 必须手动设置 Title、Info 属性！")
            End If
            '获取文件名
            Dim rawFileName As String
            Try
                rawFileName = GetFileNameFromPath(relativeUrl)
                If Not rawFileName.EndsWithF(".json", True) Then Throw New Exception("未指向 .json 后缀的文件")
            Catch ex As Exception
                Throw New Exception("联网帮助页面须指向一个帮助 JSON 文件，并在同路径下包含相应 XAML 文件！" & vbCrLf &
                                    "例如：" & vbCrLf &
                                    " - https://www.baidu.com/test.json（填写这个路径）" & vbCrLf &
                                    " - https://www.baidu.com/test.xaml（同时也需要包含这个文件）", ex)
            End Try
            '下载文件
            Dim localTemp As String = RequestTaskTempFolder() & rawFileName
            Log("[Event] 转换网络资源：" & relativeUrl & " -> " & localTemp)
            Try
                NetDownloadByClient(relativeUrl, localTemp).GetAwaiter().GetResult()
                NetDownloadByClient(relativeUrl.Replace(".json", ".xaml"), localTemp.Replace(".json", ".xaml")).GetAwaiter().GetResult()
            Catch ex As Exception
                Throw New Exception("下载指定的文件失败！" & vbCrLf &
                                    "注意，联网帮助页面须指向一个帮助 JSON 文件，并在同路径下包含相应 XAML 文件！" & vbCrLf &
                                    "例如：" & vbCrLf &
                                    " - https://www.baidu.com/test.json（填写这个路径）" & vbCrLf &
                                    " - https://www.baidu.com/test.xaml（同时也需要包含这个文件）", ex)
            End Try
            relativeUrl = localTemp
        End If
        relativeUrl = relativeUrl.Replace("/", "\").ToLower.TrimStart("\")
    
        '确认实际路径
        Dim location As String, workingDir As String = IO.Path.Combine(ExecutableDirectory, "PCL")
        HelpExtract()
        If relativeUrl.Contains(":\") Then
            '绝对路径
            location = relativeUrl
            Log("[Control] 自定义事件中由绝对路径" & type & "：" & location)
        ElseIf File.Exists(IO.Path.Combine(ExecutableDirectory, "PCL", relativeUrl)) Then
            '相对 PCL 文件夹的路径
            location = IO.Path.Combine(ExecutableDirectory, "PCL", relativeUrl)
            Log("[Control] 自定义事件中由相对 PCL 文件夹的路径" & type & "：" & location)
        ElseIf File.Exists(IO.Path.Combine(ExecutableDirectory, "PCL", "Help", relativeUrl)) Then
            '相对 PCL 本地帮助文件夹的路径
            location = IO.Path.Combine(ExecutableDirectory, "PCL", "Help", relativeUrl)
            workingDir = IO.Path.Combine(ExecutableDirectory, "PCL", "Help")
            Log("[Control] 自定义事件中由相对 PCL 本地帮助文件夹的路径" & type & "：" & location)
        ElseIf type = EventType.打开帮助 AndAlso File.Exists(IO.Path.Combine(PathTemp, "Help", relativeUrl)) Then
            '相对 PCL 自带帮助文件夹的路径
            location = IO.Path.Combine(PathTemp, "Help", relativeUrl)
            workingDir = IO.Path.Combine(PathTemp, "Help")
            Log("[Control] 自定义事件中由相对 PCL 自带帮助文件夹的路径" & type & "：" & location)
        ElseIf type = EventType.打开文件 OrElse type = EventType.执行命令 Then
            '直接使用原有路径启动程序
            location = relativeUrl
            Log("[Control] 自定义事件中直接" & type & "：" & location)
        Else
            '打开帮助，但是格式不对劲
            Throw New FileNotFoundException("未找到 EventData 指向的本地 xaml 文件：" & relativeUrl, relativeUrl)
        End If
    
        Return {location, workingDir}
    End Function

    ''' <summary>
    ''' 弹出安全确认弹窗。返回是否继续执行。
    ''' </summary>
    Private Shared Function EventSafetyConfirm(message As String) As Boolean
        If Setup.Get("HintCustomCommand") Then Return True
        Select Case MyMsgBox(message & vbCrLf & "请在确认没有安全隐患后再继续。", "执行确认", "继续", "继续且今后不再要求确认", "取消")
            Case 1
                Return True
            Case 2
                Setup.Set("HintCustomCommand", True)
                Return True
            Case Else
                Return False
        End Select
    End Function

End Class

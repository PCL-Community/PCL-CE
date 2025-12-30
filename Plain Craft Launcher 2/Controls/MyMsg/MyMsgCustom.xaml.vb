Imports PCL.Core.UI.Controls

Public Class MyMsgCustom

    Private _MyConverter As MyMsgBoxConverter
    Public ReadOnly Property MyConverter As MyMsgBoxConverter
        Get
            Return _MyConverter
        End Get
    End Property
    Private ReadOnly Uuid As Integer = GetUuid()
    Private ReadOnly ButtonList As New List(Of MyButton)
    Private ContentInput As MyMsgContentInput = Nothing
    Private ContentSelect As MyMsgContentSelect = Nothing

    Public Sub New(Converter As MyMsgBoxConverter)
        Try

            InitializeComponent()
            _MyConverter = Converter
            LabTitle.Text = Converter.Title

            ' 嵌入自定义内容
            If Converter.CustomContent IsNot Nothing Then
                ContentPresenter.Content = Converter.CustomContent

                ' 保存特殊内容控件的引用，以便后续处理
                If TypeOf Converter.CustomContent Is MyMsgContentInput Then
                    ContentInput = CType(Converter.CustomContent, MyMsgContentInput)
                    ' 监听验证状态变化
                    AddHandler ContentInput.ValidateChanged, AddressOf Input_ValidateChanged
                ElseIf TypeOf Converter.CustomContent Is MyMsgContentSelect Then
                    ContentSelect = CType(Converter.CustomContent, MyMsgContentSelect)
                    ' 监听选择变化
                    AddHandler ContentSelect.SelectionChanged, AddressOf Select_SelectionChanged
                End If
            End If

            ' 设置警告样式
            If Converter.IsWarn Then
                LabTitle.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushRedLight")
            End If

            ' 动态生成按钮
            Dim buttonsToUse As List(Of String) = Nothing
            If Converter.Buttons IsNot Nothing AndAlso Converter.Buttons.Count > 0 Then
                ' 使用 Buttons 列表
                buttonsToUse = Converter.Buttons
            Else
                ' 回退到 Button1/Button2/Button3
                buttonsToUse = New List(Of String)
                If Not String.IsNullOrEmpty(Converter.Button1) Then buttonsToUse.Add(Converter.Button1)
                If Not String.IsNullOrEmpty(Converter.Button2) Then buttonsToUse.Add(Converter.Button2)
                If Not String.IsNullOrEmpty(Converter.Button3) Then buttonsToUse.Add(Converter.Button3)
            End If

            ' 创建按钮
            For i As Integer = 0 To buttonsToUse.Count - 1
                Dim btn As New MyButton With {
                    .Text = buttonsToUse(i),
                    .ColorType = MyButton.ColorState.Normal,
                    .TextPadding = New Thickness(7),
                    .Padding = New Thickness(5, 0, 5, 0),
                    .Margin = New Thickness(If(i = 0, 0, 12), 0, 0, 0),
                    .SnapsToDevicePixels = False,
                    .UseLayoutRounding = False
                }
                btn.Name = "Btn" & (i + 1) & "_" & GetUuid()

                ' 第一个按钮的特殊处理
                If i = 0 Then
                    If Converter.IsWarn Then
                        btn.ColorType = MyButton.ColorState.Red
                    ElseIf buttonsToUse.Count > 1 Then
                        btn.ColorType = MyButton.ColorState.Highlight
                    End If
                End If

                ' 绑定点击事件
                Dim buttonIndex As Integer = i + 1
                AddHandler btn.Click, Sub()
                                          If _MyConverter.IsExited Then Return

                                          ' 处理特殊内容控件的逻辑
                                          If buttonIndex = 1 Then
                                              ' 第一个按钮的特殊处理
                                              If ContentInput IsNot Nothing Then
                                                  ' Input 模式：需要验证
                                                  Dim result = ContentInput.GetResult()
                                                  If result Is Nothing Then Return ' 验证失败，不关闭
                                                  _MyConverter.IsExited = True
                                                  _MyConverter.Result = result
                                                  Close()
                                                  Return
                                              ElseIf ContentSelect IsNot Nothing Then
                                                  ' Select 模式：需要检查是否已选择
                                                  If Not ContentSelect.HasSelection Then Return ' 未选择，不关闭
                                                  _MyConverter.IsExited = True
                                                  _MyConverter.Result = ContentSelect.GetSelectedIndex
                                                  Close()
                                                  Return
                                              End If
                                          ElseIf buttonIndex = 2 AndAlso ContentInput IsNot Nothing Then
                                              ' Input 模式的取消按钮
                                              _MyConverter.IsExited = True
                                              _MyConverter.Result = Nothing
                                              Close()
                                              Return
                                          ElseIf buttonIndex = 2 AndAlso ContentSelect IsNot Nothing Then
                                              ' Select 模式的取消按钮
                                              _MyConverter.IsExited = True
                                              _MyConverter.Result = Nothing
                                              Close()
                                              Return
                                          End If

                                          ' 检查是否有对应的 Action
                                          Dim action As Action = Nothing
                                          Select Case buttonIndex
                                              Case 1
                                                  action = _MyConverter.Button1Action
                                              Case 2
                                                  action = _MyConverter.Button2Action
                                              Case 3
                                                  action = _MyConverter.Button3Action
                                          End Select

                                          If action IsNot Nothing Then
                                              action()
                                          Else
                                              _MyConverter.IsExited = True
                                              _MyConverter.Result = buttonIndex
                                              Close()
                                          End If
                                      End Sub

                PanBtn.Children.Add(btn)
                ButtonList.Add(btn)
            Next

            ' 设置按钮的初始状态
            If ContentInput IsNot Nothing AndAlso ButtonList.Count > 0 Then
                ButtonList(0).IsEnabled = ContentInput.IsValidated
            ElseIf ContentSelect IsNot Nothing AndAlso ButtonList.Count > 0 Then
                ButtonList(0).IsEnabled = False
            End If

            ShapeLine.StrokeThickness = GetWPFSize(1)

        Catch ex As Exception
            Log(ex, "自定义弹窗初始化失败", LogLevel.Hint)
        End Try
    End Sub

    Private Sub Input_ValidateChanged(sender As Object, e As EventArgs)
        ' Input 验证状态改变时，更新第一个按钮的启用状态
        If ButtonList.Count > 0 AndAlso ContentInput IsNot Nothing Then
            ButtonList(0).IsEnabled = ContentInput.IsValidated
        End If
    End Sub

    Private Sub Select_SelectionChanged(sender As Object, e As EventArgs)
        ' Select 选择改变时，启用第一个按钮
        If ButtonList.Count > 0 AndAlso ContentSelect IsNot Nothing Then
            ButtonList(0).IsEnabled = ContentSelect.HasSelection
        End If
    End Sub

    Private Sub Load(sender As Object, e As EventArgs) Handles MyBase.Loaded
        Try

            'UI 初始化
            ' 根据内容类型决定焦点
            If ContentInput IsNot Nothing Then
                ContentInput.FocusInput()
                ' 初始化按钮状态
                If ButtonList.Count > 0 Then
                    ButtonList(0).IsEnabled = ContentInput.IsValidated
                End If
            ElseIf ContentSelect IsNot Nothing Then
                ' Select 模式：初始时第一个按钮禁用
                If ButtonList.Count > 0 Then
                    ButtonList(0).IsEnabled = False
                End If
                If ButtonList.Count > 0 Then ButtonList(0).Focus()
            ElseIf ButtonList.Count > 0 Then
                ButtonList(0).Focus()
            End If
            '动画
            Opacity = 0
            AniStart(AaColor(FrmMain.PanMsgBackground, BlurBorder.BackgroundProperty, If(_MyConverter.IsWarn, New MyColor(140, 80, 0, 0), New MyColor(90, 0, 0, 0)) - FrmMain.PanMsgBackground.Background, 200), "PanMsgBackground Background")
            AniStart({
                AaOpacity(Me, 1, 120, 60),
                AaDouble(Sub(i) TransformPos.Y += i, -TransformPos.Y, 300, 60, New AniEaseOutBack(AniEasePower.Weak)),
                AaDouble(Sub(i) TransformRotate.Angle += i, -TransformRotate.Angle, 300, 60, New AniEaseOutFluent(AniEasePower.Weak))
            }, "MyMsgBox " & Uuid)
            '记录日志
            Log("[Control] 自定义弹窗：" & LabTitle.Text)

        Catch ex As Exception
            Log(ex, "自定义弹窗加载失败", LogLevel.Hint)
        End Try
    End Sub

    Private Sub Close()
        '结束线程阻塞
        If _MyConverter.ForceWait OrElse ButtonList.Count > 1 Then _MyConverter.WaitFrame.Continue = False
        Interop.ComponentDispatcher.PopModal()
        '动画
        AniStart({
            AaCode(
            Sub()
                If Not WaitingMyMsgBox.Any() Then
                    AniStart(AaColor(FrmMain.PanMsgBackground, BlurBorder.BackgroundProperty, New MyColor(0, 0, 0, 0) - FrmMain.PanMsgBackground.Background, 200, Ease:=New AniEaseOutFluent(AniEasePower.Weak)))
                End If
            End Sub, 30),
            AaOpacity(Me, -Opacity, 80, 20),
            AaDouble(Sub(i) TransformPos.Y += i, 20 - TransformPos.Y, 150, 0, New AniEaseOutFluent),
            AaDouble(Sub(i) TransformRotate.Angle += i, 6 - TransformRotate.Angle, 150, 0, New AniEaseInFluent(AniEasePower.Weak)),
            AaCode(Sub() CType(Parent, Grid).Children.Remove(Me), , True)
        }, "MyMsgBox " & Uuid)
    End Sub

    Private Sub Drag(sender As Object, e As MouseButtonEventArgs) Handles PanBorder.MouseLeftButtonDown, LabTitle.MouseLeftButtonDown
        Try
            If e.LeftButton = MouseButtonState.Pressed Then
                If e.GetPosition(ShapeLine).Y <= 2 Then
                    FrmMain.DragMove()
                End If
            End If
        Catch ex As Exception
            Log(ex, "拖拽移动失败", LogLevel.Hint)
        End Try
    End Sub

    ' 向前兼容：提供按钮点击方法供外部调用（如键盘快捷键）
    Public Sub Btn1_Click()
        If ButtonList.Count > 0 AndAlso ButtonList(0).IsEnabled Then
            ButtonList(0).RaiseEvent(New RoutedEventArgs(Button.ClickEvent))
        End If
    End Sub

    Public Sub Btn2_Click()
        If ButtonList.Count > 1 AndAlso ButtonList(1).IsEnabled Then
            ButtonList(1).RaiseEvent(New RoutedEventArgs(Button.ClickEvent))
        End If
    End Sub

    Public Sub Btn3_Click()
        If ButtonList.Count > 2 AndAlso ButtonList(2).IsEnabled Then
            ButtonList(2).RaiseEvent(New RoutedEventArgs(Button.ClickEvent))
        End If
    End Sub

    ' 向前兼容：提供按钮可见性属性
    Public ReadOnly Property Btn2Visibility As Visibility
        Get
            If ButtonList.Count > 1 Then
                Return ButtonList(1).Visibility
            End If
            Return Visibility.Collapsed
        End Get
    End Property

    Public ReadOnly Property Btn3Visibility As Visibility
        Get
            If ButtonList.Count > 2 Then
                Return ButtonList(2).Visibility
            End If
            Return Visibility.Collapsed
        End Get
    End Property

End Class


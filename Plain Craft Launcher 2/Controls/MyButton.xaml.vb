Imports System.Windows.Markup
Imports PCL.Core.UI
Imports PCL.Core.UI.Animation
Imports PCL.Core.UI.Animation.Animatable
Imports PCL.Core.UI.Animation.Core
Imports PCL.Core.UI.Animation.Easings

<ContentProperty("Inlines")>
Public Class MyButton

    '声明
    Public Event Click(sender As Object, e As MouseButtonEventArgs) '自定义事件

    '自定义属性
    Public Uuid As Integer = GetUuid()
    Public ReadOnly Property Inlines As InlineCollection
        Get
            Return LabText.Inlines
        End Get
    End Property
    Public Property Text As String
        Get
            Return GetValue(TextProperty)
        End Get
        Set(value As String)
            SetValue(TextProperty, value)
        End Set
    End Property '显示文本
    Public Shared ReadOnly TextProperty As DependencyProperty = DependencyProperty.Register("Text", GetType(String), GetType(MyButton), New PropertyMetadata(New PropertyChangedCallback(
    Sub(sender As DependencyObject, e As DependencyPropertyChangedEventArgs)
        If sender IsNot Nothing Then CType(sender, MyButton).LabText.Text = e.NewValue
    End Sub)))
    Public Property TextPadding As Thickness
        Get
            Return LabText.Padding
        End Get
        Set(value As Thickness)
            LabText.Padding = value
        End Set
    End Property
    Private _ColorType As ColorState = ColorState.Normal  '配色方案
    Public Property ColorType As ColorState
        Get
            Return _ColorType
        End Get
        Set(value As ColorState)
            _ColorType = value
            RefreshColor()
        End Set
    End Property
    Public Enum ColorState
        Normal = 0
        Highlight = 1
        Red = 2
    End Enum
    '属性穿透
    Public Shared Shadows ReadOnly PaddingProperty As DependencyProperty = DependencyProperty.Register("Padding", GetType(Thickness), GetType(MyButton), New PropertyMetadata(New PropertyChangedCallback(
        Sub(sender As MyButton, e As DependencyPropertyChangedEventArgs) If sender IsNot Nothing Then sender.PanFore.Padding = e.NewValue)))
    Public Overloads Property Padding As Thickness
        Get
            Return PanFore.Padding
        End Get
        Set(value As Thickness)
            PanFore.Padding = value
        End Set
    End Property
    Public Property RealRenderTransform As Transform
        Get
            Return PanFore.RenderTransform
        End Get
        Set(value As Transform)
            PanFore.RenderTransform = value
        End Set
    End Property

    '自定义事件
    Private ReadOnly _animationColorIn = TimeSpan.FromMilliseconds(100)
    Private Readonly _animationColorOut = TimeSpan.FromMilliseconds(200)
    Private Sub RefreshColor(Optional obj = Nothing, Optional e = Nothing) Handles Me.MouseEnter, Me.MouseLeave, Me.Loaded, Me.IsEnabledChanged
        Try
            Dim animation = New NColorFromToAnimation
            animation.ValueType = AnimationValueType.Absolute
            Dim animatable = New WpfAnimatable(PanFore, BorderBrushProperty)
            
            If IsLoaded AndAlso AniControlEnabled = 0 Then '防止默认属性变更触发动画

                If IsEnabled Then
                    Select Case ColorType
                        Case ColorState.Normal
                            If IsMouseOver Then
                                '指向（Main 3）
                                'AniStart({AaColor(PanFore, Border.BorderBrushProperty, "ColorBrush3", AnimationColorIn)}, "MyButton Color " & Uuid)
                                animation.To = New NColor("ColorBrush3")
                                animation.Duration = _animationColorIn
                                animation.RunFireAndForget(animatable)
                            Else
                                '普通（Main 1）
                                'AniStart({AaColor(PanFore, Border.BorderBrushProperty, "ColorBrush1", AnimationColorOut)}, "MyButton Color " & Uuid)
                                animation.To = New NColor("ColorBrush1")
                                animation.Duration = _animationColorOut
                                animation.RunFireAndForget(animatable)
                            End If
                        Case ColorState.Highlight
                            If IsMouseOver Then
                                '指向（Main 3）
                                'AniStart({AaColor(PanFore, Border.BorderBrushProperty, "ColorBrush3", AnimationColorIn)}, "MyButton Color " & Uuid)
                                animation.To = New NColor("ColorBrush3")
                                animation.Duration = _animationColorIn
                                animation.RunFireAndForget(animatable)
                            Else
                                '高亮（Main 2）
                                'AniStart({AaColor(PanFore, Border.BorderBrushProperty, "ColorBrush2", AnimationColorOut)}, "MyButton Color " & Uuid)
                                animation.To = New NColor("ColorBrush2")
                                animation.Duration = _animationColorOut
                                animation.RunFireAndForget(animatable)
                            End If
                        Case ColorState.Red
                            If IsMouseOver Then
                                '红色指向
                                'AniStart({AaColor(PanFore, Border.BorderBrushProperty, "ColorBrushRedLight", AnimationColorIn)}, "MyButton Color " & Uuid)
                                animation.To = New NColor("ColorBrushRedLight")
                                animation.Duration = _animationColorIn
                                animation.RunFireAndForget(animatable)
                            Else
                                '红色
                                'AniStart({AaColor(PanFore, Border.BorderBrushProperty, "ColorBrushRedDark", AnimationColorOut)}, "MyButton Color " & Uuid)
                                animation.To = New NColor("ColorBrushRedDark")
                                animation.Duration = _animationColorOut
                                animation.RunFireAndForget(animatable)
                            End If
                    End Select
                Else
                    '不可用（Gray 4）
                    'AniStart({AaColor(PanFore, Border.BorderBrushProperty, ColorGray4 - PanFore.BorderBrush, AnimationColorOut)}, "MyButton Color " & Uuid)
                    animation.To = New NColor("ColorGray4")
                    animation.Duration = _animationColorOut
                    animation.RunFireAndForget(animatable)
                End If
            Else
                'AniStop("MyButton Color " & Uuid)
                animation.Cancel()
                If IsEnabled Then
                    Select Case ColorType
                        Case ColorState.Normal
                            If IsMouseOver Then
                                PanFore.SetResourceReference(BorderBrushProperty, "ColorBrush3")
                            Else
                                PanFore.SetResourceReference(BorderBrushProperty, "ColorBrush1")
                            End If
                        Case ColorState.Highlight
                            If IsMouseOver Then
                                PanFore.SetResourceReference(BorderBrushProperty, "ColorBrush3")
                            Else
                                PanFore.SetResourceReference(BorderBrushProperty, "ColorBrush2")
                            End If
                        Case ColorState.Red
                            If IsMouseOver Then
                                PanFore.SetResourceReference(BorderBrushProperty, "ColorBrushRedLight")
                            Else
                                PanFore.SetResourceReference(BorderBrushProperty, "ColorBrushRedDark")
                            End If
                    End Select
                Else
                    PanFore.BorderBrush = ColorGray4
                End If

            End If
        Catch ex As Exception
            Log(ex, "刷新按钮颜色出错")
        End Try
    End Sub

    '实现自定义事件
    Private Sub Button_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonUp
        If Not IsMouseDown Then Return
        Log("[Control] 按下按钮：" & Text)
        RaiseEvent Click(sender, e)
        If Not String.IsNullOrEmpty(Tag) Then
            If Tag.ToString.StartsWithF("链接-") OrElse Tag.ToString.StartsWithF("启动-") Then
                Hint("主页自定义按钮语法已更新，且不再兼容老版本语法，请查看新的自定义示例！")
            End If
        End If
        ModEvent.TryStartEvent(EventType, EventData)
    End Sub
    Public Property EventType As String
        Get
            Return GetValue(EventTypeProperty)
        End Get
        Set(value As String)
            SetValue(EventTypeProperty, value)
        End Set
    End Property
    Public Shared ReadOnly EventTypeProperty As DependencyProperty = DependencyProperty.Register("EventType", GetType(String), GetType(MyButton), New PropertyMetadata(Nothing))
    Public Property EventData As String
        Get
            Return GetValue(EventDataProperty)
        End Get
        Set(value As String)
            SetValue(EventDataProperty, value)
        End Set
    End Property
    Public Shared ReadOnly EventDataProperty As DependencyProperty = DependencyProperty.Register("EventData", GetType(String), GetType(MyButton), New PropertyMetadata(Nothing))

    '鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    Private IsMouseDown As Boolean = False
    Private Sub Button_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonDown
        IsMouseDown = True
        Focus()
'        AniStart({
'                 AaScaleTransform(PanFore, 0.955 - CType(PanFore.RenderTransform, ScaleTransform).ScaleX, 80,, New AniEaseOutFluent(AniEasePower.ExtraStrong)),
'                 AaScaleTransform(PanFore, -0.01, 700,, New AniEaseOutFluent(AniEasePower.Middle))
'                 }, "MyButton Scale " & Uuid)
        Dim animation = New NScaleTransformFromToAnimation _
                With {.To = New NScaleTransform(0.954, 0.954),
                .Duration = TimeSpan.FromMilliseconds(80),
                .Easing = ExponentialEaseOut.Shared,
                .ValueType = AnimationValueType.Absolute}
        animation.RunFireAndForget(new WpfAnimatable(PanFore, RenderTransformProperty))
    End Sub
    Private Sub Button_MouseEnter() Handles Me.MouseEnter
'        AniStart(AaColor(PanFore, BackgroundProperty, If(_ColorType = ColorState.Red, "ColorBrushRedBack", "ColorBrush7"), AnimationColorIn), "MyButton Background " & Uuid)
        Dim animation = New NColorFromToAnimation
        animation.ValueType = AnimationValueType.Absolute
        animation.To = New NColor(If(_ColorType = ColorState.Red, "ColorBrushRedBack", "ColorBrush7"))
        animation.Duration = _animationColorIn
        animation.RunFireAndForget(New WpfAnimatable(PanFore, BackgroundProperty))
    End Sub
    Private Sub Button_MouseUp() Handles Me.MouseLeftButtonUp
        If Not IsMouseDown Then Return
        IsMouseDown = False
'        AniStart({
'               AaScaleTransform(PanFore, 1 - CType(PanFore.RenderTransform, ScaleTransform).ScaleX, 300, 10, New AniEaseOutFluent(AniEasePower.Middle))
'           }, "MyButton Scale " & Uuid)
        Dim animation = New NScaleTransformFromToAnimation()
        animation.To = New NScaleTransform(1, 1)
        animation.Duration = TimeSpan.FromMilliseconds(300)
        animation.Delay = TimeSpan.FromMilliseconds(10)
        animation.ValueType = AnimationValueType.Absolute
        animation.Easing = CubicEaseOut.Shared
        animation.RunFireAndForget(new WpfAnimatable(PanFore, RenderTransformProperty))
    End Sub
    Private Sub Button_MouseLeave() Handles Me.MouseLeave
'        AniStart(AaColor(PanFore, BackgroundProperty, "ColorBrushHalfWhite", AnimationColorOut), "MyButton Background " & Uuid)
        Dim animation1 = New NColorFromToAnimation
        animation1.ValueType = AnimationValueType.Absolute
        animation1.To = New NColor("ColorBrushHalfWhite")
        animation1.Duration = _animationColorOut
        animation1.RunFireAndForget(New WpfAnimatable(PanFore, BackgroundProperty))
        If Not IsMouseDown Then Return
        IsMouseDown = False
'        AniStart(AaScaleTransform(PanFore, 1 - CType(PanFore.RenderTransform, ScaleTransform).ScaleX, 800,, New AniEaseOutFluent(AniEasePower.Strong)), "MyButton Scale " & Uuid)
        Dim animation2 = New NScaleTransformFromToAnimation()
        animation2.To = New NScaleTransform(1, 1, 0.5, 0.5)
        animation2.Duration = TimeSpan.FromMilliseconds(800)
        animation2.Easing = QuinticEaseOut.Shared
        animation2.RunFireAndForget(new WpfAnimatable(PanFore, RenderTransformProperty))
    End Sub

End Class

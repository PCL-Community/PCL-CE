Imports System.Windows.Markup
Imports PCL.Core.UI
Imports PCL.Core.UI.Animation
Imports PCL.Core.UI.Animation.Animatable
Imports PCL.Core.UI.Animation.Core
Imports PCL.Core.UI.Animation.Easings

<ContentProperty("Inlines")>
Public Class MyExtraTextButton

    '声明
    Public Event Click(sender As Object, e As MouseButtonEventArgs) '自定义事件

    '自定义属性
    Public Uuid As Integer = GetUuid()
    Private _Logo As String = ""
    Public Property Logo As String
        Get
            Return _Logo
        End Get
        Set(value As String)
            If value = _Logo Then Return
            _Logo = value
            Path.Data = (New GeometryConverter).ConvertFromString(value)
        End Set
    End Property
    Private _LogoScale As Double = 1
    Public Property LogoScale() As Double
        Get
            Return _LogoScale
        End Get
        Set(value As Double)
            _LogoScale = value
            If Path IsNot Nothing Then Path.RenderTransform = New ScaleTransform With {.ScaleX = LogoScale, .ScaleY = LogoScale}
        End Set
    End Property
    '显示文本
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
    End Property
    Public Shared ReadOnly TextProperty As DependencyProperty = DependencyProperty.Register("Text", GetType(String), GetType(MyExtraTextButton), New PropertyMetadata(New PropertyChangedCallback(
    Sub(sender As DependencyObject, e As DependencyPropertyChangedEventArgs)
        If sender IsNot Nothing Then CType(sender, MyExtraTextButton).LabText.Text = e.NewValue
    End Sub)))

    '动画
    Private _Show As Boolean = False
    Public Property Show As Boolean
        Get
            Return _Show
        End Get
        Set(value As Boolean)
            If _Show = value Then Return
            _Show = value
            AnimationService.CancelAnimationByName("MyExtraTextButton Scale " & Uuid)
'            RunInUi(
'            Sub()
'                If value Then
'                    '有了
'                    Opacity = 0
'                    AniStart({
'                        AaOpacity(Me, 1 - Opacity, 80, 50),
'                        AaScaleTransform(Me, 0.15 - CType(RenderTransform, ScaleTransform).ScaleX, 400, 50, New AniEaseOutBack),
'                        AaScaleTransform(Me, 0.85, 160, 50, New AniEaseOutFluent(AniEasePower.Middle))
'                    }, "MyExtraTextButton MainScale " & Uuid)
'                Else
'                    '没了
'                    AniStart({
'                        AaOpacity(Me, -Opacity, 50, 50),
'                        AaScaleTransform(Me, -CType(RenderTransform, ScaleTransform).ScaleX, 100,, New AniEaseInFluent(AniEasePower.Weak))
'                    }, "MyExtraTextButton MainScale " & Uuid)
'                End If
'                IsHitTestVisible = value '防止缩放动画中依然可以点进去
'            End Sub)
            AnimationService.UIAccessProvider.Invoke(Sub()
                If value Then
                    '有了
                    Opacity = 0
                    Dim animation = New ParallelAnimationGroup
                    animation.Name = "MyExtraTextButton MainScale " & Uuid
                    
                    Dim aniOpacity = New DoubleFromToAnimation
                    aniOpacity.To = 1
                    aniOpacity.Duration = TimeSpan.FromMilliseconds(80)
                    aniOpacity.Delay = TimeSpan.FromMilliseconds(50)
                    aniOpacity.SetValue(AnimationExtensions.TargetProperty, Me)
                    aniOpacity.SetValue(AnimationExtensions.TargetPropertyProperty, OpacityProperty)
                    animation.Children.Add(aniOpacity)
                    
                    Dim aniScale = New NScaleTransformFromToAnimation
                    aniScale.To = New NScaleTransform(1, 1, 0.5, 0.5)
                    aniScale.Easing = New CompositeEasing((New BackEaseWithPowerOut(), TimeSpan.FromMilliseconds(400), 0.15),
                                                          (CubicEaseOut.Shared, TimeSpan.FromMilliseconds(160), 0.85))
                    aniScale.Duration = TimeSpan.FromMilliseconds(400)
                    aniScale.Delay = TimeSpan.FromMilliseconds(60)
                    aniScale.SetValue(AnimationExtensions.TargetProperty, Me)
                    aniScale.SetValue(AnimationExtensions.TargetPropertyProperty, RenderTransformProperty)
                    animation.Children.Add(aniScale)
                    
                    animation.RunFireAndForget(EmptyAnimatable.Instance)
                Else 
                    '没了
                    Dim animation = New ParallelAnimationGroup
                    animation.Name = "MyExtraTextButton MainScale " & Uuid
                    
                    Dim aniOpacity = New DoubleFromToAnimation
                    aniOpacity.To = 0
                    aniOpacity.Duration = TimeSpan.FromMilliseconds(50)
                    aniOpacity.Delay = TimeSpan.FromMilliseconds(50)
                    aniOpacity.SetValue(AnimationExtensions.TargetProperty, Me)
                    aniOpacity.SetValue(AnimationExtensions.TargetPropertyProperty, OpacityProperty)
                    animation.Children.Add(aniOpacity)
                    
                    Dim aniScale = New NScaleTransformFromToAnimation
                    aniScale.To = New NScaleTransform(0, 0, 0.5, 0.5)
                    aniScale.Easing = QuadEaseIn.Shared
                    aniScale.Duration = TimeSpan.FromMilliseconds(100)
                    aniScale.SetValue(AnimationExtensions.TargetProperty, Me)
                    aniScale.SetValue(AnimationExtensions.TargetPropertyProperty, RenderTransformProperty)
                    animation.Children.Add(aniScale)
                    
                    animation.RunFireAndForget(EmptyAnimatable.Instance)
                End If
                End Sub)
        End Set
    End Property

    '触发点击事件
    Private Sub Button_LeftMouseUp(sender As Object, e As MouseButtonEventArgs) Handles PanClick.MouseLeftButtonUp
        If IsLeftMouseHeld Then
            Log("[Control] 按下附加图标按钮：" & Text)
            RaiseEvent Click(sender, e)
            e.Handled = True
            Button_LeftMouseUp()
        End If
    End Sub

    '鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    Private IsLeftMouseHeld As Boolean = False
    Private Sub Button_LeftMouseDown(sender As Object, e As MouseButtonEventArgs) Handles PanClick.MouseLeftButtonDown
        If Not IsLeftMouseHeld Then
'            AniStart({
'                AaScaleTransform(PanScale, 0.85 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 800,, New AniEaseOutFluent(AniEasePower.Strong)),
'                AaScaleTransform(PanScale, -0.05, 60,, New AniEaseOutFluent)
'            }, "MyExtraTextButton Scale " & Uuid)
            Dim aniScale = New NScaleTransformFromToAnimation
            aniScale.Name = "MyExtraTextButton Scale " & Uuid
            aniScale.To = New NScaleTransform(0.8, 0.8, 0.5, 0.5)
            aniScale.Easing = QuinticEaseOut.Shared
            aniScale.Duration = TimeSpan.FromMilliseconds(800)
            aniScale.RunFireAndForget(New WpfAnimatable(Me, RenderTransformProperty))
        End If
        IsLeftMouseHeld = True
        Focus()
    End Sub
    Private Sub Button_LeftMouseUp() Handles PanClick.MouseLeftButtonUp
'        AniStart({
'            AaScaleTransform(PanScale, 1 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 300,, New AniEaseOutBack)
'        }, "MyExtraTextButton Scale " & Uuid)
        Dim aniScale = New NScaleTransformFromToAnimation
        aniScale.Name = "MyExtraTextButton Scale " & Uuid
        aniScale.To = New NScaleTransform(1, 1, 0.5, 0.5)
        aniScale.Easing = New BackEaseWithPowerOut()
        aniScale.Duration = TimeSpan.FromMilliseconds(300)
        aniScale.RunFireAndForget(New WpfAnimatable(Me, RenderTransformProperty))
        IsLeftMouseHeld = False
        RefreshColor() '直接刷新颜色以判断是否已触发 MouseLeave
    End Sub
    Private Sub Button_RightMouseUp() Handles PanClick.MouseRightButtonUp
        If Not IsLeftMouseHeld Then
'            AniStart({
'                AaScaleTransform(PanScale, 1 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 300,, New AniEaseOutBack)
'            }, "MyExtraTextButton Scale " & Uuid)
            Dim aniScale = New NScaleTransformFromToAnimation
            aniScale.Name = "MyExtraTextButton Scale " & Uuid
            aniScale.To = New NScaleTransform(1, 1, 0.5, 0.5)
            aniScale.Easing = New BackEaseWithPowerOut()
            aniScale.Duration = TimeSpan.FromMilliseconds(300)
            aniScale.RunFireAndForget(New WpfAnimatable(Me, RenderTransformProperty))
        End If
        RefreshColor() '直接刷新颜色以判断是否已触发 MouseLeave
    End Sub
    Private Sub Button_MouseLeave() Handles PanClick.MouseLeave
        IsLeftMouseHeld = False
'        AniStart({
'            AaScaleTransform(PanScale, 1 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 500,, New AniEaseOutFluent)
'        }, "MyExtraTextButton Scale " & Uuid)
        Dim aniScale = New NScaleTransformFromToAnimation
        aniScale.Name = "MyExtraTextButton Scale " & Uuid
        aniScale.To = New NScaleTransform(1, 1, 0.5, 0.5)
        aniScale.Easing = QuadEaseOut.Shared
        aniScale.Duration = TimeSpan.FromMilliseconds(500)
        aniScale.RunFireAndForget(New WpfAnimatable(Me, RenderTransformProperty))
        RefreshColor() '直接刷新颜色以判断是否已触发 MouseLeave
    End Sub

    '自定义事件
    '务必放在 IsMouseDown 更新之后
    Private Const AnimationColorIn As Integer = 120
    Private Const AnimationColorOut As Integer = 150
    Public Sub RefreshColor() Handles PanClick.MouseEnter, PanClick.MouseLeave, Me.Loaded, Me.IsEnabledChanged
        Try
            If IsLoaded AndAlso AniControlEnabled = 0 Then '防止默认属性变更触发动画

                Dim aniColor = New NColorFromToAnimation
                aniColor.Name = "MyExtraTextButton Color " & Uuid
                If Not IsEnabled Then
                    '禁用
'                    AniStart(AaColor(PanColor, BackgroundProperty, "ColorBrushGray4", AnimationColorIn), "MyExtraTextButton Color " & Uuid)
                    aniColor.To = New NColor("ColorBrushGray4")
                    aniColor.Duration = TimeSpan.FromMilliseconds(AnimationColorIn)
                    aniColor.RunFireAndForget(New WpfAnimatable(PanColor, BackgroundProperty))
                ElseIf IsMouseOver Then
                    '指向
'                    AniStart(AaColor(PanColor, BackgroundProperty, "ColorBrush4", AnimationColorIn), "MyExtraTextButton Color " & Uuid)
                    aniColor.To = New NColor("ColorBrush4")
                    aniColor.Duration = TimeSpan.FromMilliseconds(AnimationColorIn)
                    aniColor.RunFireAndForget(New WpfAnimatable(PanColor, BackgroundProperty))
                Else
                    '普通
'                    AniStart(AaColor(PanColor, BackgroundProperty, "ColorBrush3", AnimationColorOut), "MyExtraTextButton Color " & Uuid)
                    aniColor.To = New NColor("ColorBrush3")
                    aniColor.Duration = TimeSpan.FromMilliseconds(AnimationColorOut)
                    aniColor.RunFireAndForget(New WpfAnimatable(PanColor, BackgroundProperty))
                End If

            Else

'                AniStop("MyExtraTextButton Color " & Uuid)
                AnimationService.CancelAnimationByName("MyExtraTextButton Color " & Uuid)
                If Not IsEnabled Then
                    PanColor.SetResourceReference(BackgroundProperty, "ColorBrushGray4")
                ElseIf IsMouseOver Then
                    PanColor.SetResourceReference(BackgroundProperty, "ColorBrush4")
                Else
                    PanColor.SetResourceReference(BackgroundProperty, "ColorBrush3")
                End If

            End If
        Catch ex As Exception
            Log(ex, "刷新附加图标按钮颜色出错")
        End Try
    End Sub

End Class

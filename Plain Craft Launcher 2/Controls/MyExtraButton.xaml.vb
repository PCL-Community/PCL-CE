Imports PCL.Core.UI
Imports PCL.Core.UI.Animation
Imports PCL.Core.UI.Animation.Animatable
Imports PCL.Core.UI.Animation.Core
Imports PCL.Core.UI.Animation.Easings

Public Class MyExtraButton

    '声明
    Public Event Click(sender As Object, e As MouseButtonEventArgs) '自定义事件
    Public Event RightClick(sender As Object, e As MouseButtonEventArgs)

    '进度条
    Private _Progress As Double = 0
    Public Property Progress As Double
        Get
            Return _Progress
        End Get
        Set(value As Double)
            If _Progress = value Then Return
            _Progress = value
            If value < 0.0001 Then
                PanProgress.Visibility = Visibility.Collapsed
            Else
                PanProgress.Visibility = Visibility.Visible
                RectProgress.Rect = New Rect(0, 40 * (1 - value), 40, 40 * value)
            End If
        End Set
    End Property

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
            If Not IsNothing(Path) Then Path.RenderTransform = New ScaleTransform With {.ScaleX = LogoScale, .ScaleY = LogoScale}
        End Set
    End Property
    Private _Show As Boolean = False
    Public Property Show As Boolean
        Get
            Return _Show
        End Get
        Set(value As Boolean)
            If _Show = value Then Return
            _Show = value
            AnimationService.CancelAnimationByName("MyExtraButton Scale " & Uuid)
'            RunInUi(Sub()
'                        If value Then
'                            '有了
'                            Visibility = Visibility.Visible
'                            AniStart({
'                                AaScaleTransform(Me, 0.3 - CType(RenderTransform, ScaleTransform).ScaleX, 500, 60, New AniEaseOutFluent(AniEasePower.Weak)),
'                                AaScaleTransform(Me, 0.7, 500, 60, New AniEaseOutBack(AniEasePower.Weak)),
'                                AaHeight(Me, 50 - Height, 200,, New AniEaseOutFluent(AniEasePower.Weak))
'                            }, "MyExtraButton MainScale " & Uuid)
'                        Else
'                            '没了
'                            AniStart({
'                                AaScaleTransform(Me, -CType(RenderTransform, ScaleTransform).ScaleX, 100,, New AniEaseInFluent(AniEasePower.Weak)),
'                                AaHeight(Me, -Height, 400, 100, New AniEaseOutFluent()),
'                                AaCode(Sub() Visibility = Visibility.Collapsed,, True)
'                            }, "MyExtraButton MainScale " & Uuid)
'                        End If
'                        IsHitTestVisible = value '防止缩放动画中依然可以点进去
'                    End Sub)
            AnimationService.UIAccessProvider.Invoke(Sub()
                If value Then
                    '有了
                    Visibility = Visibility.Visible
                    Height = 50
                    
                    Dim aniScale = New NScaleTransformFromToAnimation
                    aniScale.Name = "MyExtraButton MainScale " & Uuid
                    aniScale.To = New NScaleTransform(1, 1, 0.5, 0.5)
                    aniScale.Easing = New CompositeEasing((QuadEaseOut.Shared, TimeSpan.FromMilliseconds(500), 0.3),
                                                           (New BackEaseWithPowerOut(EasePower.Weak),TimeSpan.FromMilliseconds(500), 0.7))
                    aniScale.Duration = TimeSpan.FromMilliseconds(500)
                    aniScale.Delay = TimeSpan.FromMilliseconds(60)
                    
                    aniScale.RunFireAndForget(New WpfAnimatable(Me, RenderTransformProperty))
                Else
                    Dim animation = New SequentialAnimationGroup
                    animation.Name = "MyExtraButton MainScale " & Uuid
                    
                    Dim aniScale = New NScaleTransformFromToAnimation
                    aniScale.To = New NScaleTransform(0, 0, 0.5, 0.5)
                    aniScale.Easing = QuadEaseIn.Shared
                    aniScale.Duration = TimeSpan.FromMilliseconds(100)
                    aniScale.SetValue(AnimationExtensions.TargetProperty, Me)
                    aniScale.SetValue(AnimationExtensions.TargetPropertyProperty, RenderTransformProperty)
                    animation.Children.Add(aniScale)
                    
                    Dim aniCode = New ActionAnimation(Sub()
                        Height = 0
                        Visibility = Visibility.Collapsed
                        End Sub)
                    animation.Children.Add(aniCode)
                    
                    animation.RunFireAndForget(EmptyAnimatable.Instance)
                End If
                IsHitTestVisible = value '防止缩放动画中依然可以点进去
                End Sub)
        End Set
    End Property
    Public Delegate Function ShowCheckDelegate() As Boolean
    Public ShowCheck As ShowCheckDelegate = Nothing
    Public Sub ShowRefresh()
        If ShowCheck IsNot Nothing Then Show = ShowCheck()
    End Sub

    '触发点击事件
    Private Sub Button_LeftMouseUp(sender As Object, e As MouseButtonEventArgs) Handles PanClick.MouseLeftButtonUp
        If IsLeftMouseHeld Then
            Log("[Control] 按下附加按钮" & If(ToolTip = "", "", "：" & ToolTip.ToString))
            RaiseEvent Click(sender, e)
            e.Handled = True
            Button_LeftMouseUp()
        End If
    End Sub
    Private Sub Button_RightMouseUp(sender As Object, e As MouseButtonEventArgs) Handles PanClick.MouseRightButtonUp
        If IsRightMouseHeld Then
            Log("[Control] 右键按下附加按钮" & If(ToolTip = "", "", "：" & ToolTip.ToString))
            RaiseEvent RightClick(sender, e)
            e.Handled = True
            Button_RightMouseUp()
        End If
    End Sub
    Private _CanRightClick As Boolean = False
    Public Property CanRightClick As Boolean
        Get
            Return _CanRightClick
        End Get
        Set(value As Boolean)
            _CanRightClick = value
        End Set
    End Property

    '鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    Private IsLeftMouseHeld As Boolean = False
    Private IsRightMouseHeld As Boolean = False
    Private Sub Button_LeftMouseDown(sender As Object, e As MouseButtonEventArgs) Handles PanClick.MouseLeftButtonDown
        If Not IsLeftMouseHeld AndAlso Not IsRightMouseHeld Then
'            AniStart({
'                AaScaleTransform(PanScale, 0.85 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 800,, New AniEaseOutFluent(AniEasePower.Strong)),
'                AaScaleTransform(PanScale, -0.05, 60,, New AniEaseOutFluent)
'            }, "MyExtraButton Scale " & Uuid)
            Dim aniScale = New NScaleTransformFromToAnimation
            aniScale.Name = "MyExtraButton Scale " & Uuid
            aniScale.To = New NScaleTransform(0.8, 0.8, 0.5, 0.5)
            aniScale.Easing = QuinticEaseOut.Shared
            aniScale.Duration = TimeSpan.FromMilliseconds(800)
            aniScale.RunFireAndForget(New WpfAnimatable(Me, RenderTransformProperty))
        End If
        IsLeftMouseHeld = True
        Focus()
    End Sub
    Private Sub Button_RightMouseDown(sender As Object, e As MouseButtonEventArgs) Handles PanClick.MouseRightButtonDown
        If Not CanRightClick Then Return
        If Not IsLeftMouseHeld AndAlso Not IsRightMouseHeld Then
'            AniStart({
'                AaScaleTransform(PanScale, 0.85 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 800,, New AniEaseOutFluent(AniEasePower.Strong)),
'                AaScaleTransform(PanScale, -0.05, 60,, New AniEaseOutFluent)
'            }, "MyExtraButton Scale " & Uuid)
            Dim aniScale = New NScaleTransformFromToAnimation
            aniScale.Name = "MyExtraButton Scale " & Uuid
            aniScale.To = New NScaleTransform(0.8, 0.8, 0.5, 0.5)
            aniScale.Easing = QuinticEaseOut.Shared
            aniScale.Duration = TimeSpan.FromMilliseconds(800)
            aniScale.RunFireAndForget(New WpfAnimatable(Me, RenderTransformProperty))
        End If
        IsRightMouseHeld = True
        Focus()
    End Sub
    Private Sub Button_LeftMouseUp() Handles PanClick.MouseLeftButtonUp
        If Not IsRightMouseHeld Then
'            AniStart({
'                AaScaleTransform(PanScale, 1 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 300,, New AniEaseOutBack)
'            }, "MyExtraButton Scale " & Uuid)
            Dim aniScale = New NScaleTransformFromToAnimation
            aniScale.Name = "MyExtraButton Scale " & Uuid
            aniScale.To = New NScaleTransform(1, 1, 0.5, 0.5)
            aniScale.Easing = New BackEaseWithPowerOut(EasePower.Middle)
            aniScale.Duration = TimeSpan.FromMilliseconds(300)
            aniScale.RunFireAndForget(New WpfAnimatable(Me, RenderTransformProperty))
        End If
        IsLeftMouseHeld = False
        RefreshColor() '直接刷新颜色以判断是否已触发 MouseLeave
    End Sub
    Private Sub Button_RightMouseUp() Handles PanClick.MouseRightButtonUp
        If Not CanRightClick Then Return
        If Not IsLeftMouseHeld Then
'            AniStart({
'                AaScaleTransform(PanScale, 1 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 300,, New AniEaseOutBack)
'            }, "MyExtraButton Scale " & Uuid)
            Dim aniScale = New NScaleTransformFromToAnimation
            aniScale.Name = "MyExtraButton Scale " & Uuid
            aniScale.To = New NScaleTransform(1, 1, 0.5, 0.5)
            aniScale.Easing = New BackEaseWithPowerOut(EasePower.Middle)
            aniScale.Duration = TimeSpan.FromMilliseconds(300)
            aniScale.RunFireAndForget(New WpfAnimatable(Me, RenderTransformProperty))
        End If
        IsRightMouseHeld = False
        RefreshColor() '直接刷新颜色以判断是否已触发 MouseLeave
    End Sub
    Private Sub Button_MouseLeave() Handles PanClick.MouseLeave
        IsLeftMouseHeld = False
        IsRightMouseHeld = False
'        AniStart({
'            AaScaleTransform(PanScale, 1 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 500,, New AniEaseOutFluent)
'        }, "MyExtraButton Scale " & Uuid)
        Dim aniScale = New NScaleTransformFromToAnimation
        aniScale.Name = "MyExtraButton Scale " & Uuid
        aniScale.To = New NScaleTransform(1, 1, 0.5, 0.5)
        aniScale.Easing = CubicEaseOut.Shared
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
                aniColor.Name = "MyExtraButton Color " & Uuid
                If Not IsEnabled Then
                    '禁用
'                    AniStart(AaColor(PanColor, BackgroundProperty, "ColorBrushGray4", AnimationColorIn), "MyExtraButton Color " & Uuid)
                    aniColor.To = New NColor("ColorBrushGray4")
                    aniColor.Duration = TimeSpan.FromMilliseconds(AnimationColorIn)
                    aniColor.RunFireAndForget(New WpfAnimatable(PanColor, BackgroundProperty))
                ElseIf IsMouseOver Then
                    '指向
'                    AniStart(AaColor(PanColor, BackgroundProperty, "ColorBrush4", AnimationColorIn), "MyExtraButton Color " & Uuid)
                    aniColor.To = New NColor("ColorBrush4")
                    aniColor.Duration = TimeSpan.FromMilliseconds(AnimationColorIn)
                    aniColor.RunFireAndForget(New WpfAnimatable(PanColor, BackgroundProperty))
                Else
                    '普通
'                    AniStart(AaColor(PanColor, BackgroundProperty, "ColorBrush3", AnimationColorOut), "MyExtraButton Color " & Uuid)
                    aniColor.To = New NColor("ColorBrush3")
                    aniColor.Duration = TimeSpan.FromMilliseconds(AnimationColorOut)
                    aniColor.RunFireAndForget(New WpfAnimatable(PanColor, BackgroundProperty))
                End If

            Else

'                AniStop("MyExtraButton Color " & Uuid)
                AnimationService.CancelAnimationByName("MyExtraButton Color " & Uuid)
                If Not IsEnabled Then
                    PanColor.SetResourceReference(BackgroundProperty, "ColorBrushGray4")
                ElseIf IsMouseOver Then
                    PanColor.SetResourceReference(BackgroundProperty, "ColorBrush4")
                Else
                    PanColor.SetResourceReference(BackgroundProperty, "ColorBrush3")
                End If

            End If
        Catch ex As Exception
            Log(ex, "刷新图标按钮颜色出错")
        End Try
    End Sub

    ''' <summary>
    ''' 发出一圈波浪效果提示。
    ''' </summary>
    Public Sub Ribble()
'        RunInUi(Sub()
'                    Dim Shape As New Border With {.CornerRadius = New CornerRadius(1000), .BorderThickness = New Thickness(0.001), .Opacity = 0.5, .RenderTransformOrigin = New Point(0.5, 0.5), .RenderTransform = New ScaleTransform()}
'                    Shape.SetResourceReference(Border.BackgroundProperty, "ColorBrush5")
'                    PanScale.Children.Insert(0, Shape)
'                    AniStart({
'                       AaScaleTransform(Shape, 13, 1000, Ease:=New AniEaseInoutFluent(AniEasePower.Strong, 0.3)),
'                       AaOpacity(Shape, -Shape.Opacity, 1000),
'                       AaCode(Sub() PanScale.Children.Remove(Shape), After:=True)
'                    }, "ExtraButton Ribble " & GetUuid())
'                End Sub)
        AnimationService.UIAccessProvider.Invoke(Sub()
            Dim shape As New Border With {.CornerRadius = New CornerRadius(1000), .BorderThickness = New Thickness(0.001), .Opacity = 0.5, .RenderTransformOrigin = New Point(0.5, 0.5), .RenderTransform = New ScaleTransform()}
            shape.SetResourceReference(Border.BackgroundProperty, "ColorBrush5")
            PanScale.Children.Insert(0, shape)
            
            Dim aniGroup1 = New SequentialAnimationGroup
            aniGroup1.Name = "ExtraButton Ribble " & GetUuid()
            
            Dim aniGroup2 = New ParallelAnimationGroup
            
            Dim aniScale = New NScaleTransformFromToAnimation
            aniScale.To = New NScaleTransform(13, 13, 0.5, 0.5)
            aniScale.Duration = TimeSpan.FromMilliseconds(1000)
            aniScale.Easing = New CombinedEasing(QuarticEaseIn.Shared, QuarticEaseOut.Shared, 0.3)
            aniScale.SetValue(AnimationExtensions.TargetProperty, shape)
            aniScale.SetValue(AnimationExtensions.TargetPropertyProperty, RenderTransformProperty)
            aniGroup2.Children.Add(aniScale)
            
            Dim aniOpacity = New DoubleFromToAnimation
            aniOpacity.To = 0
            aniOpacity.Duration = TimeSpan.FromMilliseconds(1000)
            aniOpacity.SetValue(AnimationExtensions.TargetProperty, shape)
            aniOpacity.SetValue(AnimationExtensions.TargetPropertyProperty, OpacityProperty)
            aniGroup2.Children.Add(aniOpacity)
            
            aniGroup1.Children.Add(aniGroup2)
            
            Dim aniCode = New ActionAnimation(Sub()PanScale.Children.Remove(shape))
            aniGroup1.Children.Add(aniCode)
            
            aniGroup1.RunFireAndForget(EmptyAnimatable.Instance)
            End Sub)
    End Sub

End Class

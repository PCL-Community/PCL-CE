Imports PCL.Core.UI
Imports PCL.Core.UI.Animation
Imports PCL.Core.UI.Animation.Animatable
Imports PCL.Core.UI.Animation.Core

Public Class MyComboBoxItem
    Inherits ComboBoxItem

    '基础

    Public Uuid As Integer = GetUuid()
    Public Sub New()
        Style = FindResource("MyComboBoxItem")
    End Sub

    '指向动画

    Private Const AnimationTimeIn As Integer = 100
    Private Const AnimationTimeOut As Integer = 300
    Private BackColorName As String, FontOpacity As Double

    Private Sub RefreshColor() Handles Me.Unselected, Me.MouseMove, Me.MouseLeave, Me.Selected, Me.IsEnabledChanged
        '判断当前颜色
        Dim NewBackColorName As String, NewFontOpacity As Double
        Dim Time As Integer
        If IsSelected Then
            NewBackColorName = "ColorBrush6"
            NewFontOpacity = 1
            Time = AnimationTimeIn
        ElseIf IsMouseOver Then
            NewBackColorName = "ColorBrush8"
            NewFontOpacity = 1
            Time = AnimationTimeIn
        ElseIf IsEnabled Then
            NewBackColorName = "ColorBrushTransparent"
            NewFontOpacity = 1
            Time = AnimationTimeOut
        Else
            NewBackColorName = "ColorBrushTransparent"
            NewFontOpacity = 0.4
            Time = AnimationTimeOut
        End If
        If BackColorName = NewBackColorName AndAlso FontOpacity = NewFontOpacity Then Return
        BackColorName = NewBackColorName
        FontOpacity = NewFontOpacity
        '触发颜色动画
        If IsLoaded AndAlso AniControlEnabled = 0 Then '防止默认属性变更触发动画
            '有动画
'            AniStart({
'                AaColor(Me, BackgroundProperty, BackColorName, Time),
'                AaOpacity(Me, FontOpacity - Opacity, Time)
'            }, "ComboBoxItem Color " & Uuid)
            Dim animation = New ParallelAnimationGroup
            animation.Name = "ComboBoxItem Color " & Uuid
            
            Dim aniColor = New NColorFromToAnimation
            aniColor.To = New NColor(BackColorName)
            aniColor.Duration = TimeSpan.FromMilliseconds(Time)
            aniColor.SetValue(AnimationExtensions.TargetProperty, Me)
            aniColor.SetValue(AnimationExtensions.TargetPropertyProperty, BackgroundProperty)
            animation.Children.Add(aniColor)
            
            Dim aniOpacity = New DoubleFromToAnimation
            aniOpacity.To = FontOpacity
            aniOpacity.Duration = TimeSpan.FromMilliseconds(Time)
            aniOpacity.SetValue(AnimationExtensions.TargetProperty, Me)
            aniOpacity.SetValue(AnimationExtensions.TargetPropertyProperty, OpacityProperty)
            animation.Children.Add(aniOpacity)
            
            animation.RunFireAndForget(EmptyAnimatable.Instance)
        Else
            '无动画
'            AniStop("ComboBoxItem Color " & Uuid)
            AnimationService.CancelAnimationByName("ComboBoxItem Color " & Uuid)
            SetResourceReference(BackgroundProperty, BackColorName)
            Opacity = FontOpacity
        End If
    End Sub

    Public Overrides Function Tostring() As String
        Return Content.ToString
    End Function
    Public Shared Widening Operator CType(Value As MyComboBoxItem) As String
        Return Value.Content.ToString
    End Operator

    Private Sub MyComboBoxItem_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonUp
        Log("[Control] 选择下拉列表项：" & Tostring())
    End Sub

End Class

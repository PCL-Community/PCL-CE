Imports System.Runtime.CompilerServices
Imports Microsoft.Xaml.Behaviors

Module LazyLoader
    <Extension()>
    Public Sub EnableLazyLoad(element As FrameworkElement, action As Action)
        Dim behavior As New LazyLoadBehavior()
        behavior.Action = action
        Interaction.GetBehaviors(element).Add(behavior)
    End Sub
End Module

Public Class LazyLoadBehavior
    Inherits Behavior(Of FrameworkElement)

    Public Shared ReadOnly ActionProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(Action), GetType(Action), GetType(LazyLoadBehavior), New PropertyMetadata(Nothing))

    Public Property Action As Action
        Get
            Return CType(GetValue(ActionProperty), Action)
        End Get
        Set(value As Action)
            SetValue(ActionProperty, value)
        End Set
    End Property

    Protected Overrides Sub OnAttached()
        MyBase.OnAttached()
        AddHandler AssociatedObject.LayoutUpdated, AddressOf OnLayoutUpdated
    End Sub

    Protected Overrides Sub OnDetaching()
        RemoveHandler AssociatedObject.LayoutUpdated, AddressOf OnLayoutUpdated
        MyBase.OnDetaching()
    End Sub

    Private Sub OnLayoutUpdated(sender As Object, e As EventArgs)
        If AssociatedObject.RenderSize.Width < Double.Epsilon Then Return
        If Not AssociatedObject.IsVisible Then Return

        Dim scrollViewer = FindParentScrollViewer(AssociatedObject)
        If scrollViewer Is Nothing Then Return

        Dim elementBounds = AssociatedObject.TransformToAncestor(scrollViewer).TransformBounds(
            New Rect(New Point(0, 0), AssociatedObject.RenderSize))
        Dim viewport = New Rect(0, 0, scrollViewer.ViewportWidth, scrollViewer.ViewportHeight)

        If viewport.IntersectsWith(elementBounds) Then
            Action?.Invoke()
            ' 仅执行一次
            RemoveHandler AssociatedObject.LayoutUpdated, AddressOf OnLayoutUpdated
        End If
    End Sub

    Private Function FindParentScrollViewer(d As DependencyObject) As ScrollViewer
        While d IsNot Nothing
            If TypeOf d Is ScrollViewer Then Return CType(d, ScrollViewer)
            d = VisualTreeHelper.GetParent(d)
        End While
        Return Nothing
    End Function
End Class
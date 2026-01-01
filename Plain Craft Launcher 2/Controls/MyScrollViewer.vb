Public Class MyScrollViewer
    Inherits ScrollViewer

    Public Property DeltaMult As Double = 1


    Private RealOffset As Double
    Private TooltipHideId As String = $"HideTooltip_{Me.GetHashCode()}"
    Private Sub MyScrollViewer_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs) Handles Me.PreviewMouseWheel
        If e.Delta = 0 OrElse ScrollableHeight <= 0 Then Return

        Dim src = e.Source
        If Content.TemplatedParent Is Nothing Then
            If TypeOf src Is ComboBox Then
                If DirectCast(src, ComboBox).IsDropDownOpen Then Return
            ElseIf TypeOf src Is TextBox Then
                If DirectCast(src, TextBox).AcceptsReturn Then Return
            ElseIf TypeOf src Is ComboBoxItem OrElse TypeOf src Is CheckBox Then
                Return
            End If
        End If

        e.Handled = True
        PerformVerticalOffsetDelta(-e.Delta)

        If Application.ShowingTooltips.Count > 0 Then
            For Each TooltipBorder In Application.ShowingTooltips
                ' 建议：如果动画已经在执行，则不再重复触发
                AniStart(AaOpacity(TooltipBorder, -1, 100), TooltipHideId)
            Next
        End If
    End Sub
    Public Sub PerformVerticalOffsetDelta(Delta As Double)
        AniStart(
            AaDouble(
            Sub(AnimDelta As Double)
                RealOffset = MathClamp(RealOffset + AnimDelta, 0, ExtentHeight - ActualHeight)
                ScrollToVerticalOffset(RealOffset)
            End Sub, Delta * DeltaMult, 300,, New AniEaseOutFluent(6)))
    End Sub
    Private Sub MyScrollViewer_ScrollChanged(sender As Object, e As ScrollChangedEventArgs) Handles Me.ScrollChanged
        RealOffset = VerticalOffset
        If FrmMain IsNot Nothing AndAlso (e.VerticalChange OrElse e.ViewportHeightChange) Then FrmMain.BtnExtraBack.ShowRefresh()
    End Sub
    Private Sub MyScrollViewer_IsVisibleChanged(sender As Object, e As DependencyPropertyChangedEventArgs) Handles Me.IsVisibleChanged
        FrmMain.BtnExtraBack.ShowRefresh()
    End Sub

    Public ScrollBar As MyScrollBar
    Private Sub Load() Handles Me.Loaded
        ScrollBar = GetTemplateChild("PART_VerticalScrollBar")
    End Sub

    Private Sub MyScrollViewer_PreviewGotKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs) Handles Me.PreviewGotKeyboardFocus
        If e.NewFocus IsNot Nothing AndAlso TypeOf e.NewFocus Is MySlider Then e.Handled = True '#3854，阻止获得焦点时自动滚动
    End Sub
End Class

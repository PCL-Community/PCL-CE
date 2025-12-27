Public Module LazyLoadBehavior

    ''' <summary>
    ''' 指定首次进入 ScrollViewer 的可视范围时执行的操作。
    ''' </summary>
    <Runtime.CompilerServices.Extension()>
    Public Sub OnFirstEnterScrollViewerViewport(obj As DependencyObject, value As Action)
        obj.SetValue(IsInViewportProperty, value)
    End Sub

    Private ReadOnly IsInViewportProperty As DependencyProperty =
                         DependencyProperty.RegisterAttached("IsInViewport", GetType(Action), GetType(LazyLoadBehavior),
                                                             New PropertyMetadata(Nothing, AddressOf OnIsInViewportChanged))

    Private Sub OnIsInViewportChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim element = TryCast(d, FrameworkElement)
        If element Is Nothing OrElse e.NewValue Is Nothing Then Return

        Dim handled As Boolean = False
        Dim handler As EventHandler =
                Sub()
                    If handled Then Return
                    '判断元素是否可见
                    If element.RenderSize.Width < Double.Epsilon Then Return
                    If Not element.IsVisible Then Return
                    '判断是否在 ScrollViewer 的可视区域内
                    Dim scroll = FindParentScrollViewer(element)
                    If scroll Is Nothing Then Return
                    If Not New Rect(0, 0, scroll.ViewportWidth, scroll.ViewportHeight).IntersectsWith(
                        element.TransformToAncestor(scroll).TransformBounds(New Rect(New Point(0, 0), element.RenderSize))) Then Return
                    '执行
                    handled = True
                    CType(e.NewValue, Action).Invoke()
                    RemoveHandler element.LayoutUpdated, handler
                End Sub
        AddHandler element.LayoutUpdated, handler
    End Sub

    Private Function FindParentScrollViewer(d As DependencyObject) As ScrollViewer
        While d IsNot Nothing
            If TypeOf d Is ScrollViewer Then Return CType(d, ScrollViewer)
            d = VisualTreeHelper.GetParent(d)
        End While
        Return Nothing
    End Function

End Module
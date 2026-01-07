Public Class MyVirtualizingElement(Of T As FrameworkElement)
    Inherits FrameworkElement

    Private _initializer As Func(Of T)
    Public Sub New(initializer As Func(Of T))
        Me._initializer = initializer
        EnableLazyLoad(AddressOf Init)
    End Sub

    ''' <summary>
    ''' 实例化此控件。
    ''' </summary>
    Public Function Init() As T
        Dim element As T = _initializer()
        If Parent IsNot Nothing Then
            If TypeOf Parent IsNot Panel Then Throw New Exception("MyVirtualizingElement 的父级必须是一个 Panel")
            Dim parentPanel As Panel = Parent
            Dim currentIndex As Integer = parentPanel.Children.IndexOf(Me)
            parentPanel.Children.RemoveAt(currentIndex)
            parentPanel.Children.Insert(currentIndex, element)
        End If
        Return element
    End Function
    Public Shared Widening Operator CType(virtualized As MyVirtualizingElement(Of T)) As T
        Return virtualized.Init()
    End Operator

End Class

'非泛型形式
Public Class MyVirtualizingElement
    Inherits FrameworkElement

    Private _initializer As Func(Of FrameworkElement)
    Public Sub New(initializer As Func(Of FrameworkElement))
        Me._initializer = initializer
        EnableLazyLoad(AddressOf Init)
    End Sub

    ''' <summary>
    ''' 实例化此控件。
    ''' </summary>
    Public Function Init() As FrameworkElement
        Dim element As FrameworkElement = _initializer()
        If Parent IsNot Nothing Then
            If TypeOf Parent IsNot Panel Then Throw New Exception("MyVirtualizingElement 的父级必须是一个 Panel")
            Dim parentPanel As Panel = Parent
            Dim currentIndex As Integer = parentPanel.Children.IndexOf(Me)
            parentPanel.Children.RemoveAt(currentIndex)
            parentPanel.Children.Insert(currentIndex, element)
        End If
        Return element
    End Function

    ''' <summary>
    ''' 获取实例化后的控件。
    ''' 如果该控件没有实例化，则会立即实例化。
    ''' 如果类型错误，则返回原值。
    ''' </summary>
    Public Shared Function TryInit(element As FrameworkElement) As FrameworkElement
        If GetType(MyVirtualizingElement(Of )).IsInstanceOfGenericType(element) Then
            Return CType(element, Object).Init()
        ElseIf TypeOf element Is MyVirtualizingElement Then
            Return CType(element, MyVirtualizingElement).Init()
        Else
            Return element
        End If
    End Function

End Class
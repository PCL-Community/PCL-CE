Public Class AnimatedBackgroundGrid
    Inherits Grid

    Public ReadOnly Uuid As Integer = GetUuid()

    Protected Overridable ReadOnly Property AnimatableElement As FrameworkElement
        Get
            Return Me
        End Get
    End Property

    Protected Overridable Property AnimatableBrush As SolidColorBrush
        Get
            Return Background
        End Get
        Set
            Background = Value
        End Set
    End Property

    Private ReadOnly _animatableBrushProperty As DependencyProperty

    Public Sub New(brushDp As DependencyProperty)
        _animatableBrushProperty = brushDp
    End Sub

    Public Sub New()
        Me.New(BackgroundProperty)
    End Sub

    Private _isAnimating As Boolean = False
    Protected Property IsAnimating
        Get
            return _isAnimating
        End Get
        Private Set
            _isAnimating = Value
        End Set
    End Property

    Private Shared Sub _BackgroundBrushChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim grid = CType(d, AnimatedBackgroundGrid)
        Dim brush = CType(e.NewValue, SolidColorBrush)
        If Not (grid.IsLoaded And grid.IsVisible) Then
            grid.AnimatableBrush = brush
            Return
        End If
        grid.Dispatcher.BeginInvoke(Async Function() As Task
            grid.IsAnimating = True
            AniStart({AaColor(grid.AnimatableElement, grid._animatableBrushProperty, New MyColor(brush) - grid.AnimatableBrush, 300)}, "MyCard Theme " & grid.Uuid)
            Await Task.Delay(300)
            grid.AnimatableBrush = brush
            grid.IsAnimating = False
        End Function)
    End Sub

    Public Shared ReadOnly BackgroundBrushProperty As DependencyProperty = DependencyProperty.Register(
        "BackgroundBrush",
        GetType(SolidColorBrush),
        GetType(AnimatedBackgroundGrid),
        New PropertyMetadata(New SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), AddressOf _BackgroundBrushChanged))

    Public Property BackgroundBrush As SolidColorBrush
        Get
            Return GetValue(BackgroundBrushProperty)
        End Get
        Set
            SetValue(BackgroundBrushProperty, Value)
        End Set
    End Property

    Private Sub Init() Handles Me.Loaded
        AnimatableBrush = BackgroundBrush
    End Sub

End Class

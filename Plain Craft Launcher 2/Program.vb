Imports PCL.Core.LifecycleManagement
Imports PCL.Core.Service

Module Program

    ''' <summary>
    ''' Program startup point
    ''' </summary>
    <STAThread>
    Public Sub Main()
        Console.WriteLine("Welcome to Plain Craft Launcher 2 Community Edition!")
        'Preloading tasks
        ApplicationService.Loading =
            Function()
                Dim app As New Application()
                app.InitializeComponent()
                Return app
            End Function
        MainWindowService.Loading =
            Function()
                Dim form As New FormMain()
                form.InitializeComponent()
                Return form
            End Function
        'Start lifecycle
        Lifecycle.OnInitialize()
    End Sub

End Module

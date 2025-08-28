Imports PCL.Core.App

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
        'From dotnet/wpf #2393: fix tablet devices broken on .NET Core 3.0+
        'ReSharper disable once UnusedVariable
        Dim vbSucks = Tablet.TabletDevices
        'Start lifecycle
        Lifecycle.OnInitialize()
    End Sub

End Module

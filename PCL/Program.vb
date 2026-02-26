Imports PCL.Core.App
Imports PCL.Core.App.Essentials
Imports PCL.Core.App.IoC

Module Program

    ''' <summary>
    ''' Program startup point
    ''' </summary>
    <STAThread>
    Public Sub Main()
#If DEBUG
        If Basics.CommandLineArguments.Contains("--debug") Then
            While Not Debugger.IsAttached
                Thread.Sleep(50)
            End While
        End If
#End If
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
                Return form
            End Function
        'From dotnet/wpf #2393: fix tablet devices broken on .NET Core 3.0+
        'ReSharper disable once UnusedVariable
        Dim vbSucks = Tablet.TabletDevices
        'Start lifecycle
        Lifecycle.OnInitialize()
    End Sub

End Module

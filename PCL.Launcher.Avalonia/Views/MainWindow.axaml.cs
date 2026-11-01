using Avalonia.Controls;
using PCL.Avalonia.Navigation;
using PCL.Avalonia.UI.Effects;
using PCL.Avalonia.Views.Pages;
using PCL.Core.App.IoC;

namespace PCL.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WindowBackdropController.Apply(this);

        if (this.FindControl<ContentControl>("PageHost") is { } pageHost)
            PageRouter.Attach(pageHost);

        PageRouter.Navigate(new PageLoading());
        Lifecycle.When(LifecycleState.Running, () => PageRouter.Navigate(() => new PageHome()));
    }
}
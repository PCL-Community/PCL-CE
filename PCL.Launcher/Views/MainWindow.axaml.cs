using Avalonia.Controls;
using PCL.Core.App.IoC;
using PCL.Navigation;
using PCL.UI.Effects;
using PCL.Views.Pages;

namespace PCL.Views;

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
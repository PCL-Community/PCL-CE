using Avalonia.Controls;
using PCL.Core.App.IoC;
using PCL.Navigation;
using PCL.UI;
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

        if (this.FindControl<Panel>("PanHint") is { } hintLayer)
            HintPresenter.Instance.Attach(this, hintLayer);

        if (this.FindControl<Panel>("PanDialogLayer") is { } dialogLayer)
            DialogPresenter.Instance.Attach(this, dialogLayer);

        PageRouter.Navigate(new PageLoading());
        Lifecycle.When(LifecycleState.Running, () => PageRouter.Navigate(() => new PageHome()));
    }
}
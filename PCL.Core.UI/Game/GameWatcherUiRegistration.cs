using PCL.Core.App.IoC;

namespace PCL.Core.UI.Game;

/// <summary>把游戏监控界面服务接入 PCL.Core 的生命周期。</summary>
public static class GameWatcherUiRegistration
{
    /// <summary>必须在 <see cref="Lifecycle.OnInitialize" /> 之前调用。</summary>
    public static void Register()
    {
        Lifecycle.RegisterServiceTypes(LifecycleState.WindowCreating, typeof(GameWatcherUiService));
    }
}
using PCL.Core.App.IoC;
using PCL.Core.UI.Animation.Core;

namespace PCL.Core.Wpf;

/// <summary>
///     PCL.Core.Wpf 提供的生命周期服务注册入口。
/// </summary>
public static class LifecycleRegistration
{
    /// <summary>
    ///     将 PCL.Core.Wpf 中的服务注册到 PCL.Core 的生命周期中。
    ///     必须在 <see cref="Lifecycle.OnInitialize" /> 之前调用。
    /// </summary>
    public static void Register()
    {
        Lifecycle.RegisterServiceTypes(
            LifecycleState.WindowCreating,
            typeof(AnimationService));
    }
}
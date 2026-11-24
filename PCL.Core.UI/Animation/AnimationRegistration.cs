using PCL.Core.App.IoC;
using PCL.Core.UI.Animation.Core;

namespace PCL.Core.UI.Animation;

/// <summary>
///     PCL.Core.UI 动画引擎的生命周期注册入口。
///     必须在 <see cref="Lifecycle.OnInitialize" /> 之前调用 <see cref="Register" />，
///     否则动画服务需要通过 <see cref="EnsureStarted" /> 惰性启动。
/// </summary>
public static class AnimationRegistration
{
    /// <summary>
    ///     将 <see cref="AnimationService" /> 注册到 PCL.Core 的生命周期中。
    /// </summary>
    public static void Register()
    {
        Lifecycle.RegisterServiceTypes(LifecycleState.WindowCreating, typeof(AnimationService));
    }

    /// <summary>
    ///     惰性启动动画服务（不依赖生命周期注册）。
    /// </summary>
    public static void EnsureStarted()
    {
        AnimationService.EnsureStarted();
    }

    /// <summary>
    ///     停止动画服务并释放资源。
    /// </summary>
    public static void Stop()
    {
        AnimationService.StopService();
    }
}
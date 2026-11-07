using System.Threading.Tasks;

namespace PCL.Core.App.IoC;

/// <summary>
/// 启动器生命周期管理
/// </summary>
[LifecycleService(LifecycleState.BeforeLoading, Priority = int.MaxValue)]
public sealed partial class Lifecycle : ILifecycleService
{
    public string Identifier => "lifecycle";
    public string Name => "生命周期";
    public bool SupportAsync => false;

    private static LifecycleContext? _context;
    private Lifecycle() { _context = GetContext(this); }
    private static LifecycleContext Context => _context ?? SystemContext;

    /// <summary>请求在正常退出流程结束后以指定参数重启当前程序。</summary>
    public static void RequestRestartOnExit(string? arguments = null)
    {
        Context.RequestRestartOnExit(arguments);
    }

    public Task StartAsync() => Task.CompletedTask;

    public Task StopAsync()
    {
        _context = null;
        return Task.CompletedTask;
    }
}

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

    public Task StartAsync() => Task.CompletedTask;

    public Task StopAsync()
    {
        _context = null;
        return Task.CompletedTask;
    }
}

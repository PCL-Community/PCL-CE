using System.Threading.Tasks;

namespace PCL.Core.App;

/// <summary>
/// General service class for constructing <see cref="ILifecycleService"/> in a more convenient way.
/// </summary>
public abstract class GeneralService : ILifecycleService
{
    /// <inheritdoc/>
    public string Identifier { get; }
    
    /// <inheritdoc/>
    public string Name { get; }
    
    /// <inheritdoc/>
    public bool SupportAsync { get; }

    /// <summary>
    /// The context of the service instance,
    /// used for declaration, logging, lifecycle operation, etc.
    /// </summary>
    protected LifecycleContext ServiceContext { get; }
    
    /// <summary>
    /// Initialize a general service instance.
    /// This constructor should only be extended rather than invoked directly.
    /// </summary>
    /// <param name="identifier">see <see cref="Identifier"/></param>
    /// <param name="name">see <see cref="Name"/></param>
    /// <param name="asyncStart">see <see cref="SupportAsync"/></param>
    protected GeneralService(string identifier, string name, bool asyncStart = true)
    {
        Identifier = identifier;
        Name = name;
        SupportAsync = asyncStart;
        ServiceContext = Lifecycle.GetContext(this);
    }

    /// <summary>
    /// Start the service, will be invoked on the specified state
    /// from <see cref="LifecycleServiceAttribute.StartState"/> of <see cref="LifecycleServiceAttribute"/>.
    /// </summary>
    public virtual void Start() { }

    /// <summary>
    /// Stop the service, will be invoked while program exiting,
    /// or if <see cref="Start"/> throws an exception.
    /// </summary>
    public virtual void Stop() { }

    public Task StartAsync()
    {
        Start();
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        Stop();
        return Task.CompletedTask;
    }
}

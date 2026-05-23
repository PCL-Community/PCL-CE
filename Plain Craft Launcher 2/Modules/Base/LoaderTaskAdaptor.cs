using PCL.Core.App.Tasks;

namespace PCL;

public class LoaderTaskAdaptor : ITask, ITaskCancelable, ITaskProgressive
{
    private readonly ModLoader.LoaderBase _loader;

    /// <inheritdoc />
    public string Title => _loader.Name;

    public LoaderTaskAdaptor(ModLoader.LoaderBase loader)
    {
        _loader = loader;

        _loader.OnStateChangedUi += (_, newState, _) => StateChanged?.Invoke(ModLoader.FromLoadState(newState), string.Empty);

        _loader.ProgressChanged += (newVal, _) => ProgressChanged?.Invoke(newVal);
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        await using var reg = cancelToken.Register(() => _loader.Abort());
        _loader.Start();

        while (_loader.State is ModBase.LoadState.Loading or ModBase.LoadState.Waiting)
        {
            await Task.Delay(50, cancelToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Cancel() => _loader.Abort();

    /// <inheritdoc />
    public event TaskStateEvent? StateChanged;

    /// <inheritdoc />
    public event TaskProgressEvent? ProgressChanged;
}
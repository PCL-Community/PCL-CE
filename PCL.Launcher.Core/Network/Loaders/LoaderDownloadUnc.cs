namespace PCL.Network.Loaders;

public class LoaderDownloadUnc : Loading.LoaderBase
{
    private CancellationTokenSource? _cancellationTokenSource;
    public string savePath;
    public string unc;

    public LoaderDownloadUnc(string name, Tuple<string, string> file)
    {
        this.name = name;
        unc = file.Item1;
        savePath = file.Item2;
    }

    public override void Start(object input = null, bool isForceRestart = false)
    {
        if (input is Tuple<string, string> tuple)
        {
            unc = tuple.Item1;
            savePath = tuple.Item2;
        }

        lock (lockState)
        {
            if (State == LoadState.Loading)
                return;
            State = LoadState.Loading;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        LauncherCommon.RunInNewThread(() => Run(_cancellationTokenSource.Token), $"UNC/{Uuid}");
    }

    private void Run(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(
                Path.GetDirectoryName(savePath) ?? throw new IOException("下载路径无效"));
            LauncherCommon.CopyFile(unc, savePath);
            State = LoadState.Finished;
        }
        catch (OperationCanceledException)
        {
            Abort();
        }
        catch (Exception ex)
        {
            Error = ex;
            State = LoadState.Failed;
        }
    }

    public override void Abort()
    {
        if (State >= LoadState.Finished)
            return;
        State = LoadState.Aborted;
        _cancellationTokenSource?.Cancel();
    }
}
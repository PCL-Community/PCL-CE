using System.IO;

namespace PCL.Network.Loaders;

public class LoaderDownloadUnc : Loading.LoaderBase
{
    public string unc;
    public string savePath;
    private CancellationTokenSource? _cancellationTokenSource;

    public LoaderDownloadUnc(string name, Tuple<string, string> file)
    {
        base.name = name;
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
            if (State == Common.LoadState.Loading)
                return;
            State = Common.LoadState.Loading;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        Common.RunInNewThread(() => Run(_cancellationTokenSource.Token), $"UNC/{Uuid}");
    }

    private void Run(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(savePath) ?? throw new IOException("下载路径无效"));
            Common.CopyFile(unc, savePath);
            State = Common.LoadState.Finished;
        }
        catch (OperationCanceledException)
        {
            Abort();
        }
        catch (Exception ex)
        {
            Error = ex;
            State = Common.LoadState.Failed;
        }
    }

    public override void Abort()
    {
        if (State >= Common.LoadState.Finished)
            return;
        State = Common.LoadState.Aborted;
        _cancellationTokenSource?.Cancel();
    }
}

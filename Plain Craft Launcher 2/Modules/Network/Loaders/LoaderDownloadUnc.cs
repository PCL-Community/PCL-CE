using System.IO;
using System.Threading;

namespace PCL.Network.Loaders;

public class LoaderDownloadUnc : ModLoader.LoaderBase
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
            if (State == ModBase.LoadState.Loading)
                return;
            State = ModBase.LoadState.Loading;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        ModBase.RunInNewThread(() => Run(_cancellationTokenSource.Token), $"UNC/{Uuid}");
    }

    private void Run(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(savePath) ?? throw new IOException("下载路径无效"));
            ModBase.CopyFile(unc, savePath);
            State = ModBase.LoadState.Finished;
        }
        catch (OperationCanceledException)
        {
            Abort();
        }
        catch (Exception ex)
        {
            Error = ex;
            State = ModBase.LoadState.Failed;
        }
    }

    public override void Abort()
    {
        if (State >= ModBase.LoadState.Finished)
            return;
        State = ModBase.LoadState.Aborted;
        _cancellationTokenSource?.Cancel();
    }
}

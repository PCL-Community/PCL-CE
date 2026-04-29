using System.IO;
using System.Threading;

namespace PCL.Network.Loaders;

public class LoaderDownloadUnc : ModLoader.LoaderBase
{
    public string Unc;
    public string SavePath;
    private CancellationTokenSource? _cancellationTokenSource;

    public LoaderDownloadUnc(string name, Tuple<string, string> file)
    {
        Name = name;
        Unc = file.Item1;
        SavePath = file.Item2;
    }

    public override void Start(object Input = null, bool IsForceRestart = false)
    {
        if (Input is Tuple<string, string> input)
        {
            Unc = input.Item1;
            SavePath = input.Item2;
        }

        lock (LockState)
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
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath) ?? throw new IOException("下载路径无效"));
            ModBase.CopyFile(Unc, SavePath);
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

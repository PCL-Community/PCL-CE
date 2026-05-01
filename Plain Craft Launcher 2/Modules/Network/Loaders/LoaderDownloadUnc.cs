using PCL.Core.App;
using System.IO;

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
            if (State == Enums.LoadState.Loading)
                return;
            State = Enums.LoadState.Loading;
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
            State = Enums.LoadState.Finished;
        }
        catch (OperationCanceledException)
        {
            Abort();
        }
        catch (Exception ex)
        {
            Error = ex;
            State = Enums.LoadState.Failed;
        }
    }

    public override void Abort()
    {
        if (State >= Enums.LoadState.Finished)
            return;
        State = Enums.LoadState.Aborted;
        _cancellationTokenSource?.Cancel();
    }
}

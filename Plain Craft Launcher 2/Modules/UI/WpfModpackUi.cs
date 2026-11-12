namespace PCL;

public sealed class WpfModpackUi : IModpackUi
{
    public List<Loading.LoaderBase> CreateInstallLoader(object request)
    {
        return DownloadUi.McInstallLoader((McInstallRequest)request);
    }

    public void OnInstallStateChanged(object loader)
    {
        DownloadUi.McInstallState(loader);
    }

    public void AddFolder(string folderPath, string displayName, bool showHint)
    {
        PageSelectLeft.AddFolder(folderPath, displayName, showHint);
    }
}
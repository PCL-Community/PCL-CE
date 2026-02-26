using PCL.Core.Utils;

namespace PCL;

public class UpdatesRandomModel : IUpdateSource // 社区自己的更新系统格式
{
    private readonly int _randIndex;

    private readonly IEnumerable<IUpdateSource> _sources;

    public UpdatesRandomModel(IEnumerable<IUpdateSource> Sources)
    {
        _sources = Sources;
        var rand = new Random(DateTime.Now.Millisecond);
        _randIndex = rand.Next(0, _sources.Count() - 1);
    }

    public string SourceName
    {
        get => _sources.ElementAt(_randIndex).SourceName;
        set => _sources.ElementAt(_randIndex).SourceName = value;
    }

    public bool IsAvailable()
    {
        return _sources.ElementAt(_randIndex).IsAvailable();
    }

    public bool RefreshCache()
    {
        return _sources.ElementAt(_randIndex).RefreshCache();
    }

    public VersionDataModel GetLatestVersion(UpdateChannel channel, UpdateArch arch)
    {
        return _sources.ElementAt(_randIndex).GetLatestVersion(channel, arch);
    }

    public bool IsLatest(UpdateChannel channel, UpdateArch arch, SemVer currentVersion, int currentVersionCode)
    {
        return _sources.ElementAt(_randIndex).IsLatest(channel, arch, currentVersion, currentVersionCode);
    }

    public VersionAnnouncementDataModel GetAnnouncementList()
    {
        return _sources.ElementAt(_randIndex).GetAnnouncementList();
    }

    public List<ModLoader.LoaderBase> GetDownloadLoader(UpdateChannel channel, UpdateArch arch, string output)
    {
        return _sources.ElementAt(_randIndex).GetDownloadLoader(channel, arch, output);
    }
}
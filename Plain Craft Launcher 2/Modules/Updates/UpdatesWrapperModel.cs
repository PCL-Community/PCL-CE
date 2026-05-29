using PCL.Core.Utils;

namespace PCL;

public class UpdatesWrapperModel : IUpdateSource
{
    private readonly IEnumerable<IUpdateSource> _sources;
    private IUpdateSource _announcementSource;
    private IUpdateSource _versionSource;

    public UpdatesWrapperModel(IEnumerable<IUpdateSource> sources)
    {
        _sources = sources;
    }

    public string SourceName
    {
        get => _versionSource?.SourceName ?? "";
        set
        {
            if (_versionSource is null)
                return;
            _versionSource.SourceName = value;
        }
    }

    public bool IsAvailable()
    {
        return _sources.Any(x => x.IsAvailable());
    }

    public bool RefreshCache()
    {
        foreach (var item in _sources)
            try
            {
                item.RefreshCache();
                _versionSource = item;
                break;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"[Update] {item.SourceName} 暂不可用");
            }

        return _versionSource is not null;
    }

    public VersionDataModel GetLatestVersion(UpdateChannel channel, UpdateArch arch)
    {
        foreach (var item in _sources)
            try
            {
                if (_versionSource is not null)
                    try
                    {
                        return _versionSource.GetLatestVersion(channel, arch);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, $"[Update] 缓存的版本源 {_versionSource.SourceName} 不可用");
                    }

                var ret = item.GetLatestVersion(channel, arch);
                _versionSource = item;
                return ret;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"[Update] {item.SourceName} 无法获取最新版本信息");
            }

        ModBase.Log("[Update] 错误！所有的版本源都无法使用！");
        throw new Exception("获取版本信息失败");
    }

    public bool IsLatest(UpdateChannel channel, UpdateArch arch, SemVer currentVersion, int currentVersionCode)
    {
        foreach (var item in _sources)
            try
            {
                if (_versionSource is not null)
                    try
                    {
                        return _versionSource.IsLatest(channel, arch, currentVersion, currentVersionCode);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, $"[Update] 缓存的版本源 {_versionSource.SourceName} 不可用");
                    }

                var ret = item.IsLatest(channel, arch, currentVersion, currentVersionCode);
                _versionSource = item;
                return ret;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"[Update] {item.SourceName} 无法获取最新版本信息");
            }

        ModBase.Log("[Update] 错误！所有的版本源都无法使用！");
        throw new Exception("获取版本信息失败");
    }

    public VersionAnnouncementDataModel GetAnnouncementList()
    {
        foreach (var item in _sources)
            try
            {
                if (_announcementSource is not null)
                    try
                    {
                        return _announcementSource.GetAnnouncementList();
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, $"[Update] 缓存的公告源 {_announcementSource.SourceName} 不可用");
                    }

                var ret = item.GetAnnouncementList();
                _announcementSource = item;
                return ret;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"[Update] {item.SourceName} 无法获取最新公告信息");
            }

        ModBase.Log("[Update] 错误！所有的公告源都无法使用！");
        throw new Exception("获取公告信息失败");
    }

    public List<ModLoader.LoaderBase> GetDownloadLoader(UpdateChannel channel, UpdateArch arch, string output)
    {
        foreach (var item in _sources)
            try
            {
                if (_versionSource is not null)
                    try
                    {
                        return _versionSource.GetDownloadLoader(channel, arch, output);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, $"[Update] 缓存的版本源 {_versionSource.SourceName} 不可用");
                    }

                var ret = item.GetDownloadLoader(channel, arch, output);
                _versionSource = item;
                return ret;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"[Update] {item.SourceName} 无法获取最新版本信息");
            }

        ModBase.Log("[Update] 错误！所有的版本源都无法使用！");
        throw new Exception("获取版本信息失败");
    }

    public async Task<bool> IsLatestAsync(UpdateChannel channel, UpdateArch arch, SemVer currentVersion,
        int currentVersionCode)
    {
        return await Task.Run(() => IsLatest(channel, arch, currentVersion, currentVersionCode));
    }
}
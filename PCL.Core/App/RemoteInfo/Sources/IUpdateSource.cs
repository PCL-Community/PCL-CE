using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PCL.Core.Utils;

namespace PCL.Core.App.RemoteInfo.Sources;

public interface IUpdateSource
{
    /// <summary>
    /// 检查更新
    /// </summary>
    /// <returns>检查更新结果</returns>
    public Task<VersionData> GetLatestVersionAsync();

    /// <summary>
    /// 获取版本公告列表
    /// </summary>
    /// <returns>版本公告列表</returns>
    public Task<AnnouncementsListModel> GetAnnouncementAsync();

    /// <summary>
    /// 下载更新文件
    /// </summary>
    /// <param name="outputPath">输出路径</param>
    public Task<bool> DownloadAsync(string outputPath);
    
    /// <summary>
    /// 更新源名称
    /// </summary>
    public string SourceName { get; }
    
    /// <summary>
    /// 更新源是否可用
    /// </summary>
    public bool IsAvailable { get; }
}

public sealed record VersionData(
    int Code,
    string Name,
    string ChangeLog,
    string Sha256)
{
    public bool IsAvailable => Code > Basics.VersionCode &&
                               SemVer.Parse(Name) > SemVer.Parse(Basics.VersionName);
}

public record AnnouncementsListModel(
    [property: JsonPropertyName("content")] AnnouncementContentModel[] Contents
);

public record AnnouncementContentModel(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("detail")] string Detail,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("btn1")] AnnouncementBtnInfoModel? Btn1,
    [property: JsonPropertyName("btn2")] AnnouncementBtnInfoModel? Btn2
);

public record AnnouncementBtnInfoModel (
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("command_paramter")] string CommandParameter
);
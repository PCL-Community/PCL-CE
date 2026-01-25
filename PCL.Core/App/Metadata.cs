using System.Text.Json.Serialization;
using PCL.Core.Utils.OS;

namespace PCL.Core.App;

/// <summary>
/// 启动器版本信息模型
/// </summary>
/// <param name="BaseVersion">基本版本号, 如 <c>2.14.1</c></param>
/// <param name="Suffix">后缀, 如 <c>beta.1</c></param>
/// <param name="Code">内部版本代号, 如 <c>712</c></param>
/// <param name="UpstreamVersion">上游版本, 如 <c>2.12.0</c></param>
public sealed record LauncherVersionModel(
    [property: JsonPropertyName("base")] string BaseVersion,
    [property: JsonPropertyName("suffix")] string Suffix,
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("upstream")] string UpstreamVersion
) {
    private static readonly (string Name, int Code) _CompilationBranchInfo =
#if DEBUG
        ("Debug", 100);
#elif CI
        ("CI", 50);
#else
        ("Publish", 0);
#endif

    private static readonly string? _SecretCommitInfo = EnvironmentInterop.GetSecret("GITHUB_SHA", false);

    /// <summary>
    /// 基本版本名, 若 <see cref="Suffix"/> 存在实际值会附加后缀, 否则与 <see cref="BaseVersion"/> 相同
    /// </summary>
    public string BaseName { get; } = BaseVersion + (string.IsNullOrWhiteSpace(Suffix) ? "" : "-" + Suffix);
    
    /// <summary>
    /// 发行分支名
    /// </summary>
    public string BranchName { get; } = _CompilationBranchInfo.Name;
    
    /// <summary>
    /// 发行分支代号
    /// </summary>
    public int BranchCode { get; } = _CompilationBranchInfo.Code;
    
    /// <summary>
    /// 标准版本号
    /// </summary>
    public string StandardVersion { get; } = BaseVersion + "." + _CompilationBranchInfo.Code;

    /// <summary>
    /// 代码提交版本 hash, 若不存在 (非 CI 构建) 则为 <c>native</c>
    /// </summary>
    public string Commit { get; } = _SecretCommitInfo ?? "native";

    /// <summary>
    /// 代码提交版本 hash 的摘要 (取前 7 位), 若不存在 (非 CI 构建) 则为 <c>native</c>
    /// </summary>
    public string CommitDigest { get; } = _SecretCommitInfo?[..7] ?? "native";
}

/// <summary>
/// 第三方内容许可信息模型
/// </summary>
/// <param name="Name">内容名称</param>
/// <param name="Information">许可信息</param>
/// <param name="WebsiteUri">网站 URI</param>
/// <param name="LicenseUri">许可证 URI</param>
public sealed record ThirdPartyLicenseModel(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("info")] string Information,
    [property: JsonPropertyName("website")] string? WebsiteUri = null,
    [property: JsonPropertyName("license")] string? LicenseUri = null
);

/// <summary>
/// 启动器元数据模型
/// </summary>
/// <param name="Name">程序名称</param>
/// <param name="Version">版本信息</param>
public sealed record MetadataModel(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] LauncherVersionModel Version,
    [property: JsonPropertyName("licenses")] ThirdPartyLicenseModel[] Licenses
);

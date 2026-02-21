using System.IO;
using System.Net.Http;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL;

public class UpdatesMinioModel : IUpdateSource // 社区自己的更新系统格式
{
    private readonly string _baseUrl;

    private Dictionary<string, string> _remoteCache;

    public UpdatesMinioModel(string BaseUrl, string Name = "Minio")
    {
        _baseUrl = BaseUrl;
        SourceName = Name;
    }

    public string SourceName { get; set; }

    public bool IsAvailable()
    {
        return !string.IsNullOrWhiteSpace(_baseUrl);
    }

    public bool RefreshCache()
    {
        // 先检查缓存
        var remoteCache =
            JToken.Parse(Conversions.ToString(ModNet.NetGetCodeByRequestRetry($"{_baseUrl}apiv2/cache.json")));
        _remoteCache = remoteCache.ToObject<Dictionary<string, string>>();
        return true;
    }

    public VersionDataModel GetLatestVersion(UpdateChannel channel, UpdateArch arch)
    {
        if (_remoteCache is null)
            RefreshCache();
        // 确定版本通道名称
        return GetChannelInfo(channel, arch);
    }

    public bool IsLatest(UpdateChannel channel, UpdateArch arch, SemVer currentVersion, int currentVersionCode)
    {
        if (_remoteCache is null)
            RefreshCache();
        var latestVersion = GetChannelInfo(channel, arch);
        return currentVersion >= SemVer.Parse(latestVersion.VersionName);
    }

    public VersionAnnouncementDataModel GetAnnouncementList()
    {
        if (_remoteCache is null)
            RefreshCache();
        var deJsonData = GetRemoteInfoByName("announcement")?.ToObject<VersionAnnouncementDataModel>();
        if (deJsonData is null)
            throw new NullReferenceException("Can not get remote announcement info!");
        return deJsonData;
    }

    public List<ModLoader.LoaderBase> GetDownloadLoader(UpdateChannel channel, UpdateArch arch, string output)
    {
        if (_remoteCache is null)
            RefreshCache();
        var loaders = new List<ModLoader.LoaderBase>();
        var patchUpdate = true;
        var tempPath = $@"{ModBase.PathTemp}Cache\Update\Download\";
        loaders.Add(new ModLoader.LoaderTask<int, List<ModNet.NetFile>>("获取版本信息", load =>
        {
            var channelName = GetChannelName(channel, arch);
            ;
            if (deJsonData is null)
                throw new Exception("No assets can download!");
            var selfSha256 = ModBase.GetFileSHA256(ModBase.ExePathWithName);
            string remoteUpdSha256 = deJsonData.sha256;
            var patchFileName = $"{selfSha256}_{remoteUpdSha256}.patch";
            if (deJsonData.patches.Contains(patchFileName))
            {
                patchUpdate = true;
                tempPath += patchFileName;
                load.Output = new List<ModNet.NetFile>
                    { new(new[] { $"{_baseUrl}static/patch/{patchFileName}" }, tempPath) };
            }
            else
            {
                patchUpdate = false;

                tempPath += $"{deJsonData.sha256}.bin";
                load.Output = new List<ModNet.NetFile> { new(RandomUtils.Shuffle(deJsonData.downloads), tempPath) };
            }
        }));
        loaders.Add(new ModNet.LoaderDownload("下载文件", new List<ModNet.NetFile>()));
        loaders.Add(new ModLoader.LoaderTask<string, int>("应用文件", () =>
        {
            if (patchUpdate)
            {
                ;
#error Cannot convert LocalDeclarationStatementSyntax - see comment for details
                /* Cannot convert LocalDeclarationStatementSyntax, System.NullReferenceException: Object reference not set to an instance of an object.
                   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ShouldPreferExplicitType(ExpressionSyntax exp, ITypeSymbol expConvertedType, Boolean& isNothingLiteral) in /_/CodeConverter/CSharp/CommonConversions.cs:line 120
                   at ICSharpCode.CodeConverter.CSharp.CommonConversions.SplitVariableDeclarationsAsync(VariableDeclaratorSyntax declarator, HashSet`1 symbolsToSkip, Boolean preferExplicitType) in /_/CodeConverter/CSharp/CommonConversions.cs:line 74
                   at ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.SplitVariableDeclarationsAsync(VariableDeclaratorSyntax v, Boolean preferExplicitType) in /_/CodeConverter/CSharp/MethodBodyExecutableStatementVisitor.cs:line 658
                   at ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node) in /_/CodeConverter/CSharp/MethodBodyExecutableStatementVisitor.cs:line 106
                   at ICSharpCode.CodeConverter.CSharp.PerScopeStateVisitorDecorator.AddLocalVariablesAsync(VisualBasicSyntaxNode node, SyntaxKind exitableType, Boolean isBreakableInCs) in /_/CodeConverter/CSharp/PerScopeStateVisitorDecorator.cs:line 38
                   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisitInnerAsync(SyntaxNode node) in /_/CodeConverter/CSharp/CommentConvertingMethodBodyVisitor.cs:line 24

                Input:
                                                                                       Dim diff As New BsDiff()

                 */
                ;
#error Cannot convert LocalDeclarationStatementSyntax - see comment for details
                /* Cannot convert LocalDeclarationStatementSyntax, System.NullReferenceException: Object reference not set to an instance of an object.
                                       at ICSharpCode.CodeConverter.CSharp.CommonConversions.ShouldPreferExplicitType(ExpressionSyntax exp, ITypeSymbol expConvertedType, Boolean& isNothingLiteral) in /_/CodeConverter/CSharp/CommonConversions.cs:line 120
                                       at ICSharpCode.CodeConverter.CSharp.CommonConversions.SplitVariableDeclarationsAsync(VariableDeclaratorSyntax declarator, HashSet`1 symbolsToSkip, Boolean preferExplicitType) in /_/CodeConverter/CSharp/CommonConversions.cs:line 74
                                       at ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.SplitVariableDeclarationsAsync(VariableDeclaratorSyntax v, Boolean preferExplicitType) in /_/CodeConverter/CSharp/MethodBodyExecutableStatementVisitor.cs:line 658
                                       at ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node) in /_/CodeConverter/CSharp/MethodBodyExecutableStatementVisitor.cs:line 106
                                       at ICSharpCode.CodeConverter.CSharp.PerScopeStateVisitorDecorator.AddLocalVariablesAsync(VisualBasicSyntaxNode node, SyntaxKind exitableType, Boolean isBreakableInCs) in /_/CodeConverter/CSharp/PerScopeStateVisitorDecorator.cs:line 38
                                       at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisitInnerAsync(SyntaxNode node) in /_/CodeConverter/CSharp/CommentConvertingMethodBodyVisitor.cs:line 24

                                    Input:
                                                                                                           Dim newFile = diff.ApplyAsync(Global.PCL.ModBase.ReadFileBytes(Global.PCL.ModBase.ExePathWithName), Global.PCL.ModBase.ReadFileBytes(tempPath)).GetAwaiter().GetResult()

                                     */
                WriteFile(output, newFile);
            }
            else
            {
                ;
#error Cannot convert UsingBlockSyntax - see comment for details
                /* Cannot convert UsingBlockSyntax, System.NullReferenceException: Object reference not set to an instance of an object.
                                       at ICSharpCode.CodeConverter.CSharp.CommonConversions.ShouldPreferExplicitType(ExpressionSyntax exp, ITypeSymbol expConvertedType, Boolean& isNothingLiteral) in /_/CodeConverter/CSharp/CommonConversions.cs:line 120
                                       at ICSharpCode.CodeConverter.CSharp.CommonConversions.SplitVariableDeclarationsAsync(VariableDeclaratorSyntax declarator, HashSet`1 symbolsToSkip, Boolean preferExplicitType) in /_/CodeConverter/CSharp/CommonConversions.cs:line 74
                                       at ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.SplitVariableDeclarationsAsync(VariableDeclaratorSyntax v, Boolean preferExplicitType) in /_/CodeConverter/CSharp/MethodBodyExecutableStatementVisitor.cs:line 658
                                       at ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.VisitUsingBlock(UsingBlockSyntax node) in /_/CodeConverter/CSharp/MethodBodyExecutableStatementVisitor.cs:line 1089
                                       at ICSharpCode.CodeConverter.CSharp.PerScopeStateVisitorDecorator.AddLocalVariablesAsync(VisualBasicSyntaxNode node, SyntaxKind exitableType, Boolean isBreakableInCs) in /_/CodeConverter/CSharp/PerScopeStateVisitorDecorator.cs:line 38
                                       at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisitInnerAsync(SyntaxNode node) in /_/CodeConverter/CSharp/CommentConvertingMethodBodyVisitor.cs:line 24

                                    Input:
                                                                                                           Using fs As New Global.System.IO.FileStream(tempPath, Global.System.IO.FileMode.Open, Global.System.IO.FileAccess.Read, Global.System.IO.FileShare.Read)
                                                                                                               Using zip As New Global.System.IO.Compression.ZipArchive(fs)
                                                                                                                   Dim entry = zip.Entries.Where(Function(x) x.Name.Contains("Plain Craft Launcher Community Edition.exe")).FirstOrDefault()
                                                                                                                   If entry Is Nothing Then entry = zip.Entries.Where(Function(x) x.Name.Contains("Plain Craft Launcher")).FirstOrDefault()
                                                                                                                   If entry Is Nothing Then entry = zip.Entries.Where(Function(x) x.Name.Contains("Launcher")).FirstOrDefault()
                                                                                                                   If entry Is Nothing Then entry = zip.Entries.Where(Function(x) x.Name.Contains(".exe")).FirstOrDefault()
                                                                                                                   If entry Is Nothing Then Throw New Global.System.Exception("找不到更新文件")
                                                                                                                   entry.ExtractToFile(output, True)
                                                                                                               End Using
                                                                                                           End Using

                                     */
            }
        }));
        return loaders;
    }

    private VersionDataModel GetChannelInfo(UpdateChannel channel, UpdateArch arch)
    {
        var channelName = GetChannelName(channel, arch);
        var deJsonData = GetRemoteInfoByName($"updates-{channelName}", "updates/")?.ToObject<MinioUpdateModel>().assets
            .FirstOrDefault();
        if (deJsonData is null)
            throw new NullReferenceException("Can not get remote update info!");
        return new VersionDataModel
        {
            VersionName = deJsonData.version.name,
            VersionCode = deJsonData.version.code,
            SHA256 = deJsonData.sha256,
            Source = SourceName,
            Changelog = deJsonData.changelog
        };
    }

    private JToken GetRemoteInfoByName(string name, string path = "")
    {
        var localInfoFile = Path.Combine(ModBase.PathTemp, "Cache", "Update", $"{name}.json");
        JToken jsonData;
        if (IsCacheValid($"{name}.json", _remoteCache[name]))
        {
            jsonData = JToken.Parse(ModBase.ReadFile(localInfoFile));
        }
        else
        {
            var response = HttpRequestBuilder.Create($"{_baseUrl}apiv2/{path}{name}.json", HttpMethod.Get).SendAsync()
                .GetAwaiter().GetResult();
            jsonData = JToken.Parse(response.AsStringContent());
            WriteFile(localInfoFile, response.AsStringContent());
        }

        return jsonData;
    }

    /// <summary>
    ///     缓存是否有效
    /// </summary>
    /// <param name="path"></param>
    /// <param name="hash"></param>
    /// <returns></returns>
    private bool IsCacheValid(string path, string hash)
    {
        var cacheFile = Path.Combine(ModBase.PathTemp, "Cache", "Update", path);
        var fileInfo = new FileInfo(cacheFile);
        return fileInfo.Exists && (DateTime.Now - fileInfo.LastWriteTime).Hours < 1 &&
               (ModBase.GetFileMD5(cacheFile) ?? "") == (hash ?? "");
    }

    private string GetChannelName(UpdateChannel channel, UpdateArch arch)
    {
        var ChannelName = string.Empty;
        switch (channel)
        {
            case UpdateChannel.stable:
            {
                ChannelName += "sr";
                break;
            }
            case UpdateChannel.beta:
            {
                ChannelName += "fr";
                break;
            }

            default:
            {
                ChannelName += "sr";
                break;
            }
        }

        switch (arch)
        {
            case UpdateArch.x64:
            {
                ChannelName += "x64";
                break;
            }
            case UpdateArch.arm64:
            {
                ChannelName += "arm64";
                break;
            }

            default:
            {
                ChannelName += "x64";
                break;
            }
        }

        return ChannelName;
    }

    private class MinioUpdateModel
    {
        public List<MinioUpdateAsset> assets { get; set; }
    }

    private class MinioUpdateAsset
    {
        public string file_name { get; set; }
        public MinioUpdateAssetVersionInfo version { get; set; }
        public string upd_time { get; set; }
        public List<string> downloads { get; set; }
        public List<string> patches { get; set; }
        public string sha256 { get; set; }
        public string changelog { get; set; }
    }

    private class MinioUpdateAssetVersionInfo
    {
        public string channel { get; set; }
        public string name { get; set; }
        public int code { get; set; }
    }
}
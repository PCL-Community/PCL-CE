using System.Text.Json.Nodes;
using PCL.Network;
using PCL.Platform.Abstractions.Java;

namespace PCL;

internal sealed class LauncherJavaRuntimeMetadataProvider : IJavaRuntimeMetadataProvider
{
    private const string RuntimeIndexOfficial =
        "https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

    private const string RuntimeIndexMirror =
        "https://bmclapi2.bangbang93.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

    public ValueTask<string> GetRuntimeIndexAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ModNet.NetGetCodeByLoader(
            ModDownload.DlVersionListOrder([RuntimeIndexOfficial], [RuntimeIndexMirror]),
            isJson: true));
    }

    public ValueTask<string> GetManifestAsync(string manifestUrl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestUrl);

        var manifest = (JsonObject)Requester.FetchJson(
            ModDownload.DlSourceOrder(
                [manifestUrl],
                [manifestUrl.Replace("piston-meta.mojang.com", "bmclapi2.bangbang93.com")]).First(),
            RequestParam.WithRetry);
        return ValueTask.FromResult(manifest.ToJsonString());
    }
}

namespace PCL.Core.Minecraft.Instance.Service;

// TODO: 交给实例下载来做
/*
public static class InstanceLaunchService {
    /// <summary>
    /// Retrieves download information for the vanilla main JAR file of a specific Minecraft version.
    /// Requires the corresponding dependency instance to exist.
    /// Throws an exception on failure, returns null if no download is needed.
    /// </summary>
    public static async Task<DownloadItem?> CheckClientJarAsync(IMcInstance instance, JsonObject versionJson, bool returnNullOnFileUsable) {
        // Check if JSON is valid
        if (versionJson["downloads"]?["client"]?["url"] is null) {
            throw new Exception($"Base instance {instance.Name} lacks JAR file download information");
        }

        // Check file
        var checkRes = await Files.CheckAsync(
            System.IO.Path.Combine(instance.Path, $"{instance.Name}.jar"),
            minSize: 1024,
            actualSize: versionJson["downloads"]!["client"]!.AsObject().TryGetPropertyValue("size", out var sizeNode) 
                ? Int32.TryParse(sizeNode!.ToString(), out var size) ? size : -1 
                : -1,
            hash: versionJson["downloads"]!["client"]!["sha1"]?.ToString()
            );

        if (returnNullOnFileUsable && checkRes is null) {
            return null; // File passed validation
        }

        // Return download information
        var jarUrl = versionJson["downloads"]!["client"]!["url"]!.ToString();
        return new DownloadItem(DlSourceLauncherOrMetaGet(jarUrl), $"{version.Path}{version.Name}.jar", checker);
    }
}
*/

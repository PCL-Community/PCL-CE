namespace PCL.Core.Minecraft.Launch.Services;

// TODO: 实例下载
/*
public static class FileCompleteService {
    public static void FileComplete(McNoPatchesInstance noPatchesInstance, bool checkAssetHash, AssetsIndexExistsBehaviour assetsIndexBehaviour) {
        if (ShouldIgnoreFileCheck(noPatchesInstance.Path)) {
            LogWrapper.Info("Completion", "已跳过所有 Libraries 检查");
        } else {
            LibrariesComplete(noPatchesInstance);
        }
    }

    private static void LibrariesComplete(McNoPatchesInstance noPatchesInstance) {
        if (noPatchesInstance.IsPatchesFormatJson) {
            LibrariesCompleteWithPatches(noPatchesInstance);
        } else {
            LibrariesCompleteWithoutPatches(noPatchesInstance);
        }
    }
    
    private static void LibrariesCompleteWithoutPatches(McNoPatchesInstance noPatchesInstance) {
        foreach (var lib in noPatchesInstance.Libraries!) {
            var path = lib.
            if (path.Exists) {
                if (ShouldIgnoreFileCheck(path.FullName)) {
                    continue;
                }

                if (lib.Downloads?.Artifact != null) { }
            }
        }
    }
    
    private static void LibrariesCompleteWithPatches(McNoPatchesInstance noPatchesInstance) {
        foreach (var lib in noPatchesInstance.Libraries) {
            var path = lib.GetPath(noPatchesInstance);
            if (path.Exists) {
                if (ShouldIgnoreFileCheck(path.FullName)) {
                    continue;
                }

                if (lib.Downloads?.Artifact != null) { }
            }
        }
    }


    private static bool ShouldIgnoreFileCheck(string path) {
        return Config.Instance.DisableAssetVerifyV2[path];
    }
}

public enum AssetsIndexExistsBehaviour {
    /// <summary>
    /// 如果文件存在，则不进行下载。
    /// </summary>
    DontDownload,
    
    /// <summary>
    /// 如果文件存在，则启动新的下载加载器进行独立的更新。
    /// </summary>
    DownloadInBackground,
    
    /// <summary>
    /// 如果文件存在，也同样进行下载。
    /// </summary>
    AlwaysDownload
}
*/
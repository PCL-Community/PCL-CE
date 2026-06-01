using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Curseforge;

[Serializable]
public record CurseforgeFile(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("gameId")] int GameId,
    [property: JsonPropertyName("modId")] int ModId,
    [property: JsonPropertyName("isAvailable")] bool IsAvailable,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("releaseType")] int ReleaseType,
    [property: JsonPropertyName("fileStatus")] int FileStatus,
    [property: JsonPropertyName("hashes")] List<CurseforgeHashes> Hashes,
    [property: JsonPropertyName("fileDate")] string? FileDate,
    [property: JsonPropertyName("fileLength")] long FileLength,
    [property: JsonPropertyName("downloadCount")] long DownloadCount,
    [property: JsonPropertyName("downloadUrl")] string? DownloadUrl,
    [property: JsonPropertyName("gameVersions")] List<string>? GameVersions,
    [property: JsonPropertyName("dependencies")] List<CurseforgeFileDependency>? Dependencies);

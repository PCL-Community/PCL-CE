using System;
using System.Collections.Generic;
using PCL.Core.Minecraft.ResourceProject.Comp.Models.Enums;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Models;

[Serializable]
public sealed record class CompFile(
    string Id,
    string ProjectId,
    string DisplayName,
    string FileName,
    Uri? DownloadUrl,
    long FileLength,
    string ReleaseType,
    List<string> GameVersions,
    List<ModLoaderType> Loaders,
    Dictionary<HashAlgorithm, string> Hashes,
    List<CompFileDependency> Dependencies,
    string? Changelog,
    DateTime DatePublished,
    int DownloadCount,
    bool IsAvailable);

[Serializable]
public sealed record class CompFileDependency(
    string FileId,
    string ProjectId,
    string RelationType);

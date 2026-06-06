using System;

namespace PCL.Core.IO.Storage.Cache.Model;

public record ComponentCacheRow
{
    public string InstancePath { get; init; } = string.Empty; // PK 1
    public string CompType { get; init; } = string.Empty; // PK 2: mods/rp/shader/saves/datapack
    public string FileName { get; init; } = string.Empty; // PK 3

    public string RelativePath { get; init; } = string.Empty;
    public string? FileHash { get; init; } // SHA256
    public long FileSize { get; init; }
    public DateTime LastModified { get; init; }
    public bool Enabled { get; init; }

    // Mod 特有（JSON 序列化到 ModMetadata 列）
    public string? ModName { get; init; }
    public string? ModVersion { get; init; }
    public string? ModAuthor { get; init; }
    public string? ModDescription { get; init; }
    public string? ModLoader { get; init; } // forge / fabric / quilt / liteloader
    public string? ModDependencies { get; init; } // JSON array

    // Scan metadata
    public int CacheVersion { get; init; } = 7;
    public DateTime ScannedAt { get; init; }
    public string? ScanHash { get; init; } // 目录扫描指纹
}
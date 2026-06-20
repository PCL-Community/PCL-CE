// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;

namespace PCL.Core.Minecraft.Saves;

/// <summary>
/// DataVersion 关键分界线常量。
/// 各值取自对应快照的 <c>Data.DataVersion</c>。
/// </summary>
[SuppressMessage("Naming", "CA1707", Justification = "Public names mirror Minecraft snapshot identifiers.")]
public static class DataVersionBoundaries
{
    /// <summary>15w32a（1.9 快照）引入了 DataVersion 字段</summary>
    public const int _15w32a = 100;

    /// <summary>17w47a（1.13 快照）引入了 DataPacks 字段</summary>
    public const int _17w47a = 1443;

    /// <summary>20w20a（1.16 快照）引入了 WorldGenSettings.seed 替代 RandomSeed</summary>
    public const int _20w20a = 2536;

    /// <summary>26.1-snapshot-6 引入了 difficulty_settings 复合标签、spawn.pos 数组、外部种子文件</summary>
    public const int _261snapshot6 = 4774;
}

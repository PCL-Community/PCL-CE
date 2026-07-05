// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Features.Instances.Views;

internal static partial class InstancePageRegistry
{
    public static partial bool IsDefined(InstancePageSubType page);

    public static partial bool UsesGenericFolderPage(InstancePageSubType page);

    public static partial string GetTitle(InstancePageSubType page);

    public static partial string GetDescription(InstancePageSubType page);

    public static partial string GetFolderRelativePath(InstancePageSubType page);

    public static partial InstanceResourceKind GetResourceKind(InstancePageSubType page);

    [InstancePage(InstancePageSubType.Overall, "概览", "", "", usesGenericFolderPage: false)]
    private static void Overall()
    {
    }

    [InstancePage(InstancePageSubType.Setup, "设置", "", "", usesGenericFolderPage: false)]
    private static void Setup()
    {
    }

    [InstancePage(InstancePageSubType.Export, "导出", "", "", usesGenericFolderPage: false)]
    private static void Export()
    {
    }

    [InstancePage(InstancePageSubType.Saves, "存档", "管理当前 Minecraft 根目录下的游戏存档。", "saves", usesGenericFolderPage: false)]
    private static void Saves()
    {
    }

    [InstancePage(InstancePageSubType.Screenshots, "截图", "查看当前 Minecraft 根目录下的截图文件。", "screenshots", usesGenericFolderPage: false)]
    private static void Screenshots()
    {
    }

    [InstancePage(InstancePageSubType.Mods, "Mod", "管理当前 Minecraft 根目录下的 Mod 文件。", "mods", usesGenericFolderPage: true, resourceKind: InstanceResourceKind.Mod)]
    private static void Mods()
    {
    }

    [InstancePage(InstancePageSubType.ModsDisabled, "Mod", "", "", usesGenericFolderPage: false, resourceKind: InstanceResourceKind.Mod)]
    private static void ModsDisabled()
    {
    }

    [InstancePage(InstancePageSubType.ResourcePacks, "资源包", "管理当前 Minecraft 根目录下的资源包。", "resourcepacks", usesGenericFolderPage: true, resourceKind: InstanceResourceKind.ResourcePack)]
    private static void ResourcePacks()
    {
    }

    [InstancePage(InstancePageSubType.Shaders, "光影", "管理当前 Minecraft 根目录下的光影包。", "shaderpacks", usesGenericFolderPage: true, resourceKind: InstanceResourceKind.ShaderPack)]
    private static void Shaders()
    {
    }

    [InstancePage(InstancePageSubType.Schematics, "投影", "管理当前 Minecraft 根目录下的投影文件。", "schematics", usesGenericFolderPage: true, resourceKind: InstanceResourceKind.Schematic)]
    private static void Schematics()
    {
    }

    [InstancePage(InstancePageSubType.Install, "安装", "", "", usesGenericFolderPage: false)]
    private static void Install()
    {
    }

    [InstancePage(InstancePageSubType.Servers, "服务器", "管理当前 Minecraft 根目录下的服务器列表文件。", "", usesGenericFolderPage: false)]
    private static void Servers()
    {
    }
}

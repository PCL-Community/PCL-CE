namespace PCL.Core.Minecraft.Skin;

/// <summary>
/// 皮肤来源类型。
/// </summary>
public enum SkinType
{
    /// <summary>
    /// 使用启动器内置的默认皮肤。
    /// </summary>
    Default,

    /// <summary>
    /// 使用 Steve 皮肤。
    /// </summary>
    Steve,

    /// <summary>
    /// 使用 Alex 皮肤。
    /// </summary>
    Alex,

    /// <summary>
    /// 使用本地皮肤文件。
    /// </summary>
    LocalFile,

    /// <summary>
    /// 使用 LittleSkin 提供的皮肤。
    /// </summary>
    LittleSkin,

    /// <summary>
    /// 使用 Custom Skin Loader API 提供的皮肤。
    /// </summary>
    CustomSkinLoaderApi,
}

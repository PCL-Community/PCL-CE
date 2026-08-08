namespace PCL.Core.Minecraft.Skin;

/// <summary>
/// 皮肤加载完成后得到的具体数据，对应 HMCL auth/offline 模块 Skin.load 的返回结果。
/// </summary>
/// <param name="Model">纹理模型（宽/细）。</param>
/// <param name="Skin">皮肤贴图；未设置时可为 null。</param>
/// <param name="Cape">披风贴图；未设置时可为 null。</param>
public sealed record LoadedSkin(TextureModel Model, SkinTexture? Skin, SkinTexture? Cape);

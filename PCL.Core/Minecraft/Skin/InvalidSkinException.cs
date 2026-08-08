using System;

namespace PCL.Core.Minecraft.Skin;

/// <summary>
/// 皮肤纹理尺寸或格式非法时抛出的异常。
/// </summary>
public sealed class InvalidSkinException : Exception
{
    public InvalidSkinException(string message) : base(message) { }
}

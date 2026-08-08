using System;
using System.Security.Cryptography;

namespace PCL.Core.Minecraft.Skin;

/// <summary>
///     RSA 签名工具，用于内嵌 Yggdrasil 皮肤服务器对 textures 属性进行签名，
///     对齐 HMCL 的 <c>util/KeyUtils.java</c>。
/// </summary>
public static class RsaKeyUtils
{
    /// <summary>
    ///     创建 RSA 密钥对。
    /// </summary>
    /// <param name="keySize">密钥长度（位），默认 2048 位</param>
    public static RSA CreateKey(int keySize = 2048) => RSA.Create(keySize);

    /// <summary>
    ///     导出公钥的 PEM 字符串（含 <c>-----BEGIN PUBLIC KEY-----</c> 换行）。
    ///     用于 authlib-injector 元数据的 <c>signaturePublickey</c> 字段。
    /// </summary>
    /// <param name="rsa">RSA 密钥对象</param>
    public static string GetPublicKeyPem(RSA rsa)
    {
        ArgumentNullException.ThrowIfNull(rsa);
        var keyInfo = rsa.ExportSubjectPublicKeyInfo();
        return PemEncoding.WriteString("PUBLIC KEY", keyInfo);
    }

    /// <summary>
    ///     对数据计算 SHA1withRSA 签名（PKCS#1 填充），返回 Base64 字符串。
    ///     用于对 textures 属性值（Base64 JSON）进行签名，供客户端校验来源。
    /// </summary>
    /// <param name="rsa">RSA 密钥对象</param>
    /// <param name="data">待签名数据的 UTF-8 字节</param>
    public static string SignData(RSA rsa, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(rsa);
        ArgumentNullException.ThrowIfNull(data);
        var signature = rsa.SignData(data, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }
}

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Core.Logging;
using PCL.Core.Utils.Secret;

namespace PCL.Core.App.Configuration.Storage;

public class EncryptedFileConfigStorage(ConfigStorage source) : ConfigStorage
{
    public ConfigStorage Source { get; } = source;

    private static readonly JsonSerializerOptions _SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        AllowOutOfOrderMetadataProperties = true,
    };

    protected override bool OnAccess<TKey, TValue>(StorageAction action, ref TKey key, [NotNullWhen(true)] ref TValue value, object? argument)
    {
        try
        {
            switch (action)
            {
                case StorageAction.Set:
                {
                    // 序列化
                    var type = typeof(TValue);
                    string strValue;
                    if (type == typeof(string)) strValue = value?.ToString() ?? string.Empty;
                    else strValue = JsonSerializer.Serialize(value, _SerializerOptions);
                    // 加密
                    strValue = EncryptHelper.SecretEncrypt(strValue);
                    return Source.Access(StorageAction.Set, ref key, ref strValue, argument);
                }
                case StorageAction.Get:
                {
                    // 获取加密值
                    string? raw = null;
                    var hasOutput = Source.Access(StorageAction.Get, ref key, ref raw, argument);
                    if (!hasOutput) return false;
                    // 解密
                    var decrypted = EncryptHelper.SecretDecrypt(raw);
                    // 反序列化
                    var type = typeof(TValue);
                    if (type == typeof(bool)) Unsafe.As<TValue, bool>(ref value) = decrypted.ToLowerInvariant() is "true" or "1";
                    else if (type == typeof(string)) Unsafe.As<TValue, string>(ref value) = decrypted;
                    else value = JsonSerializer.Deserialize<TValue>(decrypted, _SerializerOptions) ?? throw new NullReferenceException("Decryption produced a null reference");
                    return hasOutput;
                }
                default: return Source.Access(action, ref key, ref value, argument);
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Config", "无法处理加解密");
            return false;
        }
    }
}

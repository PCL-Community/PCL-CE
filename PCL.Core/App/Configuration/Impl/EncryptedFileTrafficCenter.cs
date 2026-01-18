using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Core.App.Configuration.NTraffic;
using PCL.Core.Utils.Secret;

namespace PCL.Core.App.Configuration.Impl;

public class EncryptedFileTrafficCenter(TrafficCenter source) : SyncTrafficCenter
{
    public TrafficCenter Source { get; } = source;

    private static readonly JsonSerializerOptions _SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        AllowOutOfOrderMetadataProperties = true,
    };

    protected override void OnTrafficSync<TInput, TOutput>(PreviewTrafficEventArgs<TInput, TOutput> e)
    {
        if (FileTrafficCenter.CheckKey(e, out var key)) return;
        if (e is { Access: TrafficAccess.Write, HasOutput: true }) // set
        {
            // 序列化
            var type = typeof(TOutput);
            string output;
            if (type == typeof(string))
                output = e.Output?.ToString() ?? string.Empty;
            else
                output = JsonSerializer.Serialize(e.Output, _SerializerOptions);
            // 加密
            output = EncryptHelper.SecretEncrypt(output);
            // 设置加密值
            var eInner = NewEventArgs();
            eInner.SetOutput(output, true);
            Source.Request(eInner);
        }
        else if (e is { Access: TrafficAccess.Read, HasInitialOutput: false }) // get
        {
            // 获取加密值
            var eInner = NewEventArgs();
            Source.Request(eInner);
            if (!eInner.HasOutput || eInner.Output == null) return;
            // 解密
            var output = EncryptHelper.SecretDecrypt(eInner.Output);
            // 反序列化
            TOutput? result;
            var type = typeof(TOutput);
            if (type == typeof(bool)) result = (TOutput)(object)(output.ToLowerInvariant() is "true" or "1");
            else if (type == typeof(string)) result = (TOutput)(object)output;
            else result = JsonSerializer.Deserialize<TOutput>(output, _SerializerOptions);
            e.SetOutput(result);
        }
        else Source.Request(e); // other
        return;
        PreviewTrafficEventArgs<string, string> NewEventArgs()
            => CreateEventArgs<string, string>(e.Context, e.Access, true, key);
    }
}

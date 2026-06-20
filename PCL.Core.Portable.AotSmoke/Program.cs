// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;
using fNbt;
using PCL.Core.Minecraft.Saves;
using PCL.Core.Platform;
using PCL.Core.Serialization;
using PCL.Core.Utils;
using PCL.Core.Utils.Encryption;
using PCL.Core.Utils.Hash;

var payload = new SmokePayload("PCL N", 10);
await using var json = new MemoryStream();
await AotJson.SerializeAsync(json, payload, SmokeJsonContext.Default.SmokePayload);
json.Position = 0;
var roundTrip = await AotJson.DeserializeAsync(json, SmokeJsonContext.Default.SmokePayload);

var hashValid = VerifyHash(roundTrip?.Name ?? string.Empty);
var platformPolicyValid =
    PlatformFeaturePolicy.IsSystemAccentThemeSupported ==
    (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS());
var varIntValid = VerifyVarInt();
var encryptionValid = VerifyEncryption();
var saveFolder = Path.Combine(Path.GetTempPath(), $"pcl-aot-{Guid.NewGuid():N}");
Directory.CreateDirectory(saveFolder);
var rootTag = new NbtCompound("");
var saveData = new NbtCompound("Data");
saveData.Add(new NbtString("LevelName", "AOT World"));
saveData.Add(new NbtLong("LastPlayed", 0));
saveData.Add(new NbtLong("Time", 0));
saveData.Add(new NbtInt("GameType", 0));
rootTag.Add(saveData);
var saveFile = new NbtFile(rootTag);
await using (var output = File.Create(Path.Combine(saveFolder, "level.dat")))
{
    saveFile.SaveToStream(output, NbtCompression.GZip);
}
var saveInfo = await new SaveManager().LoadSaveAsync(saveFolder);
var saveValid = saveInfo.LevelName == "AOT World";
Directory.Delete(saveFolder, recursive: true);

return hashValid &&
       roundTrip == payload &&
       platformPolicyValid &&
       varIntValid &&
       encryptionValid &&
       saveValid
    ? 0
    : 1;

static bool VerifyHash(string value)
{
    Span<byte> hash = stackalloc byte[32];
    return SHA256Provider.Instance.TryComputeHash(value.AsSpan(), hash, out var written) &&
           written == hash.Length;
}

static bool VerifyVarInt()
{
    Span<byte> varInt = stackalloc byte[10];
    return VarIntHelper.TryEncode(ulong.MaxValue, varInt, out var length) &&
           VarIntHelper.Decode(varInt[..length], out var consumed) == ulong.MaxValue &&
           consumed == length;
}

static bool VerifyEncryption()
{
    Span<byte> key = stackalloc byte[32];
    key.Fill(0x5A);
    ReadOnlySpan<byte> plaintext = "portable-aot"u8;
    var encrypted = ChaCha20SoftwareProvider.Instance.Encrypt(plaintext, key);
    return plaintext.SequenceEqual(ChaCha20SoftwareProvider.Instance.Decrypt(encrypted, key));
}

internal sealed record SmokePayload(string Name, int RuntimeMajor);

[JsonSerializable(typeof(SmokePayload))]
internal sealed partial class SmokeJsonContext : JsonSerializerContext;

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;
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

Span<byte> hash = stackalloc byte[32];
var hashed = SHA256Provider.Instance.TryComputeHash(
    (roundTrip?.Name ?? string.Empty).AsSpan(),
    hash,
    out var written);
var platformPolicyValid =
    PlatformFeaturePolicy.IsSystemAccentThemeSupported ==
    (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS());
Span<byte> varInt = stackalloc byte[10];
var varIntValid =
    VarIntHelper.TryEncode(ulong.MaxValue, varInt, out var varIntLength) &&
    VarIntHelper.Decode(varInt[..varIntLength], out var consumed) == ulong.MaxValue &&
    consumed == varIntLength;
Span<byte> encryptionKey = stackalloc byte[32];
encryptionKey.Fill(0x5A);
var plaintext = "portable-aot"u8;
var encrypted = ChaCha20SoftwareProvider.Instance.Encrypt(plaintext, encryptionKey);
var encryptionValid = plaintext.SequenceEqual(
    ChaCha20SoftwareProvider.Instance.Decrypt(encrypted, encryptionKey));

return hashed &&
       written == hash.Length &&
       roundTrip == payload &&
       platformPolicyValid &&
       varIntValid &&
       encryptionValid
    ? 0
    : 1;

internal sealed record SmokePayload(string Name, int RuntimeMajor);

[JsonSerializable(typeof(SmokePayload))]
internal sealed partial class SmokeJsonContext : JsonSerializerContext;

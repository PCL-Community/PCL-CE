// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.JsonWebToken;

/// <summary>
/// Native AOT compatible representation of a JSON Web Key.
/// </summary>
public sealed record JsonWebKeyData
{
    [JsonPropertyName("kty")]
    public string? KeyType { get; init; }

    [JsonPropertyName("kid")]
    public string? KeyId { get; init; }

    [JsonPropertyName("use")]
    public string? PublicKeyUse { get; init; }

    [JsonPropertyName("alg")]
    public string? Algorithm { get; init; }

    [JsonPropertyName("n")]
    public string? Modulus { get; init; }

    [JsonPropertyName("e")]
    public string? Exponent { get; init; }

    [JsonPropertyName("crv")]
    public string? Curve { get; init; }

    [JsonPropertyName("x")]
    public string? X { get; init; }

    [JsonPropertyName("y")]
    public string? Y { get; init; }

    [JsonPropertyName("x5c")]
    public string[]? X509CertificateChain { get; init; }
}

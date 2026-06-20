// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.JsonWebToken;

/// <summary>
/// Native AOT compatible compact JWT parser and signature verifier.
/// </summary>
public sealed class JsonWebToken
{
    public delegate bool TokenValidateCallback(
        OpenIdMetadata metadata,
        JsonWebToken token,
        JsonWebKeyData? key,
        string? clientId);

    private readonly string _token;
    private readonly OpenIdMetadata _metadata;
    private bool _parsed;
    private bool _verified;
    private JsonElement _header;
    private JsonElement _payload;
    private int _secondSeparator;
    private int _signatureOffset;

    public JsonWebToken(string token, OpenIdMetadata metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentNullException.ThrowIfNull(metadata);
        _token = token;
        _metadata = metadata;
    }

    /// <summary>
    /// Replaces the default signature and claims validator when custom policy is required.
    /// </summary>
    public TokenValidateCallback SecurityTokenValidateCallback { get; set; } =
        static (metadata, token, key, clientId) =>
            token.ValidateCore(metadata, key, clientId);

    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(60);

    public bool IsVerified => _verified;

    public string? Algorithm => GetHeaderString("alg");

    public string? KeyId => GetHeaderString("kid");

    public bool VerifySignature(JsonWebKeyData key, string? clientId = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        try
        {
            if (!SecurityTokenValidateCallback(_metadata, this, key, clientId))
                throw new SecurityException("令牌验证器拒绝了该令牌");
            _verified = true;
            return true;
        }
        catch (SecurityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SecurityException($"令牌签名验证失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates issuer, audience, not-before, and expiration claims without
    /// treating the token as cryptographically verified.
    /// </summary>
    public bool ValidateClaims(string? clientId = null)
    {
        try
        {
            ValidateClaimsCore(_metadata, clientId);
            return true;
        }
        catch (SecurityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SecurityException($"令牌声明验证失败：{ex.Message}", ex);
        }
    }

    public JsonElement ReadTokenPayload(bool allowUnverifiedToken = false)
    {
        EnsurePayloadAccess(allowUnverifiedToken);
        EnsureParsed();
        return _payload;
    }

    public T? ReadTokenPayload<T>(
        JsonTypeInfo<T> typeInfo,
        bool allowUnverifiedToken = false)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        var payload = ReadTokenPayload(allowUnverifiedToken);
        return JsonSerializer.Deserialize(payload, typeInfo);
    }

    public JsonElement ReadTokenHeader()
    {
        EnsureParsed();
        return _header;
    }

    public T? ReadTokenHeader<T>(JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return JsonSerializer.Deserialize(ReadTokenHeader(), typeInfo);
    }

    public DateTime? GetExpirationTime() =>
        ReadNumericDate("exp") is { } seconds
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : null;

    public DateTime? GetIssuedAtTime() =>
        ReadNumericDate("iat") is { } seconds
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : null;

    public DateTime? GetNotBeforeTime() =>
        ReadNumericDate("nbf") is { } seconds
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : null;

    public bool IsExpired()
    {
        try
        {
            return ReadNumericDate("exp") is not { } expiration ||
                   TimeProvider.GetUtcNow().ToUnixTimeSeconds() > expiration;
        }
        catch
        {
            return true;
        }
    }

    public string? GetClaimValue(
        string claimType,
        bool allowUnverifiedToken = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);
        var payload = ReadTokenPayload(allowUnverifiedToken);
        if (!payload.TryGetProperty(claimType, out var claim))
            return null;
        return claim.ValueKind switch
        {
            JsonValueKind.String => claim.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => claim.ToString()
        };
    }

    public string GetTokenString() => _token;

    private bool ValidateCore(
        OpenIdMetadata metadata,
        JsonWebKeyData? key,
        string? clientId)
    {
        ValidateClaimsCore(metadata, clientId);
        return key is null || VerifyCryptographicSignature(metadata, key);
    }

    private void ValidateClaimsCore(
        OpenIdMetadata metadata,
        string? clientId)
    {
        EnsureParsed();
        if (!_payload.TryGetProperty("iss", out var issuerElement) ||
            issuerElement.ValueKind != JsonValueKind.String ||
            !string.Equals(
                issuerElement.GetString(),
                metadata.Issuer,
                StringComparison.Ordinal))
            throw new SecurityException("令牌发行者不匹配");

        if (!string.IsNullOrWhiteSpace(clientId) &&
            !HasAudience(_payload, clientId))
            throw new SecurityException("令牌受众不匹配");

        if (ClockSkew < TimeSpan.Zero)
            throw new SecurityException("JWT 时钟偏差不能为负数");
        var now = TimeProvider.GetUtcNow().ToUnixTimeSeconds();
        var skew = checked((long)Math.Ceiling(ClockSkew.TotalSeconds));
        var expiration = ReadNumericDate("exp")
                         ?? throw new SecurityException("令牌缺少 exp 声明");
        if ((Int128)now > (Int128)expiration + skew)
            throw new SecurityException("令牌已过期");

        if (ReadNumericDate("nbf") is { } notBefore &&
            (Int128)now + skew < notBefore)
            throw new SecurityException("令牌尚未生效");
    }

    private bool VerifyCryptographicSignature(
        OpenIdMetadata metadata,
        JsonWebKeyData key)
    {
        EnsureParsed();
        var algorithm = GetRequiredHeaderString("alg");
        if (string.Equals(algorithm, "none", StringComparison.OrdinalIgnoreCase))
            throw new SecurityException("不允许使用无签名 JWT");
        if (metadata.IdTokenSigningAlgValuesSupported.Count > 0 &&
            !metadata.IdTokenSigningAlgValuesSupported.Contains(
                algorithm,
                StringComparer.Ordinal))
            throw new SecurityException("OpenID 元数据不允许该 JWT 签名算法");
        if (!string.IsNullOrWhiteSpace(key.Algorithm) &&
            !string.Equals(key.Algorithm, algorithm, StringComparison.Ordinal))
            throw new SecurityException("JWT 算法与 JWK 算法不匹配");
        var keyId = GetHeaderString("kid");
        if (!string.IsNullOrWhiteSpace(keyId) &&
            !string.IsNullOrWhiteSpace(key.KeyId) &&
            !string.Equals(keyId, key.KeyId, StringComparison.Ordinal))
            throw new SecurityException("JWT kid 与 JWK kid 不匹配");
        if (!string.IsNullOrWhiteSpace(key.PublicKeyUse) &&
            !string.Equals(key.PublicKeyUse, "sig", StringComparison.Ordinal))
            throw new SecurityException("JWK 不允许用于签名验证");

        return algorithm switch
        {
            "RS256" => VerifyRsa(key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            "RS384" => VerifyRsa(key, HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1),
            "RS512" => VerifyRsa(key, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1),
            "PS256" => VerifyRsa(key, HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
            "PS384" => VerifyRsa(key, HashAlgorithmName.SHA384, RSASignaturePadding.Pss),
            "PS512" => VerifyRsa(key, HashAlgorithmName.SHA512, RSASignaturePadding.Pss),
            "ES256" => VerifyEcdsa(key, HashAlgorithmName.SHA256, "P-256"),
            "ES384" => VerifyEcdsa(key, HashAlgorithmName.SHA384, "P-384"),
            "ES512" => VerifyEcdsa(key, HashAlgorithmName.SHA512, "P-521"),
            _ => throw new SecurityException($"不支持的 JWT 签名算法：{algorithm}")
        };
    }

    private bool VerifyRsa(
        JsonWebKeyData key,
        HashAlgorithmName hashAlgorithm,
        RSASignaturePadding padding)
    {
        if (!string.Equals(key.KeyType, "RSA", StringComparison.Ordinal))
            throw new SecurityException("JWT 需要 RSA JWK");
        if (string.IsNullOrWhiteSpace(key.Modulus) ||
            string.IsNullOrWhiteSpace(key.Exponent))
            throw new SecurityException("RSA JWK 缺少 n 或 e");

        var modulus = DecodeBase64UrlToArray(key.Modulus);
        if (modulus.Length < 256)
            throw new SecurityException("RSA JWK 密钥长度不足 2048 位");

        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = modulus,
            Exponent = DecodeBase64UrlToArray(key.Exponent)
        });

        var dataBuffer = ArrayPool<byte>.Shared.Rent(_secondSeparator);
        var signatureLength = GetDecodedLength(_token.Length - _signatureOffset);
        var signatureBuffer = ArrayPool<byte>.Shared.Rent(signatureLength);
        try
        {
            CopyAscii(_token.AsSpan(0, _secondSeparator), dataBuffer);
            var written = DecodeBase64Url(
                _token.AsSpan(_signatureOffset),
                signatureBuffer);
            return rsa.VerifyData(
                dataBuffer.AsSpan(0, _secondSeparator),
                signatureBuffer.AsSpan(0, written),
                hashAlgorithm,
                padding);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(dataBuffer);
            ArrayPool<byte>.Shared.Return(signatureBuffer);
        }
    }

    private bool VerifyEcdsa(
        JsonWebKeyData key,
        HashAlgorithmName hashAlgorithm,
        string expectedCurve)
    {
        if (!string.Equals(key.KeyType, "EC", StringComparison.Ordinal))
            throw new SecurityException("JWT 需要 EC JWK");
        if (!string.Equals(key.Curve, expectedCurve, StringComparison.Ordinal))
            throw new SecurityException("JWT 算法与 JWK 曲线不匹配");
        if (string.IsNullOrWhiteSpace(key.X) ||
            string.IsNullOrWhiteSpace(key.Y))
            throw new SecurityException("EC JWK 缺少 x 或 y");

        using var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = GetCurve(expectedCurve),
            Q = new ECPoint
            {
                X = DecodeBase64UrlToArray(key.X),
                Y = DecodeBase64UrlToArray(key.Y)
            }
        });

        var dataBuffer = ArrayPool<byte>.Shared.Rent(_secondSeparator);
        var signatureLength = GetDecodedLength(_token.Length - _signatureOffset);
        var signatureBuffer = ArrayPool<byte>.Shared.Rent(signatureLength);
        try
        {
            CopyAscii(_token.AsSpan(0, _secondSeparator), dataBuffer);
            var written = DecodeBase64Url(
                _token.AsSpan(_signatureOffset),
                signatureBuffer);
            return ecdsa.VerifyData(
                dataBuffer.AsSpan(0, _secondSeparator),
                signatureBuffer.AsSpan(0, written),
                hashAlgorithm,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(dataBuffer);
            ArrayPool<byte>.Shared.Return(signatureBuffer);
        }
    }

    private void EnsureParsed()
    {
        if (_parsed)
            return;

        var firstSeparator = _token.IndexOf('.');
        _secondSeparator = firstSeparator < 0
            ? -1
            : _token.IndexOf('.', firstSeparator + 1);
        if (firstSeparator <= 0 ||
            _secondSeparator <= firstSeparator + 1 ||
            _secondSeparator == _token.Length - 1 ||
            _token.IndexOf('.', _secondSeparator + 1) >= 0)
            throw new SecurityException("令牌格式无效");

        _signatureOffset = _secondSeparator + 1;
        _header = DecodeJsonObject(_token.AsSpan(0, firstSeparator), "header");
        _payload = DecodeJsonObject(
            _token.AsSpan(firstSeparator + 1, _secondSeparator - firstSeparator - 1),
            "payload");
        _parsed = true;
    }

    private static JsonElement DecodeJsonObject(
        ReadOnlySpan<char> encoded,
        string section)
    {
        byte[]? buffer = null;
        try
        {
            var length = GetDecodedLength(encoded.Length);
            buffer = ArrayPool<byte>.Shared.Rent(length);
            var written = DecodeBase64Url(encoded, buffer);
            var reader = new Utf8JsonReader(buffer.AsSpan(0, written));
            using var document = JsonDocument.ParseValue(ref reader);
            if (reader.BytesConsumed != written ||
                document.RootElement.ValueKind != JsonValueKind.Object)
                throw new SecurityException($"JWT {section} 不是有效的 JSON 对象");
            return document.RootElement.Clone();
        }
        catch (SecurityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SecurityException($"JWT {section} 解析失败：{ex.Message}", ex);
        }
        finally
        {
            if (buffer is not null)
                ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static int DecodeBase64Url(
        ReadOnlySpan<char> source,
        Span<byte> destination)
    {
        if (source.IsEmpty || source.Length % 4 == 1)
            throw new FormatException("Invalid Base64Url length");

        var accumulator = 0;
        var bitCount = 0;
        var written = 0;
        foreach (var character in source)
        {
            var value = character switch
            {
                >= 'A' and <= 'Z' => character - 'A',
                >= 'a' and <= 'z' => character - 'a' + 26,
                >= '0' and <= '9' => character - '0' + 52,
                '-' => 62,
                '_' => 63,
                _ => throw new FormatException("Invalid Base64Url character")
            };
            accumulator = (accumulator << 6) | value;
            bitCount += 6;
            if (bitCount < 8)
                continue;
            bitCount -= 8;
            destination[written++] = (byte)(accumulator >> bitCount);
            accumulator &= bitCount == 0 ? 0 : (1 << bitCount) - 1;
        }

        if (bitCount > 0 && accumulator != 0)
            throw new FormatException("Invalid Base64Url trailing bits");
        return written;
    }

    private static byte[] DecodeBase64UrlToArray(string value)
    {
        var result = new byte[GetDecodedLength(value.Length)];
        var written = DecodeBase64Url(value, result);
        return written == result.Length ? result : result[..written];
    }

    private static int GetDecodedLength(int encodedLength)
    {
        var remainder = encodedLength % 4;
        if (encodedLength <= 0 || remainder == 1)
            throw new FormatException(
                $"Invalid Base64Url length: {encodedLength} (remainder {remainder})");
        return encodedLength / 4 * 3 + (remainder switch
        {
            0 => 0,
            2 => 1,
            3 => 2,
            _ => throw new FormatException(
                $"Invalid Base64Url length: {encodedLength} (remainder {remainder})")
        });
    }

    private static void CopyAscii(ReadOnlySpan<char> source, Span<byte> destination)
    {
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if (character > 0x7F)
                throw new SecurityException("JWT 包含非 ASCII 字符");
            destination[index] = (byte)character;
        }
    }

    private static ECCurve GetCurve(string curve) =>
        curve switch
        {
            "P-256" => ECCurve.NamedCurves.nistP256,
            "P-384" => ECCurve.NamedCurves.nistP384,
            "P-521" => ECCurve.NamedCurves.nistP521,
            _ => throw new SecurityException($"不支持的 EC 曲线：{curve}")
        };

    private static bool HasAudience(JsonElement payload, string clientId)
    {
        if (!payload.TryGetProperty("aud", out var audience))
            return false;
        if (audience.ValueKind == JsonValueKind.String)
            return string.Equals(
                audience.GetString(),
                clientId,
                StringComparison.Ordinal);
        if (audience.ValueKind != JsonValueKind.Array)
            return false;
        foreach (var item in audience.EnumerateArray())
            if (item.ValueKind == JsonValueKind.String &&
                string.Equals(item.GetString(), clientId, StringComparison.Ordinal))
                return true;
        return false;
    }

    private long? ReadNumericDate(string claimName)
    {
        EnsureParsed();
        if (!_payload.TryGetProperty(claimName, out var claim))
            return null;
        if (claim.ValueKind == JsonValueKind.Number)
        {
            if (claim.TryGetInt64(out var integer))
                return integer;
            if (claim.TryGetDouble(out var number) &&
                double.IsFinite(number) &&
                number >= long.MinValue &&
                number <= long.MaxValue)
                return checked((long)Math.Floor(number));
        }
        else if (claim.ValueKind == JsonValueKind.String &&
                 long.TryParse(
                     claim.GetString(),
                     NumberStyles.Integer,
                     CultureInfo.InvariantCulture,
                     out var parsed))
        {
            return parsed;
        }

        throw new SecurityException($"JWT {claimName} 声明不是有效的 NumericDate");
    }

    private string? GetHeaderString(string name)
    {
        EnsureParsed();
        return _header.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private string GetRequiredHeaderString(string name) =>
        GetHeaderString(name)
        ?? throw new SecurityException($"JWT header 缺少 {name}");

    private void EnsurePayloadAccess(bool allowUnverifiedToken)
    {
        if (!allowUnverifiedToken && !_verified)
            throw new SecurityException("令牌尚未通过签名验证");
    }
}

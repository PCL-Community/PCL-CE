// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Core.Minecraft.IdentityModel.Extensions.JsonWebToken;
using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;

namespace PCL.Core.Test.Minecraft.IdentityModel;

[TestClass]
public sealed class JsonWebTokenTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);

    [TestMethod]
    public void RsaTokenRequiresVerificationAndReadsTypedPayload()
    {
        using var rsa = RSA.Create(2048);
        var token = CreateRsaToken(rsa, audience: "portable-client");
        var jwt = CreateToken(token);

        Assert.ThrowsExactly<SecurityException>(() => jwt.ReadTokenPayload());
        Assert.IsTrue(jwt.VerifySignature(CreateRsaKey(rsa), "portable-client"));

        var claims = jwt.ReadTokenPayload(JwtTestJsonContext.Default.TestClaims);
        Assert.IsTrue(jwt.IsVerified);
        Assert.AreEqual("Portable User", claims?.Name);
        Assert.AreEqual("RS256", jwt.Algorithm);
        Assert.AreEqual("test-key", jwt.KeyId);
        Assert.AreEqual("Portable User", jwt.GetClaimValue("name"));
        Assert.AreEqual(Now.AddSeconds(-10).UtcDateTime, jwt.GetIssuedAtTime());
    }

    [TestMethod]
    public void ClaimsValidationDoesNotMarkUnsignedDataAsVerified()
    {
        using var rsa = RSA.Create(2048);
        var jwt = CreateToken(CreateRsaToken(rsa, audience: "portable-client"));

        Assert.IsTrue(jwt.ValidateClaims("portable-client"));
        Assert.IsFalse(jwt.IsVerified);
        Assert.ThrowsExactly<SecurityException>(() => jwt.ReadTokenPayload());
        Assert.AreEqual(
            "Portable User",
            jwt.GetClaimValue("name", allowUnverifiedToken: true));
    }

    [TestMethod]
    public void WrongAudienceAndExpiredTokenAreRejected()
    {
        using var rsa = RSA.Create(2048);
        var key = CreateRsaKey(rsa);
        var wrongAudience = CreateToken(
            CreateRsaToken(rsa, audience: "another-client"));
        var expired = CreateToken(
            CreateRsaToken(rsa, audience: "portable-client", expiresInSeconds: -120));

        Assert.ThrowsExactly<SecurityException>(
            () => wrongAudience.VerifySignature(key, "portable-client"));
        Assert.ThrowsExactly<SecurityException>(
            () => expired.VerifySignature(key, "portable-client"));
    }

    [TestMethod]
    public void EcdsaTokenUsesP1363SignatureFormat()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var token = CreateEcdsaToken(ecdsa);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);
        var key = new JsonWebKeyData
        {
            KeyType = "EC",
            KeyId = "ec-key",
            PublicKeyUse = "sig",
            Algorithm = "ES256",
            Curve = "P-256",
            X = Base64Url(parameters.Q.X!),
            Y = Base64Url(parameters.Q.Y!)
        };
        var jwt = CreateToken(token);

        Assert.IsTrue(jwt.VerifySignature(key, "portable-client"));
    }

    [TestMethod]
    public void TamperedSignatureAndMismatchedKeyIdAreRejected()
    {
        using var rsa = RSA.Create(2048);
        var token = CreateRsaToken(rsa, audience: "portable-client");
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');
        var mismatchedKey = CreateRsaKey(rsa) with { KeyId = "another-key" };

        Assert.ThrowsExactly<SecurityException>(
            () => CreateToken(tampered)
                .VerifySignature(CreateRsaKey(rsa), "portable-client"));
        Assert.ThrowsExactly<SecurityException>(
            () => CreateToken(token)
                .VerifySignature(mismatchedKey, "portable-client"));
    }

    [TestMethod]
    public void MalformedCompactTokenIsRejected()
    {
        var jwt = CreateToken("not-a-jwt");

        Assert.ThrowsExactly<SecurityException>(
            () => jwt.ReadTokenHeader());
    }

    [TestMethod]
    public void UnverifiedHeaderAndPayloadSupportValidBase64UrlRemainders()
    {
        using var rsa = RSA.Create(2048);
        var jwt = CreateToken(CreateRsaToken(rsa, audience: "portable-client"));

        Assert.AreEqual("RS256", jwt.ReadTokenHeader().GetProperty("alg").GetString());
        Assert.AreEqual(
            "Portable User",
            jwt.ReadTokenPayload(allowUnverifiedToken: true)
                .GetProperty("name")
                .GetString());
    }

    private static JsonWebToken CreateToken(string token) =>
        new(token, Metadata)
        {
            TimeProvider = new FixedTimeProvider(Now)
        };

    private static string CreateRsaToken(
        RSA rsa,
        string audience,
        long expiresInSeconds = 300)
    {
        var signingInput = CreateSigningInput(
            "RS256",
            "test-key",
            audience,
            expiresInSeconds);
        var signature = rsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string CreateEcdsaToken(ECDsa ecdsa)
    {
        var signingInput = CreateSigningInput(
            "ES256",
            "ec-key",
            "portable-client",
            expiresInSeconds: 300);
        var signature = ecdsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string CreateSigningInput(
        string algorithm,
        string keyId,
        string audience,
        long expiresInSeconds)
    {
        var now = Now.ToUnixTimeSeconds();
        var header = $$"""{"alg":"{{algorithm}}","kid":"{{keyId}}","typ":"JWT"}""";
        var payload =
            $$"""{"iss":"https://identity.example","aud":"{{audience}}","iat":{{now - 10}},"nbf":{{now - 10}},"exp":{{now + expiresInSeconds}},"name":"Portable User"}""";
        return $"{Base64Url(Encoding.UTF8.GetBytes(header))}.{Base64Url(Encoding.UTF8.GetBytes(payload))}";
    }

    private static JsonWebKeyData CreateRsaKey(RSA rsa)
    {
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        return new JsonWebKeyData
        {
            KeyType = "RSA",
            KeyId = "test-key",
            PublicKeyUse = "sig",
            Algorithm = "RS256",
            Modulus = Base64Url(parameters.Modulus!),
            Exponent = Base64Url(parameters.Exponent!)
        };
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static readonly OpenIdMetadata Metadata = new()
    {
        Issuer = "https://identity.example",
        AuthorizationEndpoint = "https://identity.example/authorize",
        DeviceAuthorizationEndpoint = "https://identity.example/device",
        TokenEndpoint = "https://identity.example/token",
        UserInfoEndpoint = "https://identity.example/userinfo",
        JwksUri = "https://identity.example/keys",
        ScopesSupported = ["openid"],
        SubjectTypesSupported = ["public"],
        IdTokenSigningAlgValuesSupported = ["RS256", "ES256"]
    };

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}

internal sealed record TestClaims
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

[JsonSerializable(typeof(TestClaims))]
internal sealed partial class JwtTestJsonContext : JsonSerializerContext;

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using fNbt;
using PCL.Core.Link.McPing;
using PCL.Core.Link.Scaffolding;
using PCL.Core.IO.Download;
using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;
using PCL.Core.Minecraft.IdentityModel.Extensions.JsonWebToken;
using PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;
using PCL.Core.Minecraft.IdentityModel.OAuth;
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
var pingValid = await VerifyPingAsync();
var lobbyCode = LobbyCodeGenerator.Generate();
var lobbyCodeValid =
    LobbyCodeGenerator.TryParse(lobbyCode) == lobbyCode &&
    LobbyCodeGenerator.GetRoomId(lobbyCode) is { Length: 8 };
var oauthValid = await VerifyOAuthAsync();
var openIdValid = await VerifyOpenIdAsync();
var yggdrasilValid = await VerifyYggdrasilAsync();
var jwtValid = VerifyJwt();
var downloadValid = await VerifyDownloadAsync();

return hashValid &&
       roundTrip == payload &&
       platformPolicyValid &&
       varIntValid &&
       encryptionValid &&
       saveValid &&
       pingValid &&
       lobbyCodeValid &&
       oauthValid &&
       openIdValid &&
       yggdrasilValid &&
       jwtValid &&
       downloadValid
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

static async Task<bool> VerifyPingAsync()
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var endpoint = (IPEndPoint)listener.LocalEndpoint;
    var server = ServeStatusAsync(listener);
    using var service = McPingServiceFactory.CreateService(endpoint, timeout: 2_000);
    var result = await service.PingAsync();
    await server;
    return result is { Description: "AOT Server", Players.Online: 1 };
}

static async Task ServeStatusAsync(TcpListener listener)
{
    using var client = await listener.AcceptTcpClientAsync();
    await using var stream = client.GetStream();
    _ = await ReadPacketAsync(stream);
    _ = await ReadPacketAsync(stream);

    const string json =
        """{"version":{"name":"AOT","protocol":772},"players":{"max":2,"online":1},"description":"AOT Server"}""";
    await stream.WriteAsync(BuildStatusPacket(json));
    var ping = await ReadPacketAsync(stream);
    _ = VarIntHelper.Decode(ping, out var offset);
    var timestamp = BinaryPrimitives.ReadInt64BigEndian(ping.AsSpan(offset));
    await stream.WriteAsync(BuildPongPacket(timestamp));
}

static async Task<byte[]> ReadPacketAsync(Stream stream)
{
    var length = checked((int)await VarIntHelper.ReadFromStreamAsync(stream));
    var packet = new byte[length];
    await stream.ReadExactlyAsync(packet);
    return packet;
}

static byte[] BuildStatusPacket(string json)
{
    var jsonBytes = Encoding.UTF8.GetBytes(json);
    var jsonLength = VarIntHelper.Encode((uint)jsonBytes.Length);
    var payload = new byte[1 + jsonLength.Length + jsonBytes.Length];
    jsonLength.CopyTo(payload, 1);
    jsonBytes.CopyTo(payload, 1 + jsonLength.Length);
    return Frame(payload);
}

static byte[] BuildPongPacket(long timestamp)
{
    var payload = new byte[9];
    payload[0] = 1;
    BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(1), timestamp);
    return Frame(payload);
}

static byte[] Frame(byte[] payload)
{
    var length = VarIntHelper.Encode((uint)payload.Length);
    var packet = new byte[length.Length + payload.Length];
    length.CopyTo(packet, 0);
    payload.CopyTo(packet, length.Length);
    return packet;
}

static async Task<bool> VerifyOAuthAsync()
{
    using var http = new HttpClient(new SmokeOAuthHandler());
    var client = new SimpleOAuthClient(new OAuthClientOptions
    {
        ClientId = "aot-client",
        RedirectUri = "https://localhost/callback",
        GetClient = () => http,
        Meta = new EndpointMeta
        {
            AuthorizeEndpoint = "https://localhost/authorize",
            DeviceEndpoint = "https://localhost/device",
            TokenEndpoint = "https://localhost/token"
        }
    });
    var result = await client.AuthorizeWithCodeAsync("aot-code", CancellationToken.None);
    return result is { AccessToken: "aot-access", ExpiresIn: 60 };
}

static async Task<bool> VerifyOpenIdAsync()
{
    using var http = new HttpClient(new SmokeOpenIdHandler());
    var options = new OpenIdOptions
    {
        OpenIdDiscoveryAddress = "https://localhost/.well-known/openid-configuration",
        ClientId = "aot-client",
        RedirectUri = "https://localhost/callback",
        GetClient = () => http
    };
    var client = new OpenIdClient(options);
    await client.InitializeAsync(CancellationToken.None, checkAddress: true);
    var key = await options.GetSignatureKeyAsync("aot-key", CancellationToken.None);
    return key is { KeyType: "RSA", Algorithm: "RS256" } &&
           client.GetAuthorizeUrl(["openid"], "aot-state")
               .Contains("code_challenge=", StringComparison.Ordinal);
}

static async Task<bool> VerifyYggdrasilAsync()
{
    using var http = new HttpClient(new SmokeYggdrasilHandler());
    var options = new YggdrasilOptions
    {
        OpenIdDiscoveryAddress = "https://localhost/.well-known/openid-configuration",
        ClientId = string.Empty,
        RedirectUri = "https://localhost/callback",
        GetClient = () => http
    };
    var client = new YggdrasilClient(options);
    await client.InitializeAsync(CancellationToken.None);
    return client.GetAuthorizeUrl(["openid"], "aot-state")
        .Contains("client_id=aot-shared-client", StringComparison.Ordinal);
}

static bool VerifyJwt()
{
    using var rsa = RSA.Create(2048);
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var header = Base64Url(Encoding.UTF8.GetBytes(
        """{"alg":"RS256","kid":"aot-jwt-key","typ":"JWT"}"""));
    var payload = Base64Url(Encoding.UTF8.GetBytes(
        $$"""{"iss":"https://localhost","aud":"aot-client","iat":{{now}},"nbf":{{now - 1}},"exp":{{now + 300}},"sub":"aot-user"}"""));
    var signingInput = $"{header}.{payload}";
    var signature = rsa.SignData(
        Encoding.ASCII.GetBytes(signingInput),
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);
    var parameters = rsa.ExportParameters(includePrivateParameters: false);
    var jwt = new JsonWebToken(
        $"{signingInput}.{Base64Url(signature)}",
        new OpenIdMetadata
        {
            Issuer = "https://localhost",
            AuthorizationEndpoint = "https://localhost/authorize",
            DeviceAuthorizationEndpoint = "https://localhost/device",
            TokenEndpoint = "https://localhost/token",
            UserInfoEndpoint = "https://localhost/userinfo",
            JwksUri = "https://localhost/keys",
            ScopesSupported = ["openid"],
            SubjectTypesSupported = ["public"],
            IdTokenSigningAlgValuesSupported = ["RS256"]
        });
    return jwt.VerifySignature(
               new JsonWebKeyData
               {
                   KeyType = "RSA",
                   KeyId = "aot-jwt-key",
                   PublicKeyUse = "sig",
                   Algorithm = "RS256",
                   Modulus = Base64Url(parameters.Modulus!),
                   Exponent = Base64Url(parameters.Exponent!)
               },
               "aot-client") &&
           jwt.GetClaimValue("sub") == "aot-user";
}

static async Task<bool> VerifyDownloadAsync()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        $"pcl-aot-download-{Guid.NewGuid():N}");
    var destination = Path.Combine(directory, "artifact.bin");
    var expected = "portable-download-aot"u8.ToArray();
    using var http = new HttpClient(new SmokeDownloadHandler(expected));

    try
    {
        var result = await new DownloadService().DownloadAsync(
            new DownloadRequest
            {
                Sources =
                [
                    "https://localhost/fail",
                    "https://localhost/success"
                ],
                DestinationPath = destination,
                ConnectionFactory = url => new HttpDlConnection(http, url)
            });
        var actual = await File.ReadAllBytesAsync(destination);
        return result is
               {
                   Success: true,
                   SuccessfulSource: "https://localhost/success",
                   Errors.Count: 1
               } &&
               expected.AsSpan().SequenceEqual(actual);
    }
    finally
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}

static string Base64Url(byte[] value) =>
    Convert.ToBase64String(value)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

internal sealed record SmokePayload(string Name, int RuntimeMajor);

[JsonSerializable(typeof(SmokePayload))]
internal sealed partial class SmokeJsonContext : JsonSerializerContext;

internal sealed class SmokeOAuthHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"access_token":"aot-access","expires_in":"60"}""",
                Encoding.UTF8,
                "application/json")
        });
}

internal sealed class SmokeOpenIdHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var json = request.RequestUri?.AbsolutePath == "/keys"
            ? """{"keys":[{"kty":"RSA","kid":"aot-key","alg":"RS256","n":"AQ","e":"AQAB"}]}"""
            : """
              {
                "issuer":"https://localhost",
                "authorization_endpoint":"https://localhost/authorize",
                "device_authorization_endpoint":"https://localhost/device",
                "token_endpoint":"https://localhost/token",
                "userinfo_endpoint":"https://localhost/userinfo",
                "jwks_uri":"https://localhost/keys",
                "scopes_supported":["openid"],
                "subject_types_supported":["public"],
                "id_token_signing_alg_values_supported":["RS256"]
              }
              """;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }
}

internal sealed class SmokeYggdrasilHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "issuer":"https://localhost",
                  "authorization_endpoint":"https://localhost/authorize",
                  "device_authorization_endpoint":"https://localhost/device",
                  "token_endpoint":"https://localhost/token",
                  "userinfo_endpoint":"https://localhost/userinfo",
                  "jwks_uri":"https://localhost/keys",
                  "scopes_supported":[
                    "openid",
                    "Yggdrasil.PlayerProfiles.Select",
                    "Yggdrasil.Server.Join"
                  ],
                  "subject_types_supported":["public"],
                  "id_token_signing_alg_values_supported":["RS256"],
                  "shared_client_id":"aot-shared-client"
                }
                """,
                Encoding.UTF8,
                "application/json")
        });
}

internal sealed class SmokeDownloadHandler(byte[] content) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsolutePath == "/fail")
            return Task.FromResult(new HttpResponseMessage(
                HttpStatusCode.ServiceUnavailable));

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        });
    }
}

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
using fNbt;
using PCL.Core.Link.McPing;
using PCL.Core.Link.Scaffolding;
using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;
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

return hashValid &&
       roundTrip == payload &&
       platformPolicyValid &&
       varIntValid &&
       encryptionValid &&
       saveValid &&
       pingValid &&
       lobbyCodeValid &&
       oauthValid &&
       openIdValid
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

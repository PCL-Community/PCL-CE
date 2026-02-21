using System;
using PCL.Core.Utils.Exts;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Utils.Hash;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.Pkce;

/// <summary>
/// 带 PKCE 支持的客户端 <br/>
/// 此客户端并非线程安全，请勿在多个线程间共享示例
/// </summary>
/// <param name="options"></param>
public class PkceClient(OAuthClientOptions options):IOAuthClient
{
    private byte[] _ChallengeCode { get; set; } = new byte[32];
    private bool _isCallGetAuthorizeUrl;
    public PkceChallengeOptions ChallengeMethod { get; private set; } = PkceChallengeOptions.Sha256;
    private readonly SimpleOAuthClient _client = new(options);
    public string GetAuthorizeUrl(string[] scopes, string redirectUri, string state, Dictionary<string, string>? extData)
    {
        RandomNumberGenerator.Fill(_ChallengeCode);
        var hash = SHA256Provider.Instance.ComputeHash(_ChallengeCode);
        extData ??= [];
        extData["code_challenge"] = hash;
        extData["code_challenge_method"] = "S256";
        _isCallGetAuthorizeUrl = true;
        return _client.GetAuthorizeUrl(scopes, redirectUri, state, extData);
    }

    public async Task<AuthorizeResult?> AuthorizeWithCodeAsync(string code, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (!_isCallGetAuthorizeUrl) throw new InvalidOperationException("Challenge code is invalid");
        var pkce = _ChallengeCode.FromBytesToB64UrlSafe();
        extData ??= [];
        extData["code_verifier"] = pkce;
        _isCallGetAuthorizeUrl = false;
        return await _client.AuthorizeWithCodeAsync(code, token, extData);
    }

    public async Task<DeviceCodeData?> GetCodePairAsync(string[] scopes, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        return await _client.GetCodePairAsync(scopes, token, extData);
    }

    public async Task<AuthorizeResult?> AuthorizeWithDeviceAsync(DeviceCodeData data, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        return await _client.AuthorizeWithDeviceAsync(data, token, extData);
    }

    public async Task<AuthorizeResult?> AuthorizeWithSilentAsync(AuthorizeResult data, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        return await _client.AuthorizeWithSilentAsync(data, token, extData);
    }
}
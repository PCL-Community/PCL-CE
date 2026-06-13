// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.Pkce;

/// <summary>
/// 带 PKCE 支持的客户端。此客户端并非线程安全，请勿在多个线程间共享实例。
/// </summary>
public sealed class PkceClient(OAuthClientOptions options) : IOAuthClient
{
    private readonly byte[] _challengeCode = new byte[32];
    private readonly SimpleOAuthClient _client = new(options);
    private string? _codeVerifier;
    private bool _hasAuthorizationRequest;

    /// <summary>
    /// 设置验证方法，支持 PlainText 和 SHA256。
    /// </summary>
    public PkceChallengeOptions ChallengeMethod { get; private set; } = PkceChallengeOptions.Sha256;

    public string GetAuthorizeUrl(
        string[] scopes,
        string state,
        Dictionary<string, string>? extData)
    {
        RandomNumberGenerator.Fill(_challengeCode);
        _codeVerifier = _challengeCode.FromBytesToB64UrlSafe();

        extData ??= [];
        extData["code_challenge"] = ChallengeMethod == PkceChallengeOptions.Sha256
            ? CreateS256Challenge(_codeVerifier)
            : _codeVerifier;
        extData["code_challenge_method"] =
            ChallengeMethod == PkceChallengeOptions.Sha256 ? "S256" : "plain";

        _hasAuthorizationRequest = true;
        return _client.GetAuthorizeUrl(scopes, state, extData);
    }

    public async Task<AuthorizeResult?> AuthorizeWithCodeAsync(
        string code,
        CancellationToken token,
        Dictionary<string, string>? extData = null)
    {
        if (!_hasAuthorizationRequest || _codeVerifier is null)
            throw new InvalidOperationException("Challenge code is invalid");

        extData ??= [];
        extData["code_verifier"] = _codeVerifier;
        _codeVerifier = null;
        _hasAuthorizationRequest = false;
        Array.Clear(_challengeCode);

        return await _client
            .AuthorizeWithCodeAsync(code, token, extData)
            .ConfigureAwait(false);
    }

    public Task<DeviceCodeData?> GetCodePairAsync(
        string[] scopes,
        CancellationToken token,
        Dictionary<string, string>? extData = null) =>
        _client.GetCodePairAsync(scopes, token, extData);

    public Task<AuthorizeResult?> AuthorizeWithDeviceAsync(
        DeviceCodeData data,
        CancellationToken token,
        Dictionary<string, string>? extData = null) =>
        _client.AuthorizeWithDeviceAsync(data, token, extData);

    public Task<AuthorizeResult?> AuthorizeWithSilentAsync(
        AuthorizeResult data,
        CancellationToken token,
        Dictionary<string, string>? extData = null) =>
        _client.AuthorizeWithSilentAsync(data, token, extData);

    internal static string CreateS256Challenge(string codeVerifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);
        if (codeVerifier.Length is < 43 or > 128)
            throw new ArgumentOutOfRangeException(
                nameof(codeVerifier),
                "PKCE code verifier length must be between 43 and 128 characters.");

        Span<byte> verifierBytes = stackalloc byte[128];
        var byteCount = Encoding.ASCII.GetBytes(codeVerifier, verifierBytes);
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.TryHashData(verifierBytes[..byteCount], hash, out _);

        Span<char> base64 = stackalloc char[44];
        Convert.TryToBase64Chars(hash, base64, out var charsWritten);
        while (charsWritten > 0 && base64[charsWritten - 1] == '=')
            charsWritten--;
        for (var index = 0; index < charsWritten; index++)
        {
            base64[index] = base64[index] switch
            {
                '+' => '-',
                '/' => '_',
                var value => value
            };
        }

        return new string(base64[..charsWritten]);
    }
}

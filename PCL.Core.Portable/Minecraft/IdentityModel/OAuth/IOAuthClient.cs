// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.IdentityModel.OAuth;

public interface IOAuthClient
{
    public string GetAuthorizeUrl(string[] scopes,string state,Dictionary<string,string>? extData);
    public Task<AuthorizeResult?> AuthorizeWithCodeAsync(string code,CancellationToken token,Dictionary<string,string>? extData = null);
    public Task<DeviceCodeData?> GetCodePairAsync(string[] scopes,CancellationToken token, Dictionary<string, string>? extData = null);
    public Task<AuthorizeResult?> AuthorizeWithDeviceAsync(DeviceCodeData data,CancellationToken token,Dictionary<string,string>? extData = null);
    public Task<AuthorizeResult?> AuthorizeWithSilentAsync(AuthorizeResult data,CancellationToken token,Dictionary<string,string>? extData = null);
}

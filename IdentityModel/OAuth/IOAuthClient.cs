using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PCL.Core.IdentityModel.OAuth;

public interface IOAuthClient
{
    public string GetAuthorizeUrl(string[] scopes,string redirectUri,string state);
    public Task<AuthorizeResult?> AuthorizeWithCodeAsync(string code,CancellationToken token,Dictionary<string,string>? extData = null);
    public Task<DeviceCodeData?> GetCodePairAsync(string[] scopes,CancellationToken token, Dictionary<string, string>? extData = null);
    public Task<AuthorizeResult?> AuthorizeWithDeviceAsync(DeviceCodeData data,CancellationToken token,Dictionary<string,string>? extData = null);
    public Task<AuthorizeResult?> AuthorizeWithSilentAsync(AuthorizeResult data,CancellationToken token,Dictionary<string,string>? extData = null);
}
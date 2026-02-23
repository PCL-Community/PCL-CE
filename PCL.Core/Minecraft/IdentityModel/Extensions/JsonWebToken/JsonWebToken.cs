using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.JsonWebToken;

/// <summary>
/// Json Web Token 类
/// </summary>
/// <param name="token"></param>
/// <param name="meta"></param>
public class JsonWebToken(string token,OpenIdMetadata meta)
{
    public delegate SecurityToken? TokenValidateCallback(OpenIdMetadata metadata,string token, JsonWebKey key,string clientId);

    public TokenValidateCallback SecurityTokenValidateCallback { get; set; } = static (meta,token, key,clientId) =>
    {
        var handler = new JwtSecurityTokenHandler();
        var parameter = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = meta.Issuer,
            ValidateAudience = true,
            ValidAudience = clientId,
        };
        handler.ValidateToken(token, parameter, out var secToken);
        return secToken;
    };
    
    private bool _verified;
    /// <summary>
    /// 尝试读取 Token 中的字段
    /// </summary>
    /// <param name="allowUnverifyToken">是否允许在未验证的情况下读取字段，若为 false，当 Token 未验证时将抛出异常</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="SecurityException">未调用 <see cref="VerifySignature"/></exception>
    public T ReadTokenPayload<T>(bool allowUnverifyToken = false)
    {
        throw new NotImplementedException();
    }
    /// <summary>
    /// 读取 Token 头
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T ReadTokenHeader<T>()
    {
        throw new NotImplementedException();
    }
    /// <summary>
    /// 对 Token 进行签名验证 <br/>
    /// 默认情况下仅对签名、iss、nbf、exp 进行验证，如果需要更细粒度验证，请设置 <see cref="SecurityTokenValidateCallback"/>
    /// </summary>
    /// <returns></returns>
    public SecurityToken? TryVerifySignature(JsonWebKey key,string clientId)
    {
        return SecurityTokenValidateCallback.Invoke(meta, token, key, clientId);
    }
}
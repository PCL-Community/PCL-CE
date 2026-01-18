using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PCL.Core.Account.OAuth;
public static class PKCE
{
    public struct CodeChallenge
    {
        public string Verifier;
        public string Challenge;
        public string Method;
    }

    public const string CodeVerifyMap = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
    public static CodeChallenge GenerateChallenge()
    {
        var verify = RandomNumberGenerator.GetString(CodeVerifyMap, 43);
        var code = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(verify)))
            .Replace("+", "-").Replace("/", "-").Replace("=", "");
        return new CodeChallenge {
            Verifier = verify,
            Challenge = code,
            Method = "S256"
        };
    } 
}

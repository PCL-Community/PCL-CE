using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCL.Core.Minecraft.IdentityModel.Yggdrasil;

public class YggdrasilLegacyClient
{
    public string? User;
    public string? Password;
    public string? AccessToken;
    public YggdrasilLegacyClient(string username,string password)
    {

    }

    public async Task<object> AuthenticateAsync()
    {

    }
}
using PCL.Core.Minecraft.Profile.Autnenrication;

namespace PCL.Core.Minecraft.Profile.Models;

public class Profile
{
    public IAuthenticateProvider? CreateAuthenticateServiceProvider()
    {
        return default;
    }
}
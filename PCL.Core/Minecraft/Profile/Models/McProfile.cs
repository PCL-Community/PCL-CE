using PCL.Core.Minecraft.Profile.Autnenrication;

namespace PCL.Core.Minecraft.Profile.Models;

internal record McProfile: SafeProfile
{
    
    public static IAuthenticateProvider? CreateAuthenticateServiceProvider()
    {
        return default;
    }
}
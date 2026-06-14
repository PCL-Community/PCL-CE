using PCL.Core.Minecraft.Profile.Autnenrication;

namespace PCL.Core.Minecraft.Profile.Models;

internal record Profile: SafeProfile
{
    public IAuthenticateProvider? CreateAuthenticateServiceProvider()
    {
        return default;
    }
}
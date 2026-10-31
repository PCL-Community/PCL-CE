using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Minecraft.Profile.Authentication;

namespace PCL;

public sealed class WpfProfileUi : IProfileUi
{
    public object? GetLoginData()
    {
        return ProfileUi.GetLoginData();
    }

    public string IsProfileValid()
    {
        return ProfileUi.IsProfileValid();
    }

    public Task<AuthorizeResult?> ShowDeviceCodeLoginAsync(
        DeviceCodeAuthenticationContext context,
        CancellationToken token)
    {
        return ProfileUi.ShowDeviceCodeLoginAsync(context, token);
    }

    public bool HandleMicrosoftXstsError(IdentityModelAuthenticationException exception)
    {
        return ProfileUi.HandleMicrosoftXstsError(exception);
    }

    public bool HandleMicrosoftNotOwnedError(IdentityModelAuthenticationException exception)
    {
        return ProfileUi.HandleMicrosoftNotOwnedError(exception);
    }

    public bool HandleMicrosoftCreateProfileError(IdentityModelAuthenticationException exception)
    {
        return ProfileUi.HandleMicrosoftCreateProfileError(exception);
    }
}
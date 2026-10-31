using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Minecraft.Profile.Authentication;

namespace PCL;

/// <summary>登录档案 UI 交互。</summary>
public interface IProfileUi
{
    object? GetLoginData();
    string IsProfileValid();

    Task<AuthorizeResult?> ShowDeviceCodeLoginAsync(
        DeviceCodeAuthenticationContext context,
        CancellationToken token);

    bool HandleMicrosoftXstsError(IdentityModelAuthenticationException exception);
    bool HandleMicrosoftNotOwnedError(IdentityModelAuthenticationException exception);
    bool HandleMicrosoftCreateProfileError(IdentityModelAuthenticationException exception);
}

public static class LoginProfile
{
    public static IProfileUi? Current { get; set; }

    public static object? GetLoginData()
    {
        return Current?.GetLoginData();
    }

    public static string IsProfileValid()
    {
        return Current?.IsProfileValid() ?? "";
    }

    public static Task<AuthorizeResult?> ShowDeviceCodeLoginAsync(
        DeviceCodeAuthenticationContext context,
        CancellationToken token)
    {
        return Current?.ShowDeviceCodeLoginAsync(context, token)
            ?? Task.FromResult<AuthorizeResult?>(null);
    }

    public static bool HandleMicrosoftXstsError(IdentityModelAuthenticationException ex)
    {
        return Current?.HandleMicrosoftXstsError(ex) ?? false;
    }

    public static bool HandleMicrosoftNotOwnedError(IdentityModelAuthenticationException ex)
    {
        return Current?.HandleMicrosoftNotOwnedError(ex) ?? false;
    }

    public static bool HandleMicrosoftCreateProfileError(IdentityModelAuthenticationException ex)
    {
        return Current?.HandleMicrosoftCreateProfileError(ex) ?? false;
    }
}
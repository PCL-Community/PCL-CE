using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;

namespace PCL.Core.Minecraft.Profile.Autnenrication;

public class MicrosoftProviderBuilder
{
    private MicrosoftProviderBuilder(){}

    private OpenIdClient? _client;

    public MicrosoftProviderBuilder Create() => new MicrosoftProviderBuilder();

    public MicrosoftProviderBuilder SetOptions(OpenIdOptions options)
    {
        _client = new OpenIdClient(options);
        return this;
    } 
    

}
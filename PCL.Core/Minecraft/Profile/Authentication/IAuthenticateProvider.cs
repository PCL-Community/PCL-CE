using System.Threading.Tasks;

namespace PCL.Core.Minecraft.Profile.Authentication;

public interface IAuthenticateProvider
{
    //public Task AuthenticateAsync();

    public void RefreshInfo();
}
using PCL.Core.Minecraft.Profile.Models;

namespace PCL.Core.Minecraft.Profile;

public class ProfileManagement<TProfileModel>: IProfileManagement<TProfileModel>
    where TProfileModel:SafeProfile
{
    private ProfileJson<TProfileModel> _profiles = new();
    
    public SafeProfile Current { get; set; }
    
    public void Add(TProfileModel profile)
    {
        _profiles.Profiles = _profiles.Profiles.Add(profile);
    }

    public void Delete(TProfileModel profile)
    {
        _profiles.Profiles = _profiles.Profiles.Remove(profile);
    }

    public void Update(TProfileModel origin ,TProfileModel current)
    {
        if (origin == Current) Current = current;
        
    }

    public void LoadFromPath(string path)
    {
        throw new System.NotImplementedException();
    }

    public void LoadFromString(string profiles)
    {
        throw new System.NotImplementedException();
    }

    public void Clear(bool deleteLocal = false)
    {
        throw new System.NotImplementedException();
    }
}
using System.IO;
using PCL.Core.Minecraft.Profile.Models;

namespace PCL.Core.Minecraft.Profile;

public class ProfileManagement<T>: IProfileManagement<T> where T:class
{

    private ProfileJson<T>? _profiles;
    public T Create()
    {
        throw new System.NotImplementedException();
    }

    public void Delete(int index)
    {
        throw new System.NotImplementedException();
    }

    public void Update(T profile)
    {
        throw new System.NotImplementedException();
    }

    public void LoadFromPath(string path)
    {
        LoadFromString(File.ReadAllText(path));
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
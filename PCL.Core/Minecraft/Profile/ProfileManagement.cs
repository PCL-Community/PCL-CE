namespace PCL.Core.Minecraft.Profile;

public class ProfileManagement<T>: IProfileManagement<T> where T:class
{
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
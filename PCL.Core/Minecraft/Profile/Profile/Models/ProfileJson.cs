using System.Collections.Immutable;

namespace PCL.Core.Minecraft.Profile.Models;

public class ProfileJson<T> where T : class
{
    public int LastUsed { get; set; }
    public ImmutableArray<T> Profiles { get; private set; } = [];
}
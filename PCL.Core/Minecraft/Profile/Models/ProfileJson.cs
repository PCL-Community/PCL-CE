using System.Collections.Immutable;

namespace PCL.Core.Minecraft.Profile.Models;

public class ProfileJson<T>
{
    public int LastUsed { get; set; }
    public ImmutableArray<T> Profiles { get; internal set; } = [];
}
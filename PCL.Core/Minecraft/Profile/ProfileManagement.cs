using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PCL.Core.Minecraft.Profile.Models;

namespace PCL.Core.Minecraft.Profile;

public class ProfileManagement<TProfileModel>: IProfileManagement<TProfileModel>
    where TProfileModel:SafeProfile
{
    
    private ProfileJson<TProfileModel>? _profiles;
    private object _syncLock = new();
    /// <summary>
    /// 当前档案
    /// </summary>
    public SafeProfile? Current { get; private set; }
    
    public void Add(TProfileModel profile)
    {
        if (_profiles is null) throw new InvalidOperationException("Must load the profile list before modifying it.");
        _profiles.Profiles = _profiles.Profiles.Add(profile);
    }

    public void Delete(TProfileModel profile)
    {
        if (_profiles is null) throw new InvalidOperationException("Must load the profile list before modifying it.");
        _profiles.Profiles = _profiles.Profiles.Remove(profile);
    }

    public void Update(TProfileModel origin ,TProfileModel current)
    {
        if (_profiles is null) throw new InvalidOperationException("Must load the profile list before modifying it.");
        if (origin == Current) Current = current;
        var index = _profiles.Profiles.IndexOf(origin);
        _profiles.Profiles = _profiles.Profiles.SetItem(index, current);
    }

    public void LoadFromPath(string path)
    {
        lock(_syncLock)
        {
            if (Current is not null) return;
            _profiles = JsonSerializer.Deserialize<ProfileJson<TProfileModel>>(File.ReadAllText(path));

        }
    }

    public void LoadFromString(string profiles)
    {
        lock (_syncLock)
        {
            if (Current is not null) return;
            _profiles = JsonSerializer.Deserialize<ProfileJson<TProfileModel>>(profiles);
        }
    }

    public void Clear()
    {
        _profiles?.Profiles = _profiles.Profiles.Clear();
    }

    public IEnumerable<TProfileModel> GetAll()
    {
        if (_profiles is null) throw new InvalidOperationException("Profile list was not loaded.");
        return _profiles.Profiles;
    }
}
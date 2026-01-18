using PCL.Core.Link.Scaffolding.Client.Models;
using System;

namespace PCL.Core.Link.Scaffolding.Server;

public record TrackedPlayerProfile
{
    public required PlayerProfile Profile { get; set; }
    public required DateTime LastSeenUtc { get; set; }
};
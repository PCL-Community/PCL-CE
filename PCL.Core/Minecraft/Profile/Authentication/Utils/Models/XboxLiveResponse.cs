using System;

namespace PCL.Core.Minecraft.Profile.Authentication.Utils.Models;

public record XboxLiveResponse
{
    public required DateTime IssueInstant { get; set; }
    public required DateTime NotAfter { get; set; }
    public required string Token { get; set; }
}
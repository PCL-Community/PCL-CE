using System;
using System.Text.Json.Serialization;
using PCL.Core.Minecraft.Profile.Authentication;

namespace PCL.Core.Minecraft.Profile.Models;

internal record McProfile: SafeProfile
{
    [JsonPropertyName("refreshToken")]
    public required string RefreshToken { get; set; }
    [JsonPropertyName("skinPath")]
    public required string SkinPath { get; set; }
    [JsonPropertyName("expires")]
    public required DateTime ExpiredAt { get; set; }
    [JsonPropertyName("profileDetails")]
    public object? ProfileDetails { get; set; }
    
    // Yggdrasil
    
    [JsonPropertyName("server")]
    public string? YggdrasilApiServerAddress { get; set; }
    
    //
    
    public static IAuthenticateProvider? CreateAuthenticateServiceProvider()
    {
        return default;
    }
}
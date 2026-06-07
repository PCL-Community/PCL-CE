namespace PCL.Core.Minecraft.Profile.Autnenrication.Utils.Models;

public record XboxAuthenticate<T>
{
    public required T Properties { get; set; }
    public string RelyingParty { get; set; } = "http://auth.xboxlive.com";
    public required string TokenType { get; set; }
}
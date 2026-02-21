namespace PCL.Core.Minecraft.IdentityModel.OAuth;

public record EndpointMeta
{
    public string? DeviceEndpoint { get; set; }
    public required string AuthorizeEndpoint { get; set; }
    public required string TokenEndpoint { get; set; }
}
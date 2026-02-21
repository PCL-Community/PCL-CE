namespace PCL.Core.Link.McPing.Model;

public record McPingResult(
    McPingVersionResult Version,
    McPingPlayerResult Players,
    string Description,
    string Favicon,
    long Latency,
    McPingModInfoResult? ModInfo);

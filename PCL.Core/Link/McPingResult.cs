using System.Collections.Generic;

namespace PCL.Core.Link;

public record McPingResult(
    McPingVersionResult Version,
    McPingPlayerResult Players,
    string Description,
    string Favicon,
    long Latency,
    McPingModInfoResult? ModInfo);

public record McPingVersionResult(
    string Name,
    int Protocol);

public record McPingPlayerResult(
    int Max,
    int Online,
    List<McPingPlayerSampleResult> Samples);

public record McPingPlayerSampleResult(
    string Name,
    string Id);

public record McPingModInfoResult(
    string Type,
    List<McPingModInfoModResult> ModList);

public record McPingModInfoModResult(
    string Id,
    string Version);
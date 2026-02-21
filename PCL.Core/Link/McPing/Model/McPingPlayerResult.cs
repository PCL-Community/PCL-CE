using System.Collections.Generic;

namespace PCL.Core.Link.McPing.Model;

public record McPingPlayerResult(
    int Max,
    int Online,
    List<McPingPlayerSampleResult> Samples);

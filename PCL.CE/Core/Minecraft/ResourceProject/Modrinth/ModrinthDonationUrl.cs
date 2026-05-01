using System;

namespace PCL.CE.Core.Minecraft.ResourceProject.Modrinth;

[Serializable]
public record ModrinthDonationUrl(
    string id,
    string platform,
    string url);
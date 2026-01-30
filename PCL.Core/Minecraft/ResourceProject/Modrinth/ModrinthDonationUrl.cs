using System;

namespace PCL.Core.Minecraft.ResourceProject.Modrinth;

[Serializable]
public record ModrinthDonationUrl(
    string id,
    string platform,
    string url);
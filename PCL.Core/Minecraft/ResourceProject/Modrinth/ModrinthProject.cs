using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.ResourceProject.Modrinth;

[Serializable]
public record class ModrinthProject(
    string slug,
    string title,
    string description,
    List<string> categories,
    string client_side,
    string server_side,
    string body,
    string status,
    string? requested_status,
    List<string> additional_categories,
    string? issues_url,
    string? source_url,
    string? wiki_url,
    string? discord_url,
    List<ModrinthDonationUrl> donation_urls,
    string project_type,
    int downloads,
    string icon_url,
    int color,
    string thread_id,
    string monetization_status,
    string id,
    string team,
    string body_url,
    ModrinthModeratorMessage moderator_message,
    string published,
    string updated,
    string? approved,
    string? queued,
    int followers,
    ModrinthLicense license,
    List<string> versions,
    List<string> game_versions,
    List<string> loaders,
    List<object> gallery);
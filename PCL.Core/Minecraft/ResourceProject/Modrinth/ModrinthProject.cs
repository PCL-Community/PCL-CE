using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Modrinth;

[Serializable]
public record class ModrinthProject(
    string slug,
    string title,
    string description,
    List<string> categories,
    [property: JsonPropertyName("client_side")] string clientSide,
    [property: JsonPropertyName("server_side")] string serverSide,
    string body,
    string status,
    [property: JsonPropertyName("requested_status")] string? requestedStatus,
    [property: JsonPropertyName("additional_categories")] List<string> additionalCategories,
    [property: JsonPropertyName("issues_url")] string? issuesUrl,
    [property: JsonPropertyName("source_url")] string? sourceUrl,
    [property: JsonPropertyName("wiki_url")] string? wikiUrl,
    [property: JsonPropertyName("discord_url")] string? discordUrl,
    [property: JsonPropertyName("donation_urls")] List<ModrinthDonationUrl> donationUrls,
    [property: JsonPropertyName("project_type")] string projectType,
    int downloads,
    [property: JsonPropertyName("icon_url")] string iconUrl,
    int color,
    [property: JsonPropertyName("thread_id")] string threadId,
    [property: JsonPropertyName("monetization_status")] string monetizationStatus,
    string id,
    string team,
    [property: JsonPropertyName("body_url")] string bodyUrl,
    [property: JsonPropertyName("moderator_message")] ModrinthModeratorMessage moderatorMessage,
    string published,
    string updated,
    string? approved,
    string? queued,
    int followers,
    ModrinthLicense license,
    List<string> versions,
    [property: JsonPropertyName("game_versions")] List<string> gameVersions,
    List<string> loaders,
    List<object> gallery);
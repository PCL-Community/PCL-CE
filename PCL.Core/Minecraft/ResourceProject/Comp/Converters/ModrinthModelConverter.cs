using System;
using System.Collections.Generic;
using System.Text.Json;
using PCL.Core.Minecraft.ResourceProject.Comp.Models;
using PCL.Core.Minecraft.ResourceProject.Comp.Models.Enums;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Converters;

public static class ModrinthModelConverter
{

    public static CompProject ToProject(JsonElement data)
    {
        // Search hits use "project_id", full project uses "id"
        var id = data.TryGetProperty("project_id", out var pidEl)
            ? pidEl.GetString() ?? ""
            : data.TryGetProperty("id", out var idEl)
                ? idEl.GetString() ?? ""
                : "";

        var slug = data.GetProperty("slug").GetString() ?? "";
        var title = data.GetProperty("title").GetString() ?? "";
        var description = data.GetProperty("description").GetString() ?? "";

        string? bodyHtml = null;
        if (data.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String)
            bodyHtml = body.GetString();

        Uri? iconUrl = null;
        if (data.TryGetProperty("icon_url", out var icon) && icon.ValueKind == JsonValueKind.String)
        {
            var iconStr = icon.GetString();
            if (!string.IsNullOrEmpty(iconStr) && Uri.TryCreate(iconStr, UriKind.Absolute, out var uri))
                iconUrl = uri;
        }

        var categories = new List<string>();
        if (data.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in cats.EnumerateArray())
            {
                var cStr = c.GetString();
                if (!string.IsNullOrEmpty(cStr)) categories.Add(cStr);
            }
        }

        // Search returns "versions", full project returns "game_versions"
        var gameVersions = new List<string>();
        JsonElement gvEl;
        if (data.TryGetProperty("game_versions", out gvEl) && gvEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var gv in gvEl.EnumerateArray())
            {
                var gvStr = gv.GetString();
                if (!string.IsNullOrEmpty(gvStr)) gameVersions.Add(gvStr);
            }
        }
        else if (data.TryGetProperty("versions", out gvEl) && gvEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var gv in gvEl.EnumerateArray())
            {
                var gvStr = gv.GetString();
                if (!string.IsNullOrEmpty(gvStr)) gameVersions.Add(gvStr);
            }
        }

        var downloads = data.TryGetProperty("downloads", out var dn) ? dn.GetInt32() : 0;

        // Search returns "follows", full project returns "followers"
        var followers = data.TryGetProperty("followers", out var fw)
            ? fw.GetInt32()
            : data.TryGetProperty("follows", out var fl)
                ? fl.GetInt32()
                : 0;

        var projectType = data.TryGetProperty("project_type", out var ptEl)
            ? _MapProjectType(ptEl.GetString())
            : CompProjectType.Mod;

        // License: search returns string, full project returns object
        string? license = null;
        if (data.TryGetProperty("license", out var lic))
        {
            if (lic.ValueKind == JsonValueKind.Object)
            {
                if (lic.TryGetProperty("id", out var licId))
                    license = licId.GetString();
            }
            else if (lic.ValueKind == JsonValueKind.String)
            {
                license = lic.GetString();
            }
        }

        // Dates: search uses date_created/date_modified, full uses published/updated
        var datePublished = _ParseDateTime(data, "published") != DateTime.MinValue
            ? _ParseDateTime(data, "published")
            : _ParseDateTime(data, "date_created");
        var dateUpdated = _ParseDateTime(data, "updated") != DateTime.MinValue
            ? _ParseDateTime(data, "updated")
            : _ParseDateTime(data, "date_modified");

        string? author = null;
        if (data.TryGetProperty("author", out var au) && au.ValueKind == JsonValueKind.String)
            author = au.GetString();

        Uri? issuesUrl = null;
        if (data.TryGetProperty("issues_url", out var iu) && iu.ValueKind == JsonValueKind.String)
        {
            var iuStr = iu.GetString();
            if (!string.IsNullOrEmpty(iuStr) && Uri.TryCreate(iuStr, UriKind.Absolute, out var uri))
                issuesUrl = uri;
        }

        Uri? sourceUrl = null;
        if (data.TryGetProperty("source_url", out var su) && su.ValueKind == JsonValueKind.String)
        {
            var suStr = su.GetString();
            if (!string.IsNullOrEmpty(suStr) && Uri.TryCreate(suStr, UriKind.Absolute, out var uri))
                sourceUrl = uri;
        }

        // Search hits don't have "status", default to Approved
        var status = data.TryGetProperty("status", out var stEl) && stEl.ValueKind == JsonValueKind.String
            ? _MapStatus(stEl.GetString())
            : CompProjectStatus.Approved;

        return new CompProject(
            id, "Modrinth", slug, title, description, bodyHtml, iconUrl,
            categories, gameVersions, downloads, followers,
            projectType, license, datePublished, dateUpdated,
            author, null, issuesUrl, sourceUrl, status);
    }

    public static CompFile ToFile(JsonElement data)
    {
        var id = data.GetProperty("id").GetString() ?? "";
        var projectId = data.TryGetProperty("project_id", out var pid)
            ? pid.GetString() ?? ""
            : "";

        var displayName = data.GetProperty("name").GetString() ?? "";

        string? fileName = null;
        Uri? downloadUrl = null;
        long fileLength = 0;
        var hashes = new Dictionary<HashAlgorithm, string>();

        if (data.TryGetProperty("files", out var filesArr) && filesArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in filesArr.EnumerateArray())
            {
                fileName ??= f.GetProperty("filename").GetString();

                if (downloadUrl is null && f.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
                {
                    var urlStr = url.GetString();
                    if (!string.IsNullOrEmpty(urlStr) && Uri.TryCreate(urlStr, UriKind.Absolute, out var uri))
                        downloadUrl = uri;
                }

                if (f.TryGetProperty("size", out var sz))
                    fileLength = sz.GetInt64();

                if (f.TryGetProperty("hashes", out var hs) && hs.ValueKind == JsonValueKind.Object)
                {
                    foreach (var h in hs.EnumerateObject())
                    {
                        var algo = h.Name switch
                        {
                            "sha1" => HashAlgorithm.Sha1,
                            "sha512" => HashAlgorithm.Sha512,
                            "md5" => HashAlgorithm.Md5,
                            _ => (HashAlgorithm?)null
                        };
                        if (algo.HasValue)
                        {
                            var val = h.Value.GetString();
                            if (!string.IsNullOrEmpty(val))
                                hashes[algo.Value] = val;
                        }
                    }
                }
            }
        }

        var releaseType = data.TryGetProperty("version_type", out var vt)
            ? vt.GetString() ?? "release"
            : "release";

        var gameVersions = new List<string>();
        if (data.TryGetProperty("game_versions", out var gvs) && gvs.ValueKind == JsonValueKind.Array)
        {
            foreach (var gv in gvs.EnumerateArray())
            {
                var gvStr = gv.GetString();
                if (!string.IsNullOrEmpty(gvStr)) gameVersions.Add(gvStr);
            }
        }

        var loaders = new List<ModLoaderType>();
        if (data.TryGetProperty("loaders", out var lds) && lds.ValueKind == JsonValueKind.Array)
        {
            foreach (var l in lds.EnumerateArray())
            {
                var loader = l.GetString();
                if (!string.IsNullOrEmpty(loader))
                    loaders.Add(_MapLoaderString(loader));
            }
        }

        var dependencies = new List<CompFileDependency>();
        if (data.TryGetProperty("dependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
        {
            foreach (var d in deps.EnumerateArray())
            {
                var depProjectId = d.TryGetProperty("project_id", out var dpid)
                    ? dpid.GetString() ?? ""
                    : "";
                var depFileName = d.TryGetProperty("file_name", out var dfn)
                    ? dfn.GetString()
                    : null;
                var relationType = d.GetProperty("dependency_type").GetString() ?? "required";
                dependencies.Add(new CompFileDependency(depFileName ?? depProjectId, depProjectId, relationType));
            }
        }

        string? changelog = null;
        if (data.TryGetProperty("changelog", out var cl) && cl.ValueKind == JsonValueKind.String)
            changelog = cl.GetString();

        var datePublished = _ParseDateTime(data, "date_published");
        var downloadCount = data.TryGetProperty("downloads", out var dnl) ? dnl.GetInt32() : 0;

        var isAvailable = data.TryGetProperty("status", out var st)
            ? st.GetString() == "listed"
            : true;

        return new CompFile(
            id, projectId, displayName, fileName ?? "", downloadUrl,
            fileLength, releaseType, gameVersions, loaders, hashes,
            dependencies, changelog, datePublished, downloadCount, isAvailable);
    }

    public static CompSearchResult ToSearchResult(JsonElement root, int offset, int limit)
    {
        var hits = root.GetProperty("hits");
        var totalCount = root.TryGetProperty("total_hits", out var tc) ? tc.GetInt32() : 0;

        var projects = new List<CompProject>();
        if (hits.ValueKind == JsonValueKind.Array)
        {
            foreach (var hit in hits.EnumerateArray())
                projects.Add(ToProject(hit));
        }

        return new CompSearchResult(projects, totalCount, offset, limit);
    }

    public static CompCategory ToCategory(JsonElement data)
    {
        var name = data.GetProperty("name").GetString() ?? "";
        var icon = data.TryGetProperty("icon", out var ic) ? ic.GetString() : null;

        Uri? iconUrl = null;
        if (!string.IsNullOrEmpty(icon) && Uri.TryCreate(icon, UriKind.Absolute, out var uri))
            iconUrl = uri;

        string? projectType = null;
        if (data.TryGetProperty("project_type", out var pt))
            projectType = pt.GetString();

        return new CompCategory(name, name, name.ToLowerInvariant(), iconUrl, null, projectType);
    }

    public static CompGameVersion ToGameVersion(JsonElement data)
    {
        var version = data.GetProperty("version").GetString() ?? "";
        var versionType = data.TryGetProperty("version_type", out var vt)
            ? vt.GetString()
            : null;

        DateTime? dateModified = null;
        if (data.TryGetProperty("updated", out var up) && up.ValueKind == JsonValueKind.String)
        {
            var upStr = up.GetString();
            if (!string.IsNullOrEmpty(upStr) && DateTime.TryParse(upStr, out var dt))
                dateModified = dt;
        }

        return new CompGameVersion(version, version, versionType, dateModified);
    }

    public static CompLoader ToLoader(JsonElement data)
    {
        var name = data.GetProperty("name").GetString() ?? "";
        var displayName = data.TryGetProperty("display_name", out var dn)
            ? dn.GetString()
            : null;

        var loaderType = _MapLoaderString(data.TryGetProperty("name", out var nm)
            ? nm.GetString() ?? ""
            : "");

        var isLatest = data.TryGetProperty("latest", out var lt) && lt.GetBoolean();

        return new CompLoader(name, displayName, loaderType, isLatest);
    }

    private static CompProjectType _MapProjectType(string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "mod" => CompProjectType.Mod,
            "modpack" => CompProjectType.Modpack,
            "resourcepack" => CompProjectType.ResourcePack,
            "shader" => CompProjectType.Shader,
            "datapack" => CompProjectType.DataPack,
            _ => CompProjectType.Unknown
        };
    }

    private static CompProjectStatus _MapStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "approved" => CompProjectStatus.Approved,
            "draft" => CompProjectStatus.Draft,
            "rejected" => CompProjectStatus.Rejected,
            "archived" => CompProjectStatus.Archived,
            "unlisted" => CompProjectStatus.Unlisted,
            "processing" => CompProjectStatus.Processing,
            "withheld" => CompProjectStatus.Withheld,
            "scheduled" => CompProjectStatus.Scheduled,
            _ => CompProjectStatus.Unknown
        };
    }

    private static ModLoaderType _MapLoaderString(string loader)
    {
        return loader.ToLowerInvariant() switch
        {
            "forge" => ModLoaderType.Forge,
            "fabric" => ModLoaderType.Fabric,
            "quilt" => ModLoaderType.Quilt,
            "neoforge" => ModLoaderType.NeoForge,
            "rift" => ModLoaderType.Rift,
            "liteloader" => ModLoaderType.LiteLoader,
            "cauldron" => ModLoaderType.Cauldron,
            "data" => ModLoaderType.Data,
            _ => ModLoaderType.Any
        };
    }

    private static DateTime _ParseDateTime(JsonElement data, string property)
    {
        if (data.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            var str = prop.GetString();
            if (!string.IsNullOrEmpty(str) && DateTime.TryParse(str, out var dt))
                return dt;
        }
        return DateTime.MinValue;
    }
}

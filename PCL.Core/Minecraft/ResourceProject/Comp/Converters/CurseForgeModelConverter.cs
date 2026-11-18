using System;
using System.Collections.Generic;
using System.Text.Json;
using PCL.Core.Minecraft.ResourceProject.Comp.Models;
using PCL.Core.Minecraft.ResourceProject.Comp.Models.Enums;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Converters;

public static class CurseForgeModelConverter
{
    public static CompProject ToProject(JsonElement data)
    {
        var id = data.GetProperty("id").GetInt32().ToString();
        var name = data.GetProperty("name").GetString() ?? "";
        var slug = data.GetProperty("slug").GetString() ?? "";
        var summary = data.GetProperty("summary").GetString() ?? "";

        Uri? iconUrl = null;
        if (data.TryGetProperty("logo", out var logo) && logo.ValueKind == JsonValueKind.Object)
        {
            var urlStr = logo.GetProperty("url").GetString();
            if (!string.IsNullOrEmpty(urlStr) && Uri.TryCreate(urlStr, UriKind.Absolute, out var uri))
                iconUrl = uri;
        }

        var categories = new List<string>();
        if (data.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in cats.EnumerateArray())
            {
                var cSlug = c.GetProperty("slug").GetString();
                if (!string.IsNullOrEmpty(cSlug))
                    categories.Add(cSlug);
            }
        }

        var gameVersions = new List<string>();
        if (data.TryGetProperty("latestFiles", out var files) && files.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in files.EnumerateArray())
            {
                if (f.TryGetProperty("gameVersions", out var gvs) && gvs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var gv in gvs.EnumerateArray())
                    {
                        var gvStr = gv.GetString();
                        if (!string.IsNullOrEmpty(gvStr) && !gameVersions.Contains(gvStr))
                            gameVersions.Add(gvStr);
                    }
                }
            }
        }

        var downloadCount = data.TryGetProperty("downloadCount", out var dc) ? dc.GetInt32() : 0;
        var dateCreated = _ParseDateTime(data, "dateCreated");
        var dateModified = _ParseDateTime(data, "dateModified");

        string? author = null;
        if (data.TryGetProperty("authors", out var authors) && authors.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in authors.EnumerateArray())
            {
                author = a.GetProperty("name").GetString();
                if (!string.IsNullOrEmpty(author)) break;
            }
        }

        Uri? siteUrl = null;
        Uri? sourceUrl = null;
        if (data.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Object)
        {
            var ws = links.GetProperty("websiteUrl").GetString();
            if (!string.IsNullOrEmpty(ws) && Uri.TryCreate(ws, UriKind.Absolute, out var sui))
                siteUrl = sui;

            var src = links.GetProperty("sourceUrl").GetString();
            if (!string.IsNullOrEmpty(src) && Uri.TryCreate(src, UriKind.Absolute, out var sui2))
                sourceUrl = sui2;
        }

        var status = _MapStatus(data.TryGetProperty("status", out var st) ? st.GetInt32() : 0);

        return new CompProject(
            id, "CurseForge", slug, name, summary, null, iconUrl,
            categories, gameVersions, downloadCount, null,
            CompProjectType.Mod, null, dateCreated, dateModified,
            author, siteUrl, null, sourceUrl, status);
    }

    public static CompFile ToFile(JsonElement data, int modId)
    {
        var fileId = data.GetProperty("id").GetInt32().ToString();
        var displayName = data.GetProperty("displayName").GetString() ?? "";
        var fileName = data.GetProperty("fileName").GetString() ?? "";

        Uri? downloadUrl = null;
        if (data.TryGetProperty("downloadUrl", out var du) && du.ValueKind == JsonValueKind.String)
        {
            var duStr = du.GetString();
            if (!string.IsNullOrEmpty(duStr) && Uri.TryCreate(duStr, UriKind.Absolute, out var uri))
                downloadUrl = uri;
        }

        var fileLength = data.TryGetProperty("fileLength", out var fl) ? fl.GetInt64() : 0L;

        var releaseType = data.TryGetProperty("releaseType", out var rt) ? rt.GetInt32() : 0;
        var releaseTypeStr = releaseType switch
        {
            1 => "release",
            2 => "beta",
            3 => "alpha",
            _ => "release"
        };

        var gameVersions = new List<string>();
        if (data.TryGetProperty("gameVersions", out var gvs) && gvs.ValueKind == JsonValueKind.Array)
        {
            foreach (var gv in gvs.EnumerateArray())
            {
                var gvStr = gv.GetString();
                if (!string.IsNullOrEmpty(gvStr)) gameVersions.Add(gvStr);
            }
        }

        var loaders = new List<ModLoaderType>();
        if (data.TryGetProperty("sortableGameVersions", out var sgvs) && sgvs.ValueKind == JsonValueKind.Array)
        {
            foreach (var sgv in sgvs.EnumerateArray())
            {
                if (sgv.TryGetProperty("modLoader", out var ml) && ml.ValueKind == JsonValueKind.Number)
                {
                    loaders.Add(_MapLoaderType(ml.GetInt32()));
                }
            }
        }

        var hashes = new Dictionary<HashAlgorithm, string>();
        if (data.TryGetProperty("hashes", out var hashesEl) && hashesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var h in hashesEl.EnumerateArray())
            {
                var algo = h.GetProperty("algo").GetInt32();
                var value = h.GetProperty("value").GetString() ?? "";
                var key = algo switch
                {
                    1 => HashAlgorithm.Sha1,
                    2 => HashAlgorithm.Md5,
                    _ => (HashAlgorithm?)null
                };
                if (key.HasValue && !string.IsNullOrEmpty(value))
                    hashes[key.Value] = value;
            }
        }

        var dependencies = new List<CompFileDependency>();
        if (data.TryGetProperty("dependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
        {
            foreach (var d in deps.EnumerateArray())
            {
                var depFileId = d.GetProperty("modId").GetInt32().ToString();
                var relationType = d.GetProperty("relationType").GetInt32() switch
                {
                    1 => "required",
                    2 => "optional",
                    3 => "incompatible",
                    _ => "unknown"
                };
                dependencies.Add(new CompFileDependency(depFileId, depFileId, relationType));
            }
        }

        var datePublished = _ParseDateTime(data, "fileDate");
        var downloadCount = data.TryGetProperty("downloadCount", out var dn) ? dn.GetInt32() : 0;
        var isAvailable = data.TryGetProperty("isAvailable", out var ia) && ia.GetBoolean();

        return new CompFile(
            fileId, modId.ToString(), displayName, fileName, downloadUrl,
            fileLength, releaseTypeStr, gameVersions, loaders, hashes,
            dependencies, null, datePublished, downloadCount, isAvailable);
    }

    public static CompSearchResult ToSearchResult(JsonElement root, int offset, int limit)
    {
        var data = root.GetProperty("data");
        var pagination = root.GetProperty("pagination");
        var totalCount = pagination.GetProperty("totalCount").GetInt32();

        var hits = new List<CompProject>();
        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
                hits.Add(ToProject(item));
        }

        return new CompSearchResult(hits, totalCount, offset, limit);
    }

    public static CompCategory ToCategory(JsonElement data)
    {
        var id = data.GetProperty("id").GetInt32().ToString();
        var name = data.GetProperty("name").GetString() ?? "";
        var slug = data.GetProperty("slug").GetString() ?? "";

        Uri? iconUrl = null;
        if (data.TryGetProperty("iconUrl", out var ic) && ic.ValueKind == JsonValueKind.String)
        {
            var icStr = ic.GetString();
            if (!string.IsNullOrEmpty(icStr) && Uri.TryCreate(icStr, UriKind.Absolute, out var uri))
                iconUrl = uri;
        }

        string? parentId = null;
        if (data.TryGetProperty("parentCategoryId", out var pid) && pid.ValueKind == JsonValueKind.Number)
        {
            var pidVal = pid.GetInt32();
            if (pidVal > 0) parentId = pidVal.ToString();
        }

        string? classId = null;
        if (data.TryGetProperty("classId", out var cid) && cid.ValueKind == JsonValueKind.Number)
        {
            classId = cid.GetInt32().ToString();
        }

        return new CompCategory(id, name, slug, iconUrl, parentId, classId);
    }

    public static CompGameVersion ToGameVersion(JsonElement data)
    {
        var id = data.TryGetProperty("id", out var idProp) ? idProp.GetInt32().ToString() : "";
        var version = data.GetProperty("versionString").GetString() ?? "";

        string? versionType = null;
        if (data.TryGetProperty("type", out var vt) && vt.ValueKind == JsonValueKind.Number)
        {
            versionType = vt.GetInt32() switch
            {
                1 => "release",
                2 => "snapshot",
                3 => "alpha",
                4 => "beta",
                _ => null
            };
        }

        DateTime? dateModified = _ParseDateTimeOrNull(data, "modified");

        return new CompGameVersion(id, version, versionType, dateModified);
    }

    public static CompLoader ToLoader(JsonElement data)
    {
        var name = data.GetProperty("name").GetString() ?? "";
        var displayName = data.TryGetProperty("displayName", out var dn)
            ? dn.GetString()
            : null;

        var loaderType = _MapLoaderType(data.TryGetProperty("id", out var id) ? id.GetInt32() : 0);

        var isLatest = data.TryGetProperty("latest", out var lt) && lt.GetBoolean();

        return new CompLoader(name, displayName, loaderType, isLatest);
    }

    private static CompProjectStatus _MapStatus(int cfStatus)
    {
        return cfStatus switch
        {
            1 => CompProjectStatus.Draft,
            2 => CompProjectStatus.Processing,
            3 => CompProjectStatus.Processing,
            4 => CompProjectStatus.Rejected,
            5 => CompProjectStatus.Approved,
            6 => CompProjectStatus.Approved,
            _ => CompProjectStatus.Unknown
        };
    }

    private static ModLoaderType _MapLoaderType(int cfLoader)
    {
        return cfLoader switch
        {
            0 => ModLoaderType.Any,
            1 => ModLoaderType.Forge,
            2 => ModLoaderType.Cauldron,
            3 => ModLoaderType.LiteLoader,
            4 => ModLoaderType.Fabric,
            5 => ModLoaderType.Quilt,
            6 => ModLoaderType.NeoForge,
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

    private static DateTime? _ParseDateTimeOrNull(JsonElement data, string property)
    {
        if (data.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            var str = prop.GetString();
            if (!string.IsNullOrEmpty(str) && DateTime.TryParse(str, out var dt))
                return dt;
        }
        return null;
    }
}

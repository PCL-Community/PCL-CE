using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using PCL.Core.Utils;

namespace PCL;

public static partial class LocalComp
{
    public partial class LocalCompFile
    {
        internal void LookupMetadata(ZipArchive jar, string? loaderType = null)
        {
            _TryParseMcmodInfo(jar);
            _TryParseFabricModJson(jar, loaderType);
            if (!_TryParseQuiltModJson(jar, loaderType) && !_TryParseModsToml(jar, loaderType))
            {
                _TryParseFmlCacheAnnotation(jar);
                _TryExtractResourcePackIcon(jar);
            }

            _NormalizeVersionFromManifest(jar);
        }

        private void _TryParseMcmodInfo(ZipArchive jar)
        {
            try
            {
                var infoObject = _ReadMcmodInfoObject(jar);
                if (infoObject is null)
                    return;
                _ApplyMcmodInfoMetadata(infoObject, jar);
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    $"读取 mcmod.info 时出现未知错误（{path}）",
                    Common.LogLevel.Developer);
            }
        }

        private static JsonObject? _ReadMcmodInfoObject(ZipArchive jar)
        {
            var hasModernMetadata =
                jar.GetEntry("fabric.mod.json") is not null
                || jar.GetEntry("quilt.mod.json") is not null
                || jar.GetEntry("META-INF/mods.toml") is not null
                || jar.GetEntry("META-INF/neoforge.mods.toml") is not null;
            if (hasModernMetadata)
                return null;

            var infoEntry = jar.GetEntry("mcmod.info");
            if (infoEntry is null)
                return null;

            var infoString = Common.ReadFile(infoEntry.Open());
            if (infoString.Length < 15)
                return null;

            var jsonObject = Common.GetJson(infoString);
            return jsonObject.GetValueKind() == JsonValueKind.Array
                ? jsonObject[0] as JsonObject
                : jsonObject["modList"]?[0] as JsonObject;
        }

        private void _ApplyMcmodInfoMetadata(JsonObject infoObject, ZipArchive jar)
        {
            Name = infoObject["name"]?.ToString() ?? "";
            Description = infoObject["description"]?.ToString();
            Version = infoObject["version"]?.ToString();
            Url = infoObject["url"]?.ToString();
            ModId = infoObject["modid"]?.ToString();

            if (infoObject["authorList"] is JsonArray authorJson)
            {
                var author = authorJson
                    .Select(token => token?.ToString())
                    .OfType<string>()
                    .ToList();

                if (author.Count != 0)
                    Authors = author.Join(", ");
            }

            var logoFile = infoObject["logoFile"]?.ToString();
            if (logoFile is not null)
            {
                var logoItem = jar.GetEntry(logoFile);
                if (logoItem is not null)
                    _SaveLogoFromEntry(logoItem);
            }

            if (infoObject["requiredMods"] is JsonArray requiredMods)
                _AddDependencyTokens(requiredMods);
            if (infoObject["dependencies"] is JsonArray dependencies)
                _AddDependencyTokens(dependencies);
        }

        private void _AddDependencyTokens(JsonArray tokens)
        {
            foreach (var item in tokens)
            {
                var rawToken = item?.ToString();
                if (!string.IsNullOrEmpty(rawToken))
                    _AddDependencyToken(rawToken);
            }
        }

        private void _AddDependencyToken(string token)
        {
            var id = token[(token.IndexOfF(":") + 1)..];
            if (id.Contains('@'))
            {
                var parts = id.Split("@");
                AddDependency(parts[0], parts[1]);
            }
            else
            {
                AddDependency(id);
            }
        }

        private void _AddFabricDependencies(JsonObject? dependencies, bool optional)
        {
            if (dependencies is null)
                return;
            foreach (var dep in dependencies)
                AddDependency(dep.Key, _ParseFabricDependencyVersion(dep.Value), optional);
        }

        private void _AddFabricConflicts(JsonObject? dependencies, bool hard)
        {
            if (dependencies is null)
                return;
            foreach (var dep in dependencies)
                _AddConflict(dep.Key, _ParseFabricDependencyVersion(dep.Value), hard);
        }

        private static string? _ParseFabricDependencyVersion(JsonNode? value)
        {
            var version = value is JsonArray arr
                ? string.Join(
                    " || ",
                    arr.Select(x => x?.ToString()).Where(x => !string.IsNullOrEmpty(x)))
                : value?.ToString();
            return string.IsNullOrEmpty(version) ? null : version;
        }

        private void _TryParseFabricModJson(ZipArchive jar, string? loaderType)
        {
            try
            {
                var fabricObject = _ReadFabricModObject(jar, loaderType);
                if (fabricObject is null)
                    return;
                _ApplyFabricModMetadata(fabricObject, jar);
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"读取 fabric.mod.json 时出错（{path}）", Common.LogLevel.Developer);
            }
        }

        private static JsonObject? _ReadFabricModObject(ZipArchive jar, string? loaderType)
        {
            var fabricEntry = string.Equals(
                loaderType,
                "Fabric",
                StringComparison.OrdinalIgnoreCase)
                ? jar.GetEntry("fabric.mod.json")
                : null;
            if (fabricEntry is null)
                return null;

            var fabricText = Common.ReadFile(fabricEntry.Open(), Encoding.UTF8);
            return !fabricText.Contains("schemaVersion")
                ? null
                : Common.GetJson(fabricText) as JsonObject;
        }

        private void _ApplyFabricModMetadata(JsonObject fabricObject, ZipArchive jar)
        {
            if (fabricObject.ContainsKey("name"))
                Name = fabricObject["name"]?.ToString() ?? "";
            if (fabricObject.ContainsKey("version"))
                Version = fabricObject["version"]?.ToString();
            if (fabricObject.ContainsKey("description"))
                Description = fabricObject["description"]?.ToString();
            if (fabricObject.ContainsKey("id"))
                ModId = fabricObject["id"]?.ToString();
            if (fabricObject["contact"]?["homepage"] is not null)
                Url = fabricObject["contact"]?["homepage"]?.ToString();

            if (fabricObject["authors"] is JsonArray authorJson)
            {
                var authorList = authorJson
                    .Select(t => t?.ToString())
                    .Where(t => t is not null)
                    .ToList();
                if (authorList.Count != 0)
                    Authors = string.Join(", ", authorList);
            }

            var logoFile = fabricObject["icon"]?.ToString();
            if (logoFile != null)
            {
                var logoItem = jar.GetEntry(logoFile);
                if (logoItem is not null)
                    _SaveLogoFromEntry(logoItem);
            }

            if (fabricObject["depends"] is JsonObject depends)
                _AddFabricDependencies(depends, false);
            if (fabricObject["recommends"] is JsonObject recommends)
                _AddFabricDependencies(recommends, true);
            if (fabricObject["suggests"] is JsonObject suggests)
                _AddFabricDependencies(suggests, true);
            if (fabricObject["breaks"] is JsonObject breaks)
                _AddFabricConflicts(breaks, true);
            if (fabricObject["conflicts"] is JsonObject conflicts)
                _AddFabricConflicts(conflicts, false);

            if (fabricObject["provides"] is not JsonArray provides)
                return;

            foreach (var pv in provides)
            {
                var id = pv?.ToString();
                if (!string.IsNullOrEmpty(id))
                    _providedIds.Add(id.ToLower());
            }
        }

        private bool _TryParseQuiltModJson(ZipArchive jar, string? loaderType)
        {
            try
            {
                var quiltObject = _ReadQuiltLoaderObject(jar, loaderType);
                if (quiltObject is null)
                    return false;

                _ApplyQuiltMetadata(quiltObject, jar);
                return true;
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    $"读取 quilt.mod.json 时出现未知错误（{path}）",
                    Common.LogLevel.Developer);
                return false;
            }
        }

        private static JsonObject? _ReadQuiltLoaderObject(ZipArchive jar, string? loaderType)
        {
            var quiltEntry = string.Equals(loaderType, "Quilt", StringComparison.OrdinalIgnoreCase)
                ? jar.GetEntry("quilt.mod.json")
                : null;
            if (quiltEntry is null)
                return null;

            var quiltText = Common.ReadFile(quiltEntry.Open(), Encoding.UTF8);
            return !quiltText.Contains("schema_version")
                ? null
                : (Common.GetJson(quiltText) as JsonObject)?["quilt_loader"] as JsonObject;
        }

        private void _ApplyQuiltMetadata(JsonObject quiltObject, ZipArchive jar)
        {
            if (quiltObject.ContainsKey("id"))
                ModId = quiltObject["id"]?.ToString();
            if (quiltObject.ContainsKey("version"))
                Version = quiltObject["version"]?.ToString();
            if (quiltObject["metadata"] is JsonObject quiltMetadata)
            {
                if (quiltMetadata.ContainsKey("name"))
                    Name = quiltMetadata["name"]?.ToString() ?? "";
                if (quiltMetadata.ContainsKey("description"))
                    Description = quiltMetadata["description"]?.ToString();
                if (quiltMetadata.ContainsKey("contact"))
                    Url = quiltMetadata["contact"]?["homepage"]?.ToString();
            }

            if (!quiltObject.ContainsKey("icon"))
                return;

            var logoFile = quiltObject["icon"]?.ToString();
            if (logoFile is null)
                return;

            var logoItem = jar.GetEntry(logoFile);
            if (logoItem is not null)
                _SaveLogoFromEntry(logoItem);
        }

        private bool _TryParseModsToml(ZipArchive jar, string? loaderType)
        {
            try
            {
                var tomlText = _ReadModsTomlText(jar, loaderType);
                if (tomlText is null)
                    return false;

                var lines = _ReadTomlLines(tomlText);
                var tomlData = _ParseTomlData(lines);

                var modSection = tomlData.FirstOrDefault(section => section.Name == "mods");
                if (modSection is null
                    || !modSection.Values.TryGetValue("modId", out var value))
                    return false;

                ModId = value?.ToString();
                if (_modId is null)
                    return false;

                _ApplyModsTomlMetadata(modSection, tomlData, lines);
                return true;
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    $"读取 mods.toml 时出现未知错误（{path}）",
                    Common.LogLevel.Developer);
            }

            return false;
        }

        private static string? _ReadModsTomlText(ZipArchive jar, string? loaderType)
        {
            ZipArchiveEntry? tomlEntry = null;
            if (string.Equals(loaderType, "NeoForge", StringComparison.OrdinalIgnoreCase))
                tomlEntry =
                    jar.GetEntry("META-INF/neoforge.mods.toml")
                    ?? jar.GetEntry("META-INF/mods.toml");
            else if (string.Equals(loaderType, "Forge", StringComparison.OrdinalIgnoreCase))
                tomlEntry =
                    jar.GetEntry("META-INF/mods.toml")
                    ?? jar.GetEntry("META-INF/neoforge.mods.toml");

            if (tomlEntry is null)
                return null;

            string tomlText;
            using (var reader = new StreamReader(tomlEntry.Open()))
            {
                tomlText = reader.ReadToEnd();
            }

            return tomlText.Length < 15
                ? null
                : tomlText;
        }

        private static List<string> _ReadTomlLines(string tomlText)
        {
            var lines = new List<string>();
            var rawLines = tomlText
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Split('\n');

            foreach (var rawLine in rawLines)
            {
                var line = rawLine;
                if (line.StartsWithF("#"))
                    continue;

                if (line.Contains('#'))
                    line = line[..line.IndexOfF("#")];

                line = line.Trim(' ', '\t', '　');
                if (!string.IsNullOrEmpty(line))
                    lines.Add(line);
            }

            return lines;
        }

        private static List<TomlSection> _ParseTomlData(List<string> lines)
        {
            var tomlData = new List<TomlSection>
            {
                new("", new Dictionary<string, object>())
            };

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.StartsWithF("[") && line.EndsWithF("]"))
                {
                    var header = line.Trim('[', ']');
                    tomlData.Add(new TomlSection(header, new Dictionary<string, object>()));
                    continue;
                }

                if (!line.Contains('='))
                    continue;

                var key = line[..line.IndexOfF("=")].TrimEnd(' ', '\t', '　');
                var rawValue = line[(line.IndexOfF("=") + 1)..].TrimStart(' ', '\t', '　');
                var value = _ParseTomlValue(lines, ref i, rawValue);

                var lastPair = tomlData[^1];
                lastPair.Values[key] = value;
            }

            return tomlData;
        }

        private static object _ParseTomlValue(List<string> lines, ref int index, string rawValue)
        {
            if (rawValue.StartsWithF("\"") && rawValue.EndsWithF("\""))
                return rawValue.Trim('\"');

            if (rawValue.StartsWithF("'''"))
                return _ParseMultilineTomlString(lines, ref index, rawValue);

            if (rawValue.Equals("true", StringComparison.CurrentCultureIgnoreCase)
                || rawValue.Equals("false", StringComparison.CurrentCultureIgnoreCase))
                return rawValue.Equals("true", StringComparison.CurrentCultureIgnoreCase);

            if (double.TryParse(rawValue, out var num))
                return num;

            return rawValue;
        }

        private static string _ParseMultilineTomlString(
            List<string> lines,
            ref int index,
            string rawValue)
        {
            var valueLines = new List<string>
            {
                rawValue.Replace("'''", "")
            };
            if (!rawValue.EndsWithF("'''") || rawValue.Length == 3)
                while (index < lines.Count - 1)
                {
                    index++;
                    var valueLine = lines[index];
                    if (valueLine.EndsWithF("'''"))
                    {
                        valueLines.Add(valueLine.Replace("'''", ""));
                        break;
                    }

                    valueLines.Add(valueLine);
                }

            return string
                .Join("\n", valueLines)
                .Trim('\n')
                .Replace("\n", "\r\n");
        }

        private void _ApplyModsTomlMetadata(
            TomlSection modSection,
            List<TomlSection> tomlData,
            List<string> lines)
        {
            _ApplyTomlDisplayMetadata(modSection.Values, tomlData);

            var ownModIdL = ModId?.ToLower() ?? "";
            var jarMods = _CollectJarMods(tomlData);
            var jarModIds = jarMods.Keys.ToHashSet();

            _RegisterSiblingMods(jarModIds, ownModIdL, jarMods);
            _AddSectionDependencies(tomlData, jarModIds, ownModIdL);
            _AddInlineDependencies(string.Join("\n", lines), jarModIds, ownModIdL);
        }

        private void _ApplyTomlDisplayMetadata(
            Dictionary<string, object> modEntry,
            List<TomlSection> tomlData)
        {
            if (modEntry.TryGetValue("displayName", out var displayName))
                Name = displayName?.ToString() ?? "";
            if (modEntry.TryGetValue("description", out var description))
                Description = description?.ToString();
            if (modEntry.TryGetValue("version", out var version))
                Version = version?.ToString();

            if (tomlData[0].Values.TryGetValue("displayURL", out var value))
                Url = value?.ToString();
            if (tomlData[0].Values.TryGetValue("authors", out var value1))
                Authors = value1?.ToString();
        }

        private static Dictionary<string, string?> _CollectJarMods(List<TomlSection> tomlData)
        {
            return tomlData
                .Where(section => section.Name == "mods"
                    && section.Values.ContainsKey("modId"))
                .Select(section => (
                    Id: section.Values["modId"]?.ToString()?.ToLower() ?? "",
                    Version: section.Values.TryGetValue("version", out var version)
                        ? version?.ToString()
                        : null))
                .GroupBy(
                    mod => mod.Id,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().Version,
                    StringComparer.OrdinalIgnoreCase);
        }

        private void _RegisterSiblingMods(
            HashSet<string> jarModIds,
            string ownModIdL,
            Dictionary<string, string?> jarMods)
        {
            foreach (var siblingId in jarModIds
                         .Where(siblingId => siblingId != ownModIdL))
            {
                _providedIds.Add(siblingId);
                var siblingVersion = jarMods[siblingId];
                if (!string.IsNullOrWhiteSpace(siblingVersion))
                    _providedVersions[siblingId] = siblingVersion;
            }
        }

        private void _AddSectionDependencies(
            List<TomlSection> tomlData,
            HashSet<string> jarModIds,
            string ownModIdL)
        {
            foreach (var (name, depEntry) in tomlData)
            {
                var headerL = name.ToLower();
                if (headerL != "dependencies"
                    && !headerL.StartsWithF("dependencies."))
                    continue;

                if (!depEntry.TryGetValue("modId", out var value))
                    continue;

                var depModId = value?.ToString();
                if (depModId is null)
                    continue;

                var declarerId = headerL.StartsWithF("dependencies.")
                    ? headerL["dependencies.".Length..].Trim('"')
                    : ownModIdL;
                if (!jarModIds.Contains(declarerId))
                    continue;

                var type = depEntry.TryGetValue("type", out var typeValue)
                    ? typeValue?.ToString()?.ToLower()
                    : null;
                var serverOnly =
                    depEntry.ContainsKey("side")
                    && depEntry["side"]?.ToString()?.ToLower() == "server";
                var range = depEntry.TryGetValue("versionRange", out var rangeValue)
                    ? rangeValue?.ToString()
                    : null;
                var sameJarTarget = jarModIds.Contains(depModId.ToLower());
                if (sameJarTarget && type is not ("incompatible" or "discouraged"))
                    continue;

                if (type is "incompatible" or "discouraged")
                {
                    if (!serverOnly)
                        _AddConflict(depModId, range, type == "incompatible", declarerId);
                }
                else if (!serverOnly)
                {
                    var optional =
                        type == "optional"
                        || depEntry["mandatory"]?.ToString()?.ToLower() == "false";
                    AddDependency(depModId, range, optional);
                }
            }
        }

        private void _AddInlineDependencies(
            string tomlText,
            HashSet<string> jarModIds,
            string ownModIdL)
        {
            foreach (var inline in _ExtractInlineDepArrays(tomlText))
            {
                var declarerId = inline.DeclarerId ?? ownModIdL;
                if (!jarModIds.Contains(declarerId))
                    continue;

                foreach (Match obj in InlineDependencyRegex().Matches(inline.Body))
                    _ApplyInlineDependency(obj.Groups[1].Value, declarerId, jarModIds);
            }
        }

        private void _ApplyInlineDependency(
            string body,
            string declarerId,
            HashSet<string> jarModIds)
        {
            var idMatch = InlineDependencyIdRegex().Match(body);
            if (!idMatch.Success)
                return;
            if (InlineDependencyServerSideRegex().IsMatch(body))
                return;

            var typeMatch = InlineDependencyTypeRegex().Match(body);
            var type = typeMatch.Success ? typeMatch.Groups[1].Value.ToLower() : null;
            var optional = InlineDependencyMandatoryRegex().IsMatch(body) || type == "optional";
            var versionRangeMatch = InlineDependencyVersionRangeRegex().Match(body);
            var range = versionRangeMatch.Success ? versionRangeMatch.Groups[1].Value : null;
            var depId = idMatch.Groups[1].Value;
            var sameJarTarget = jarModIds.Contains(depId.ToLower());

            if (sameJarTarget && type is not ("incompatible" or "discouraged"))
                return;
            if (type is "incompatible" or "discouraged")
                _AddConflict(depId, range, type == "incompatible", declarerId);
            else
                AddDependency(depId, range, optional);
        }

        [GeneratedRegex(@"\{([^}]*)\}")]
        private static partial Regex InlineDependencyRegex();

        [GeneratedRegex("modId\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
        private static partial Regex InlineDependencyIdRegex();

        [GeneratedRegex("side\\s*=\\s*\"server\"", RegexOptions.IgnoreCase)]
        private static partial Regex InlineDependencyServerSideRegex();

        [GeneratedRegex(
            "type\\s*=\\s*\"(incompatible|discouraged|optional|required)\"",
            RegexOptions.IgnoreCase
        )]
        private static partial Regex InlineDependencyTypeRegex();

        [GeneratedRegex(@"mandatory\s*=\s*""?false", RegexOptions.IgnoreCase)]
        private static partial Regex InlineDependencyMandatoryRegex();

        [GeneratedRegex("versionRange\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
        private static partial Regex InlineDependencyVersionRangeRegex();

        private void _TryParseFmlCacheAnnotation(ZipArchive jar)
        {
            try
            {
                var fmlObject = _FindFmlModAnnotation(jar);
                if (fmlObject is null)
                    return;
                _ApplyFmlAnnotation(fmlObject);
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"读取 fml_cache_annotation.json 时出现未知错误（{path}）");
            }
        }

        private static JsonObject? _FindFmlModAnnotation(ZipArchive jar)
        {
            var fmlEntry = jar.GetEntry("META-INF/fml_cache_annotation.json");
            if (fmlEntry is null)
                return null;

            var fmlText = Common.ReadFile(fmlEntry.Open(), Encoding.UTF8);
            if (!fmlText.Contains("Lnet/minecraftforge/fml/common/Mod;"))
                return null;

            if (Common.GetJson(fmlText) is not JsonObject fmlJson)
                return null;

            foreach (var modFilePair in fmlJson)
            {
                if (modFilePair.Value?["annotations"] is not JsonArray modFileAnnos)
                    continue;

                foreach (var modFileAnno in modFileAnnos)
                {
                    var name = modFileAnno?["name"]?.ToString();
                    if (name != "Lnet/minecraftforge/fml/common/Mod;")
                        continue;

                    if (modFileAnno?["values"] is JsonObject values)
                        return values;

                    break;
                }
            }

            return null;
        }

        private void _ApplyFmlAnnotation(JsonObject fmlObject)
        {
            if (fmlObject["useMetadata"]?["value"]?.ToString()?.ToLower() == "true")
            {
                var value = fmlObject["modid"]?["value"]?.ToString();
                if (value is null)
                    return;

                value = value.ToLower().RegexSeek(RegexPatterns.ModIdMatch);
                if (value is not null
                    && !value.Equals("name", StringComparison.InvariantCulture)
                    && value.Length > 1
                    && Common.Val(value).ToString(CultureInfo.InvariantCulture) != value
                    && !possibleModId.Contains(value))
                    possibleModId.Add(value);
                return;
            }

            if (fmlObject.ContainsKey("name"))
                Name = fmlObject["name"]?["value"]?.ToString() ?? "";
            if (fmlObject.ContainsKey("version"))
                Version = fmlObject["version"]?["value"]?.ToString();
            if (fmlObject.ContainsKey("modid"))
                ModId = fmlObject["modid"]?["value"]?.ToString();

            if (fmlObject["serverSideOnly"]?["value"]?.ToObject<bool>() == true)
                return;

            var depMinecraft = fmlObject["acceptedMinecraftVersions"]?["value"]?.ToString();
            if (!string.IsNullOrEmpty(depMinecraft))
                AddDependency("minecraft", depMinecraft);

            var deps = fmlObject["dependencies"]?["value"]?.ToString();
            if (string.IsNullOrEmpty(deps))
                return;

            foreach (var item in deps.Split(";"))
            {
                if (string.IsNullOrEmpty(item) || !item.StartsWithF("required-"))
                    continue;

                _AddDependencyToken(item);
            }
        }

        private void _TryExtractResourcePackIcon(ZipArchive jar)
        {
            try
            {
                var packPngEntry = jar.GetEntry("pack.png");
                if (packPngEntry is null)
                    return;

                try
                {
                    _SaveLogoFromEntry(packPngEntry);
                    Common.Log($"成功提取资源包图标：{path}", Common.LogLevel.Developer);
                }
                catch (Exception logoEx)
                {
                    Common.Log(
                        logoEx,
                        $"提取 pack.png 图标失败（{path}）",
                        Common.LogLevel.Developer);
                }
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    $"识别资源包图标时出现未知错误（{path}）",
                    Common.LogLevel.Developer);
            }
        }

        private void _SaveLogoFromEntry(ZipArchiveEntry logoItem)
        {
            var md5 = Common.GetStringMd5(logoItem.Length + logoItem.CompressedLength + path);
            Logo = Path.Combine(Common.pathTemp, "Cache", "Images", $"{md5}.png");
            using var entryStream = logoItem.Open();
            Common.WriteFile(Logo, entryStream);
        }

        private void _NormalizeVersionFromManifest(ZipArchive jar)
        {
            if (_version == "version")
                _NormalizeVersionFromManifestCore(jar);

            if (_version is not null
                && !(_version.Contains('.') || _version.Contains('-')))
                Version = null;
        }

        private void _NormalizeVersionFromManifestCore(ZipArchive jar)
        {
            try
            {
                var metaEntry = jar.GetEntry("META-INF/MANIFEST.MF");
                if (metaEntry is null)
                    return;

                var metaString = Common
                    .ReadFile(metaEntry.Open())
                    .Replace(" :", ":")
                    .Replace(": ", ":");
                const string versionHeader = "Implementation-Version:";
                if (!metaString.Contains(versionHeader))
                    return;

                metaString = metaString[
                    (metaString.IndexOfF(versionHeader) + versionHeader.Length)..
                ];
                metaString = metaString[..metaString.IndexOfAny(['\r', '\n'])].Trim();
                Version = metaString;
            }
            catch (Exception)
            {
                Common.Log($"获取 META-INF 中的版本信息失败（{path}）", Common.LogLevel.Developer);
                Version = null;
            }
        }

        private sealed record TomlSection(
            string Name,
            Dictionary<string, object> Values);
    }
}
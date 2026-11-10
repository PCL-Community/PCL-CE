using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

public static partial class DownloadUi
{
    private static JsonObject _ApplyLabyModJson(
        JsonObject? outputJson,
        JsonObject labyModJson,
        string labyModChannel)
    {
        labyModJson.Remove("releaseTime");
        labyModJson.Remove("time");
        outputJson ??= new JsonObject();
        outputJson.Merge(labyModJson);

        var labyModLib = (JsonObject)
            Requester.FetchJson(
                $"https://releases.r2.labymod.net/api/v1/libraries/{labyModChannel}.json",
                RequestParam.WithRetry);
        var labyModCore = (JsonObject)
            Requester.FetchJson(
                $"https://releases.r2.labymod.net/api/v1/manifest/{labyModChannel}/latest.json",
                RequestParam.WithRetry);
        var outputLibraries = _CreateLabyModLibraries(labyModLib, labyModJson);
        var labyModCommitReference = labyModCore["commitReference"]?.ToString() ?? "";
        outputLibraries.Add(
            _CreateLabyModCoreLibrary(labyModCore, labyModChannel, labyModCommitReference));

        outputJson["libraries"] = outputLibraries;
        outputJson.Add(
            "labymod_data",
            new JsonObject
            {
                ["channelType"] = labyModChannel,
                ["commitReference"] = labyModCommitReference,
                ["version"] = labyModCore["labyModVersion"]?.ToString(),
                ["versionType"] = "release"
            }
        );

        return outputJson;
    }

    private static JsonArray _CreateLabyModLibraries(JsonObject labyModLib, JsonObject labyModJson)
    {
        var outputLibraries = new JsonArray();
        var minecraftVersion = labyModJson["_minecraftVersion"];

        var isolatedLibraries = (labyModLib["isolated_libraries"]?.AsArray() ?? new JsonArray())
            .Where(library =>
                (library?["versions"] as JsonArray)?.Contains(minecraftVersion) == true)
            .ToDictionary(library => library?["name"]?.ToString() ?? "", _ => true);

        foreach (var library in (labyModJson["libraries"]?.AsArray() ?? new JsonArray()).OfType<JsonNode>())
        {
            var regexMatchResult =
                (library["name"]?.ToString() ?? "").RegexSeek(RegexPatterns.CatchLwjglInLib);
            if (regexMatchResult is null
                || !isolatedLibraries.Contains(
                    new KeyValuePair<string, bool>(regexMatchResult, true)))
                outputLibraries.Add(library.DeepClone());
        }

        foreach (var library in (labyModLib["libraries"]?.AsArray() ?? new JsonArray()).OfType<JsonNode>())
        {
            var libraryUrl = library["url"]?.ToString() ?? "";
            outputLibraries.Add(_CreateLabyModLibrary(library, libraryUrl));
        }

        return outputLibraries;
    }

    private static JsonObject _CreateLabyModLibrary(JsonNode library, string libraryUrl)
    {
        var path = libraryUrl.Substring(
            libraryUrl.LastIndexOfF("https://releases.r2.labymod.net/libraries/") + 42);
        return new JsonObject
        {
            ["name"] = library?["name"]?.ToString(),
            ["downloads"] = new JsonObject
            {
                ["artifact"] = new JsonObject
                {
                    ["path"] = path,
                    ["sha1"] = library?["sha1"]?.ToString(),
                    ["size"] = library?["size"]?.DeepClone(),
                    ["url"] = libraryUrl
                }
            }
        };
    }

    private static JsonObject _CreateLabyModCoreLibrary(
        JsonObject labyModCore,
        string labyModChannel,
        string labyModCommitReference)
    {
        return new JsonObject
        {
            ["name"] = "net.labymod:LabyMod:4",
            ["downloads"] = new JsonObject
            {
                ["artifact"] = new JsonObject
                {
                    ["path"] = "net/labymod/LabyMod/4/LabyMod-4.jar",
                    ["sha1"] = labyModCore["sha1"]?.ToString(),
                    ["size"] = labyModCore["size"]?.DeepClone(),
                    ["url"] =
                        $"https://releases.r2.labymod.net/api/v1/download/labymod4/{labyModChannel}/{labyModCommitReference}.jar"
                }
            }
        };
    }
}
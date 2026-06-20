// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PCL.Application.Minecraft.Launch.Arguments;

public static class MinecraftLaunchArgumentService
{
    private static readonly DateTimeOffset QuickPlayReleaseCutoff =
        new(new DateTime(2023, 4, 4), TimeSpan.Zero);

    public static MinecraftGameArgumentResult BuildLegacyGameArguments(MinecraftLegacyGameArgumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<string> arguments = [];
        if (request.NeedsRetroWrapper)
            arguments.Add("--tweakClass com.zero.retrowrapper.RetroTweaker");

        string minecraftArguments = request.MinecraftArguments;
        if (!minecraftArguments.Contains("--height", StringComparison.Ordinal))
            minecraftArguments += " --height ${resolution_height} --width ${resolution_width}";

        arguments.Add(minecraftArguments);
        return NormalizeOptiFineTweaker(string.Join(' ', arguments), request.HasForge, request.HasLiteLoader, request.HasOptiFine);
    }

    public static MinecraftGameArgumentResult BuildModernGameArguments(MinecraftModernGameArgumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.RuleContext);

        List<string> arguments = [];
        AddModernGameArguments(arguments, request.VersionJson, request.RuleContext);
        foreach (JsonObject inheritedVersion in request.InheritedVersionJsons)
            AddModernGameArguments(arguments, inheritedVersion, request.RuleContext);

        List<string> normalizedArguments = MergeSwitchValues(arguments);
        string result = string.Join(' ', normalizedArguments.Distinct(StringComparer.Ordinal));
        return NormalizeOptiFineTweaker(result, request.HasForge, request.HasLiteLoader, request.HasOptiFine);
    }

    public static MinecraftFinalArgumentResult BuildFinalArguments(MinecraftFinalArgumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string arguments = request.Arguments;
        if (request.JavaMajorVersion > 8)
        {
            if (!arguments.Contains("-Dstdout.encoding=", StringComparison.Ordinal))
                arguments = "-Dstdout.encoding=UTF-8 " + arguments;
            if (!arguments.Contains("-Dstderr.encoding=", StringComparison.Ordinal))
                arguments = "-Dstderr.encoding=UTF-8 " + arguments;
        }

        if (request.JavaMajorVersion >= 18 &&
            !arguments.Contains("-Dfile.encoding=", StringComparison.Ordinal))
        {
            arguments = "-Dfile.encoding=COMPAT " + arguments;
        }

        arguments = arguments.Replace(" -Dos.name=Windows 10", " -Dos.name=\"Windows 10\"", StringComparison.Ordinal);

        if (request.Fullscreen)
            arguments += " --fullscreen";

        foreach (string argument in request.ExtraArguments)
        {
            string trimmed = argument.Trim();
            if (trimmed.Length > 0)
                arguments += " " + trimmed;
        }

        if (!string.IsNullOrWhiteSpace(request.CustomGameArguments))
            arguments += " " + request.CustomGameArguments;

        Dictionary<string, string> replacements = new(request.Replacements, StringComparer.Ordinal);
        if (replacements.TryGetValue("${version_type}", out string? versionType) &&
            string.IsNullOrWhiteSpace(versionType))
        {
            arguments = arguments.Replace(" --versionType ${version_type}", string.Empty, StringComparison.Ordinal);
            replacements["${version_type}"] = "\"\"";
        }

        string finalArguments = ReplaceArguments(arguments, replacements);

        if (!string.IsNullOrWhiteSpace(request.WorldName))
            finalArguments += $" --quickPlaySingleplayer \"{request.WorldName}\"";

        bool shouldWarnOptiFineAutoJoin = false;
        if (string.IsNullOrWhiteSpace(request.WorldName) && !string.IsNullOrWhiteSpace(request.Server))
        {
            if (request.ReleaseTime > QuickPlayReleaseCutoff)
            {
                finalArguments += $" --quickPlayMultiplayer \"{request.Server}\"";
            }
            else
            {
                AddLegacyServerArguments(ref finalArguments, request.Server);
                shouldWarnOptiFineAutoJoin = request.HasOptiFine;
            }
        }

        return new MinecraftFinalArgumentResult(finalArguments.TrimEnd(), shouldWarnOptiFineAutoJoin);
    }

    public static bool IsRuleAllowed(JsonNode? ruleToken, MinecraftArgumentRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (ruleToken is null)
            return true;

        bool required = false;
        foreach (JsonNode? rule in ruleToken.AsArray())
        {
            if (rule is null)
                continue;

            bool isRightRule = IsOperatingSystemAllowed(rule["os"], context) &&
                               AreFeaturesAllowed(rule["features"], context);
            string? action = rule["action"]?.ToString();
            if (string.Equals(action, "allow", StringComparison.Ordinal))
            {
                if (isRightRule)
                    required = true;
            }
            else if (isRightRule)
            {
                required = false;
            }
        }

        return required;
    }

    private static void AddModernGameArguments(
        List<string> arguments,
        JsonObject versionJson,
        MinecraftArgumentRuleContext context)
    {
        JsonArray? gameArguments = versionJson["arguments"]?["game"]?.AsArray();
        if (gameArguments is null)
            return;

        foreach (JsonNode? node in gameArguments)
        {
            if (node is null)
                continue;

            if (node.GetValueKind() == JsonValueKind.String)
            {
                arguments.Add(node.ToString());
            }
            else if (node.GetValueKind() == JsonValueKind.Object &&
                     IsRuleAllowed(node["rules"], context))
            {
                AddArgumentValue(arguments, node["value"]);
            }
        }
    }

    private static void AddArgumentValue(List<string> arguments, JsonNode? valueNode)
    {
        if (valueNode is null)
            return;

        if (valueNode.GetValueKind() == JsonValueKind.String)
        {
            arguments.Add(valueNode.ToString());
            return;
        }

        foreach (JsonNode? value in valueNode.AsArray())
        {
            if (value is not null)
                arguments.Add(value.ToString());
        }
    }

    private static MinecraftGameArgumentResult NormalizeOptiFineTweaker(
        string arguments,
        bool hasForge,
        bool hasLiteLoader,
        bool hasOptiFine)
    {
        if (!((hasForge || hasLiteLoader) && hasOptiFine))
            return new MinecraftGameArgumentResult(arguments, OptiFineTweakerAdjustment.None);

        if (arguments.Contains("--tweakClass optifine.OptiFineForgeTweaker", StringComparison.Ordinal))
        {
            return new MinecraftGameArgumentResult(
                arguments.Replace(" --tweakClass optifine.OptiFineForgeTweaker", string.Empty, StringComparison.Ordinal)
                    .Replace("--tweakClass optifine.OptiFineForgeTweaker ", string.Empty, StringComparison.Ordinal) +
                " --tweakClass optifine.OptiFineForgeTweaker",
                OptiFineTweakerAdjustment.MovedForgeTweaker);
        }

        if (arguments.Contains("--tweakClass optifine.OptiFineTweaker", StringComparison.Ordinal))
        {
            return new MinecraftGameArgumentResult(
                arguments.Replace(" --tweakClass optifine.OptiFineTweaker", string.Empty, StringComparison.Ordinal)
                    .Replace("--tweakClass optifine.OptiFineTweaker ", string.Empty, StringComparison.Ordinal) +
                " --tweakClass optifine.OptiFineForgeTweaker",
                OptiFineTweakerAdjustment.ReplacedPlainTweaker);
        }

        return new MinecraftGameArgumentResult(arguments, OptiFineTweakerAdjustment.None);
    }

    private static List<string> MergeSwitchValues(List<string> arguments)
    {
        List<string> result = [];
        for (int i = 0; i < arguments.Count; i++)
        {
            string currentEntry = arguments[i];
            if (currentEntry.StartsWith('-'))
            {
                while (i < arguments.Count - 1)
                {
                    if (arguments[i + 1].StartsWith('-'))
                        break;

                    i++;
                    currentEntry += " " + arguments[i];
                }
            }

            result.Add(currentEntry);
        }

        return result;
    }

    private static string ReplaceArguments(string arguments, IReadOnlyDictionary<string, string> replacements)
    {
        List<string> finalArguments = [];
        foreach (string rawArgument in arguments.Split(' '))
        {
            string argument = rawArgument;
            foreach ((string key, string value) in replacements)
                argument = argument.Replace(key, value, StringComparison.Ordinal);

            if ((argument.Contains(' ', StringComparison.Ordinal) ||
                 argument.Contains(@":\", StringComparison.Ordinal)) &&
                !argument.EndsWith('"'))
            {
                argument = $"\"{argument}\"";
            }

            finalArguments.Add(argument);
        }

        return string.Join(' ', finalArguments).TrimEnd();
    }

    private static bool IsOperatingSystemAllowed(JsonNode? osNode, MinecraftArgumentRuleContext context)
    {
        if (osNode is null)
            return true;

        bool isAllowed = true;
        string? osName = osNode["name"]?.ToString();
        if (!string.IsNullOrWhiteSpace(osName) &&
            !string.Equals(osName, "unknown", StringComparison.Ordinal))
        {
            isAllowed = string.Equals(osName, ToMojangOsName(context.OperatingSystem), StringComparison.Ordinal);
            string? osVersionPattern = osNode["version"]?.ToString();
            if (isAllowed && !string.IsNullOrWhiteSpace(osVersionPattern))
                isAllowed = Regex.IsMatch(context.OperatingSystemVersion, osVersionPattern);
        }

        string? architecture = osNode["arch"]?.ToString();
        if (!string.IsNullOrWhiteSpace(architecture))
            isAllowed = isAllowed &&
                        string.Equals(architecture, "x86", StringComparison.Ordinal) == context.Is32BitArchitecture;

        return isAllowed;
    }

    private static bool AreFeaturesAllowed(JsonNode? featuresNode, MinecraftArgumentRuleContext context)
    {
        if (featuresNode is null)
            return true;

        JsonObject features = featuresNode.AsObject();
        if (features.ContainsKey("is_demo_user"))
            return false;

        return context.EnableQuickPlayFeatureArguments ||
               !features.Any(static property => property.Key.Contains("quick_play", StringComparison.Ordinal));
    }

    private static string ToMojangOsName(MinecraftArgumentOperatingSystem operatingSystem) =>
        operatingSystem switch
        {
            MinecraftArgumentOperatingSystem.Win32 => "windows",
            MinecraftArgumentOperatingSystem.Linux => "linux",
            MinecraftArgumentOperatingSystem.MacOs => "osx",
            _ => "unknown"
        };

    private static void AddLegacyServerArguments(ref string finalArguments, string server)
    {
        int portSeparator = server.IndexOf(':', StringComparison.Ordinal);
        if (portSeparator >= 0)
        {
            finalArguments += " --server " + server[..portSeparator] + " --port " + server[(portSeparator + 1)..];
        }
        else
        {
            finalArguments += " --server " + server + " --port 25565";
        }
    }
}

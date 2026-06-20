// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PCL.Application.Minecraft.Launch.Arguments;

public static class MinecraftJvmArgumentService
{
    public static MinecraftJvmArgumentResult Build(MinecraftJvmArgumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.VersionJson);
        ArgumentNullException.ThrowIfNull(request.RuleContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MainClass);
        if (request.MemoryMegabytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), request.MemoryMegabytes, "Memory must be positive.");

        return request.UseModernArguments
            ? BuildModern(request)
            : BuildLegacy(request);
    }

    private static MinecraftJvmArgumentResult BuildModern(MinecraftJvmArgumentRequest request)
    {
        List<string> arguments = [];
        AddModernJvmArguments(arguments, request.VersionJson, request.RuleContext);
        foreach (JsonObject inheritedVersionJson in request.InheritedVersionJsons)
            AddModernJvmArguments(arguments, inheritedVersionJson, request.RuleContext);

        if (!string.IsNullOrWhiteSpace(request.CustomJvmArguments))
            arguments.Insert(0, request.CustomJvmArguments);
        AddPreferredIpArguments(arguments, request.PreferredIpStack);
        AddMemoryAndSecurityArguments(arguments, request.MemoryMegabytes);

        arguments.InsertRange(0, request.PrefixArguments.Where(static value => !string.IsNullOrWhiteSpace(value)));
        arguments.AddRange(request.SuffixArguments.Where(static value => !string.IsNullOrWhiteSpace(value)));

        List<string> normalized = MergeSwitchValues(arguments)
            .Select(static value => value.Trim().Replace("McEmu= ", "McEmu=", StringComparison.Ordinal))
            .Where(static value => value.Length > 0)
            .Where(static value => !string.Equals(value, "-XX:MaxDirectMemorySize=256M", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        normalized.Add(request.MainClass);
        return new MinecraftJvmArgumentResult(string.Join(' ', normalized));
    }

    private static MinecraftJvmArgumentResult BuildLegacy(MinecraftJvmArgumentRequest request)
    {
        List<string> arguments = [];
        arguments.AddRange(request.PrefixArguments.Where(static value => !string.IsNullOrWhiteSpace(value)));

        string customJvmArguments = request.CustomJvmArguments ?? string.Empty;
        if (!customJvmArguments.Contains("-Dlog4j2.formatMsgNoLookups=true", StringComparison.Ordinal))
            customJvmArguments += " -Dlog4j2.formatMsgNoLookups=true";
        customJvmArguments = customJvmArguments.Replace(
            " -XX:MaxDirectMemorySize=256M",
            string.Empty,
            StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(customJvmArguments))
            arguments.Add(customJvmArguments.Trim());

        arguments.Add("-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump");
        arguments.Add("-Xmn" + Math.Floor(request.MemoryMegabytes * 0.15d).ToString(CultureInfo.InvariantCulture) + "m");
        arguments.Add("-Xmx" + request.MemoryMegabytes.ToString(CultureInfo.InvariantCulture) + "m");
        if (!string.IsNullOrWhiteSpace(request.NativesDirectory))
            arguments.Add("\"-Djava.library.path=" + request.NativesDirectory + "\"");
        arguments.Add("-cp ${classpath}");
        arguments.AddRange(request.SuffixArguments.Where(static value => !string.IsNullOrWhiteSpace(value)));
        arguments.Add(request.MainClass);
        return new MinecraftJvmArgumentResult(string.Join(' ', arguments));
    }

    private static void AddModernJvmArguments(
        List<string> arguments,
        JsonObject versionJson,
        MinecraftArgumentRuleContext context)
    {
        JsonArray? jvmArguments = versionJson["arguments"]?["jvm"]?.AsArray();
        if (jvmArguments is null)
            return;

        foreach (JsonNode? node in jvmArguments)
        {
            if (node is null)
                continue;
            if (node.GetValueKind() == JsonValueKind.String)
            {
                arguments.Add(node.ToString());
            }
            else if (node.GetValueKind() == JsonValueKind.Object &&
                     MinecraftLaunchArgumentService.IsRuleAllowed(node["rules"], context))
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

    private static void AddPreferredIpArguments(
        List<string> arguments,
        MinecraftJvmIpPreference preferredIpStack)
    {
        switch (preferredIpStack)
        {
            case MinecraftJvmIpPreference.PreferV4:
                arguments.Add("-Djava.net.preferIPv4Stack=true");
                arguments.Add("-Djava.net.preferIPv4Addresses=true");
                break;
            case MinecraftJvmIpPreference.PreferV6:
                arguments.Add("-Djava.net.preferIPv6Stack=true");
                arguments.Add("-Djava.net.preferIPv6Addresses=true");
                break;
        }
    }

    private static void AddMemoryAndSecurityArguments(List<string> arguments, int memoryMegabytes)
    {
        arguments.Add("-Xmn" + Math.Floor(memoryMegabytes * 0.15d).ToString(CultureInfo.InvariantCulture) + "m");
        arguments.Add("-Xmx" + memoryMegabytes.ToString(CultureInfo.InvariantCulture) + "m");
        if (!arguments.Any(static value =>
                value.Contains("-Dlog4j2.formatMsgNoLookups=true", StringComparison.Ordinal)))
        {
            arguments.Add("-Dlog4j2.formatMsgNoLookups=true");
        }
    }

    private static List<string> MergeSwitchValues(List<string> arguments)
    {
        List<string> result = [];
        for (int index = 0; index < arguments.Count; index++)
        {
            string currentEntry = arguments[index];
            if (currentEntry.StartsWith('-'))
            {
                while (index < arguments.Count - 1 && !arguments[index + 1].StartsWith('-'))
                {
                    index++;
                    currentEntry += " " + arguments[index];
                }
            }

            result.Add(currentEntry);
        }

        return result;
    }
}

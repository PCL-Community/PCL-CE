// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Launch.Arguments;

public static class MinecraftLaunchPlanService
{
    public static MinecraftLaunchPlanResult CreatePlan(MinecraftLaunchPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string jvmArguments = !string.IsNullOrWhiteSpace(request.PrebuiltJvmArguments)
            ? request.PrebuiltJvmArguments
            : request.Jvm is not null
                ? MinecraftJvmArgumentService.Build(request.Jvm).Arguments
                : throw new ArgumentException("Either Jvm or PrebuiltJvmArguments must be provided.", nameof(request));
        List<string> gameArguments = [];
        OptiFineTweakerAdjustment adjustment = OptiFineTweakerAdjustment.None;

        if (request.LegacyGame is not null)
        {
            MinecraftGameArgumentResult result =
                MinecraftLaunchArgumentService.BuildLegacyGameArguments(request.LegacyGame);
            gameArguments.Add(result.Arguments);
            adjustment = CombineAdjustment(adjustment, result.OptiFineTweakerAdjustment);
        }

        if (request.ModernGame is not null)
        {
            MinecraftGameArgumentResult result =
                MinecraftLaunchArgumentService.BuildModernGameArguments(request.ModernGame);
            gameArguments.Add(result.Arguments);
            adjustment = CombineAdjustment(adjustment, result.OptiFineTweakerAdjustment);
        }

        string combinedGameArguments = string.Join(
            ' ',
            gameArguments.Where(static value => !string.IsNullOrWhiteSpace(value)));
        string baseArguments = string.IsNullOrWhiteSpace(combinedGameArguments)
            ? jvmArguments
            : jvmArguments + " " + combinedGameArguments;

        MinecraftFinalArgumentResult finalResult = MinecraftLaunchArgumentService.BuildFinalArguments(
            new MinecraftFinalArgumentRequest
            {
                Arguments = baseArguments,
                Replacements = request.Replacements,
                JavaMajorVersion = request.JavaMajorVersion,
                Fullscreen = request.Fullscreen,
                ExtraArguments = request.ExtraArguments,
                CustomGameArguments = request.CustomGameArguments,
                WorldName = request.WorldName,
                Server = request.Server,
                ReleaseTime = request.ReleaseTime,
                HasOptiFine = request.HasOptiFine
            });

        return new MinecraftLaunchPlanResult(
            jvmArguments,
            combinedGameArguments,
            finalResult.Arguments,
            adjustment,
            finalResult.ShouldWarnOptiFineAutoJoin);
    }

    private static OptiFineTweakerAdjustment CombineAdjustment(
        OptiFineTweakerAdjustment current,
        OptiFineTweakerAdjustment candidate)
    {
        if (candidate == OptiFineTweakerAdjustment.ReplacedPlainTweaker)
            return candidate;
        return current == OptiFineTweakerAdjustment.None ? candidate : current;
    }
}

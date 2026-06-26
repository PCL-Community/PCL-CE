using System;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;

namespace PCL;

internal static class MicrosoftLoginPolicyGate
{
    private const string MissingProfilePromptGlobalKey = "__global__";
    private const string MissingProfilePromptUrl = "https://www.minecraft.net/msaprofile/mygames/editprofile";

    public static bool EnsureAccepted()
    {
        if (PCL.Online.FirstLaunchService.IsAccepted())
            return true;

        var legalText = PCL.Online.FirstLaunchService.LoadFullText();
        if (ModMain.MyMsgBoxMarkdown(legalText, Lang.Text("Main.Legal.Title"),
                Lang.Text("Main.Legal.Agree"), Lang.Text("Main.Legal.Decline"),
                isWarn: false, forceWait: true) != 1)
            return false;

        PCL.Online.FirstLaunchService.Accept();
        return true;
    }

    public static void ShowOnlineCreateProfilePromptOnce(string? accountKey)
    {
        TryShowMissingMinecraftProfilePromptOnce(
            accountKey,
            Lang.Text("Online.Login.CreateProfile.Message"),
            Lang.Text("Online.Login.CreateProfile.Title"),
            Lang.Text("Online.Login.CreateProfile.Button"));
    }

    public static void ShowLaunchCreateProfilePromptOnce(string? accountKey)
    {
        TryShowMissingMinecraftProfilePromptOnce(
            accountKey,
            Lang.Text("Minecraft.Launch.Login.Microsoft.CreateProfile.Message"),
            Lang.Text("Minecraft.Launch.Login.Failed"),
            Lang.Text("Minecraft.Launch.Login.Microsoft.CreateProfile.Button"));
    }

    private static void TryShowMissingMinecraftProfilePromptOnce(string? accountKey, string message, string title,
        string confirmButton)
    {
        var normalizedKey = NormalizeMissingProfilePromptKey(accountKey);
        if (HasShownMissingMinecraftProfilePrompt(normalizedKey))
            return;

        MarkMissingMinecraftProfilePromptShown(normalizedKey);
        if (ModMain.MyMsgBox(message, title, confirmButton, Lang.Text("Common.Action.Cancel")) == 1)
            ModBase.OpenWebsite(MissingProfilePromptUrl);
    }

    private static bool HasShownMissingMinecraftProfilePrompt(string accountKey)
    {
        var rawValue = States.Online.MissingMinecraftProfilePromptedKeys;
        if (string.IsNullOrWhiteSpace(rawValue))
            return false;

        foreach (var existingKey in rawValue.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (string.Equals(existingKey, accountKey, StringComparison.Ordinal))
                return true;

        return false;
    }

    private static void MarkMissingMinecraftProfilePromptShown(string accountKey)
    {
        States.Online.MissingMinecraftProfilePromptedKeys = string.IsNullOrWhiteSpace(States.Online.MissingMinecraftProfilePromptedKeys)
            ? accountKey
            : States.Online.MissingMinecraftProfilePromptedKeys + "\n" + accountKey;
        ConfigService.FlushAll();
    }

    private static string NormalizeMissingProfilePromptKey(string? accountKey) =>
        string.IsNullOrWhiteSpace(accountKey) ? MissingProfilePromptGlobalKey : accountKey.Trim();
}

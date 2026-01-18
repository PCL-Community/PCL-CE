using System.Linq;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Minecraft.Instance.Utils;
using PCL.Core.Minecraft.Launch.Utils;
using PCL.Core.Utils.OS;

namespace PCL.Core.Minecraft.Launch.Services;

using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

public class PreLaunchService(IMcInstance instance, JavaInfo selectedJava) {
    private static readonly SemaphoreSlim GpuAdjustmentSemaphore = new(1, 1);

    public async Task McLaunchPrerunAsync(CancellationToken cancellationToken = default) {
        var tasks = new List<Task> {
            AdjustGpuSettingsAsync(cancellationToken),
            UpdateOptionsFileAsync(cancellationToken)
        };

        try {
            await Task.WhenAll(tasks);
        } catch (Exception ex) {
            LogWrapper.Error(ex, "One or more prerun tasks failed");
            throw;
        }
    }

    // TODO: 等待实现 - Update launcher_profiles.json
    private async Task UpdateLauncherProfilesAsync(CancellationToken cancellationToken) {
        // This functionality is temporarily commented out in the original code
        // Keeping the method structure for future implementation

        /*
        try
        {
            if (McLoginLoader.Output.Type != "Microsoft")
                return;

            await McFolderLauncherProfilesJsonCreateAsync(PathMcFolder).ConfigureAwait(false);

            // Build JSON object to replace
            var replaceJsonString = $"""
                                      {
                                        "authenticationDatabase": {
                                          "00000111112222233333444445555566": {
                                            "username": "{{McLoginLoader.Output.Name.Replace("\"", "-")}}",
                                            "profiles": {
                                              "66666555554444433333222221111100": {
                                                "displayName": "{{McLoginLoader.Output.Name}}"
                                              }
                                            }
                                          }
                                        },
                                        "clientToken": "{{McLoginLoader.Output.ClientToken}}",
                                        "selectedUser": {
                                          "account": "00000111112222233333444445555566",
                                          "profile": "66666555554444433333222221111100"
                                        }
                                      }
                                      """;


            // Note: Assuming GetJson returns a JsonDocument. If using Newtonsoft.Json, replace with JObject.Parse.
            using var replaceJson = GetJson(replaceJsonString);
            using var profiles = GetJson(await ReadFileAsync(Path.Combine(PathMcFolder, "launcher_profiles.json"), cancellationToken));
            MergeJson(profiles, replaceJson); // Custom merge logic for System.Text.Json
            await WriteFileAsync(
                Path.Combine(PathMcFolder, "launcher_profiles.json"),
                profiles.RootElement.GetRawText(),
                Encoding.GetEncoding("GB18030"),
                cancellationToken
                ).ConfigureAwait(false);
            McLaunchUtils.Log("Updated launcher_profiles.json");

        } catch (Exception ex) {
            LogWrapper.Warn(ex, "Failed to update launcher_profiles.json, retrying after deletion");
            try {
                File.Delete(Path.Combine(PathMcFolder, "launcher_profiles.json"));
                await McFolderLauncherProfilesJsonCreateAsync(PathMcFolder).ConfigureAwait(false);

                // Build JSON object to replace (repeated for retry)
                var replaceJsonString = $"""
                                          {
                                            "authenticationDatabase": {
                                              "00000111112222233333444445555566": {
                                                "username": "{{McLoginLoader.Output.Name.Replace("\"", "-")}}",
                                                "profiles": {
                                                  "66666555554444433333222221111100": {
                                                    "displayName": "{{McLoginLoader.Output.Name}}"
                                                  }
                                                }
                                              }
                                            },
                                            "clientToken": "{{McLoginLoader.Output.ClientToken}}",
                                            "selectedUser": {
                                              "account": "00000111112222233333444445555566",
                                              "profile": "66666555554444433333222221111100"
                                            }
                                          }
                                          """;

                using var replaceJson = GetJson(replaceJsonString);
                using var profiles = GetJson(await ReadFileAsync(Path.Combine(PathMcFolder, "launcher_profiles.json"), cancellationToken));
                MergeJson(profiles, replaceJson);
                await WriteFileAsync(
                    Path.Combine(PathMcFolder, "launcher_profiles.json"),
                    profiles.RootElement.GetRawText(),
                    Encoding.GetEncoding("GB18030"),
                    cancellationToken
                    ).ConfigureAwait(false);
                McLaunchUtils.Log("Updated launcher_profiles.json after deletion");
            } catch (Exception exx) {
                LogWrapper.Warn(exx, "Failed to update launcher_profiles.json", LogLevel.Feedback);
            }
        }
        */

        // TODO: Implement launcher profiles update functionality
        await Task.CompletedTask;
    }

    private async Task AdjustGpuSettingsAsync(CancellationToken cancellationToken) {
        await GpuAdjustmentSemaphore.WaitAsync(cancellationToken);

        try {
            ProcessInterop.SetGpuPreference(selectedJava.JavawExePath, Config.Launch.SetGpuPreference);
        } catch (Exception ex) {
            await HandleGpuAdjustmentFailureAsync(ex, cancellationToken);
        }
    }

    private async Task HandleGpuAdjustmentFailureAsync(Exception originalException, CancellationToken cancellationToken) {
        if (ProcessInterop.IsAdmin()) {
            LogWrapper.Warn(originalException, "Failed to adjust GPU settings directly");
            return;
        }

        LogWrapper.Warn(originalException, "Failed to adjust GPU settings directly, restarting PCL with admin privileges to retry");

        try {
            var process = ProcessInterop.StartAsAdmin($"--gpu \"{selectedJava.JavawExePath}\"");
            if (process == null) {
                throw new InvalidOperationException("Failed to start admin process");
            }

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode.Equals(ProcessExitCode.TaskDone)) {
                McLaunchUtils.Log("以管理员权限重启 PCL 并调整显卡设置成功");
            } else {
                throw new InvalidOperationException($"GPU adjustment failed with exit code: {process.ExitCode}");
            }
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "Failed to adjust GPU settings, Minecraft may use the default GPU");
        }
    }

    private async Task UpdateOptionsFileAsync(CancellationToken cancellationToken) {
        var setupFileAddress = await GetOptionsFilePathAsync();

        if (string.IsNullOrEmpty(setupFileAddress)) {
            LogWrapper.Info("No options.txt file found to update");
            return;
        }

        try {
            await UpdateLanguageSettingsAsync(setupFileAddress, cancellationToken);
            await UpdateWindowSettingsAsync(setupFileAddress, cancellationToken);
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "Failed to update options.txt");
        }
    }

    private async Task<string?> GetOptionsFilePathAsync() {
        var setupFileAddress = Path.Combine(instance.IsolatedPath, "options.txt");

        if (File.Exists(setupFileAddress)) {
            return setupFileAddress;
        }

        // Check for Yosbr Mod compatibility
        var yosbrFileAddress = Path.Combine(instance.IsolatedPath, "config", "yosbr", "options.txt");
        if (File.Exists(yosbrFileAddress)) {
            McLaunchUtils.Log("将修改 Yosbr Mod 中的 options.txt");
            await HandleYosbrOptionsAsync(yosbrFileAddress);
            return yosbrFileAddress;
        }

        return null;
    }

    private static async Task HandleYosbrOptionsAsync(string filePath) {
        var lines = await File.ReadAllLinesAsync(filePath);
        var linesList = lines.ToList();

        var langLineIndex = linesList.FindIndex(line => line.StartsWith("lang:", StringComparison.OrdinalIgnoreCase));

        if (langLineIndex != -1) {
            linesList[langLineIndex] = "lang:none";
            await File.WriteAllLinesAsync(filePath, linesList);
        }
    }

    private async Task UpdateLanguageSettingsAsync(string filePath, CancellationToken cancellationToken) {
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        var optionLines = lines.ToList();

        var currentLang = ExtractLanguageFromOptions(optionLines);
        var requiredLang = DetermineRequiredLanguage(currentLang);

        if (string.Equals(currentLang, requiredLang, StringComparison.OrdinalIgnoreCase)) {
            McLaunchUtils.Log($"需要的语言为 {requiredLang}，当前语言为 {currentLang}，无需修改");
            return;
        }

        await UpdateLanguageInOptionsAsync(filePath, optionLines, currentLang, requiredLang, cancellationToken);
    }

    private static string ExtractLanguageFromOptions(IReadOnlyList<string> lines) {
        foreach (var line in lines) {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(':', 2);
            if (parts.Length >= 2 && parts[0].Trim().Equals("lang", StringComparison.OrdinalIgnoreCase)) {
                return parts[1].Trim();
            }
        }
        return "none";
    }

    /*
    *  1.0         : No language option, do not set for these versions
    *  1.1  ~ 1.5  : zh_CN works fine, zh_cn will crash (the last two letters must be uppercase, otherwise it will cause an NPE crash)
    *  1.6  ~ 1.10 : zh_CN works fine, zh_cn will automatically switch to English
    *  1.11 ~ 1.12 : zh_cn works fine, zh_CN will display Chinese but the language setting will incorrectly show English as selected
    *  1.13+       : zh_cn works fine, zh_CN will automatically switch to English
    */
    private string DetermineRequiredLanguage(string currentLang) {
        var hasExistingSaves = Directory.Exists(Path.Combine(instance.IsolatedPath, "saves"));
        var shouldUseDefault = currentLang == "none" || !hasExistingSaves;

        // Get the Minecraft version information
        var mcVersionMinor = instance.InstanceInfo.McVersionMinor;
        var mcReleaseDate = instance.InstanceInfo.McReleaseDate;
        var isUnder11 = mcReleaseDate < new DateTime(2012, 1, 12) 
                          || instance.InstanceInfo.VersionType == McVersionType.Old
                          || (mcVersionMinor == 1 && instance.InstanceInfo.McVersionBuild < 1);

        // For 1.0 and lower version, return "none" as no language option is available
        if (isUnder11) {
            return "none";
        }

        // Determine the default language based on configuration
        var defaultLang = Config.Tool.AutoChangeLanguage ? "zh_cn" : "en_us";
        var requiredLang = shouldUseDefault ? defaultLang : currentLang.ToLowerInvariant();

        // Apply version-specific language format rules
        if (!requiredLang.StartsWith("zh_")) {
            return requiredLang;
        }
        
        // 1.1 ~ 1.10: Last two letters must be uppercase (zh_CN)
        if (mcVersionMinor is >= 1 and <= 10) {
            requiredLang = requiredLang[..^2] + requiredLang[^2..].ToUpperInvariant();
        }
        
        return requiredLang;
    }

    private static async Task UpdateLanguageInOptionsAsync(
        string filePath,
        List<string> optionLines,
        string currentLang,
        string requiredLang,
        CancellationToken cancellationToken) {
        var langLineIndex = optionLines.FindIndex(line => line.StartsWith("lang:", StringComparison.OrdinalIgnoreCase));

        if (currentLang == "none") {
            optionLines.Add($"lang:{requiredLang}");
        } else if (langLineIndex != -1) {
            optionLines[langLineIndex] = $"lang:{requiredLang}";
        }

        await File.WriteAllLinesAsync(filePath, optionLines, cancellationToken);
        McLaunchUtils.Log($"已将语言从 {currentLang} 修改为 {requiredLang}");
    }

    private static async Task UpdateWindowSettingsAsync(string filePath, CancellationToken cancellationToken) {
        switch (Config.Launch.GameWindowMode) {
            case 0: // Fullscreen
                await UpdateFullscreenSettingAsync(filePath, true, cancellationToken);
                break;
            case 1: // Default - no changes needed
                break;
            default: // Windowed
                await UpdateFullscreenSettingAsync(filePath, false, cancellationToken);
                break;
        }
    }

    private static async Task UpdateFullscreenSettingAsync(string filePath, bool isFullscreen, CancellationToken cancellationToken) {
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        var linesList = lines.ToList();

        var fullscreenLineIndex = linesList.FindIndex(line => line.StartsWith("fullscreen:", StringComparison.OrdinalIgnoreCase));
        var fullscreenSetting = $"fullscreen:{(isFullscreen ? "true" : "false")}";

        if (fullscreenLineIndex != -1) {
            linesList[fullscreenLineIndex] = fullscreenSetting;
        } else {
            linesList.Add(fullscreenSetting);
        }

        await File.WriteAllLinesAsync(filePath, linesList, cancellationToken);
    }
}

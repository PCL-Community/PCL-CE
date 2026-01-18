using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Minecraft.Launch.Utils;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;

namespace PCL.Core.Minecraft.Launch.Services.Argument;

/// <summary>
/// Minecraft 启动参数构建器
/// </summary>
public class LaunchArgBuilder(IMcInstance instance, JavaInfo selectedJava, bool isDemo) {
    private readonly List<string> _arguments = [];
    private readonly IJsonBasedInstance _jsonBasedInstance = (IJsonBasedInstance) instance;

    // ReSharper disable InconsistentNaming
    // 常量定义
    private const string DEFAULT_SERVER_PORT = "25565";
    private const int JAVA_VERSION_8 = 8;
    private const int JAVA_VERSION_18 = 18;
    // ReSharper restore InconsistentNaming

    /// <summary>
    /// 构建基础的 JVM 参数
    /// </summary>
    public LaunchArgBuilder AddJvmArguments() {
        var jvmArgBuilder = new JvmArgBuilder(instance);

        if (HasModernJvmArguments()) {
            McLaunchUtils.Log("构建现代版本 JVM 参数");
            _arguments.AddRange(jvmArgBuilder.BuildModernJvmArguments(selectedJava));
        } else {
            McLaunchUtils.Log("构建传统版本 JVM 参数");
            _arguments.AddRange(jvmArgBuilder.BuildLegacyJvmArguments(selectedJava));
        }

        McLaunchUtils.Log("JVM 参数构建完成");
        return this;
    }

    /// <summary>
    /// 添加游戏参数
    /// </summary>
    public LaunchArgBuilder AddGameArguments() {
        var gameArgBuilder = new GameArgBuilder(instance);

        if (HasLegacyMinecraftArguments()) {
            McLaunchUtils.Log("构建传统版本游戏参数");
            _arguments.AddRange(gameArgBuilder.BuildLegacyGameArguments());
        }

        if (HasModernGameArguments()) {
            McLaunchUtils.Log("构建现代版本游戏参数");
            _arguments.AddRange(gameArgBuilder.BuildModernGameArguments());
        }

        McLaunchUtils.Log("游戏参数构建完成");
        return this;
    }

    /// <summary>
    /// 添加世界和服务器相关参数
    /// </summary>
    public LaunchArgBuilder AddWorldArguments(string? worldName = null, string? serverIp = null) {
        AddWorldArgument(worldName);
        AddServerArgument(worldName, serverIp);
        return this;
    }

    /// <summary>
    /// 添加其他杂项参数
    /// </summary>
    public LaunchArgBuilder AddOtherArguments() {
        AddEncodingArguments();
        FixWindowsOsNameArgument();
        AddFullscreenArgument();
        AddDemoArgument();
        AddCustomArguments();

        return this;
    }

    /// <summary>
    /// 构建最终的启动参数字符串
    /// </summary>
    public async Task<string> BuildAsync() {
        var argumentString = string.Join(' ', _arguments);
        var customEnvReplacer = new GameEnvReplacer(instance, selectedJava);
        var replaceArguments = await customEnvReplacer.BuildArgumentReplacementsAsync();

        return ProcessArgumentReplacements(argumentString, replaceArguments);
    }

    #region 私有辅助方法

    /// <summary>
    /// 检查是否有现代版本的 JVM 参数
    /// </summary>
    private bool HasModernJvmArguments() {
        return _jsonBasedInstance.VersionJson!.TryGetPropertyValue("arguments", out var argumentNode) &&
               argumentNode!.GetValueKind() == JsonValueKind.Object &&
               argumentNode.AsObject().TryGetPropertyValue("jvm", out _);
    }

    /// <summary>
    /// 检查是否有传统的 Minecraft 参数
    /// </summary>
    private bool HasLegacyMinecraftArguments() {
        return !string.IsNullOrEmpty(_jsonBasedInstance.VersionJson!["minecraftArguments"]?.ToString());
    }

    /// <summary>
    /// 检查是否有现代版本的游戏参数
    /// </summary>
    private bool HasModernGameArguments() {
        return _jsonBasedInstance.VersionJson!.TryGetPropertyValue("arguments", out var argumentNode) &&
               argumentNode!.GetValueKind() == JsonValueKind.Object &&
               argumentNode.AsObject().TryGetPropertyValue("game", out _);
    }

    /// <summary>
    /// 添加单人世界参数
    /// </summary>
    private void AddWorldArgument(string? worldName) {
        if (!string.IsNullOrEmpty(worldName)) {
            _arguments.Add($"--quickPlaySingleplayer \"{worldName}\"");
        }
    }

    /// <summary>
    /// 添加服务器参数
    /// </summary>
    private void AddServerArgument(string? worldName, string? serverIp) {
        if (!string.IsNullOrWhiteSpace(worldName)) return;

        var server = GetServerAddress(serverIp);
        if (string.IsNullOrWhiteSpace(server)) return;

        if (ShouldUseQuickPlayMultiplayer()) {
            _arguments.Add($"--quickPlayMultiplayer \"{server}\"");
        } else {
            AddLegacyServerArguments(server);
        }
    }

    /// <summary>
    /// 获取服务器地址
    /// </summary>
    private string GetServerAddress(string? serverIp) {
        return string.IsNullOrEmpty(serverIp)
            ? Config.Instance.ServerToEnter[instance.Path]
            : serverIp;
    }

    /// <summary>
    /// 判断是否应该使用快速多人游戏参数
    /// </summary>
    private bool ShouldUseQuickPlayMultiplayer() {
        return instance.InstanceInfo.ReleaseTime > new DateTime(2023, 4, 4);
    }

    /// <summary>
    /// 添加传统的服务器参数
    /// </summary>
    private void AddLegacyServerArguments(string server) {
        var (host, port) = ParseServerAddress(server);
        _arguments.Add($"--server {host} --port {port}");

        if (instance.InstanceInfo.HasPatch("optifine")) {
            HintWrapper.Show("OptiFine 与自动进入服务器可能不兼容，有概率导致材质丢失甚至游戏崩溃！", HintTheme.Error);
        }
    }

    /// <summary>
    /// 解析服务器地址和端口
    /// </summary>
    private static (string host, string port) ParseServerAddress(string server) {
        if (server.Contains(':')) {
            var parts = server.Split(':', 2);
            return (parts[0], parts[1]);
        }
        return (server, DEFAULT_SERVER_PORT);
    }

    /// <summary>
    /// 添加编码相关参数
    /// </summary>
    private void AddEncodingArguments() {
        if (selectedJava.JavaMajorVersion > JAVA_VERSION_8) {
            AddArgumentIfNotExists("-Dstdout.encoding=UTF-8");
            AddArgumentIfNotExists("-Dstderr.encoding=UTF-8");
        }

        if (selectedJava.JavaMajorVersion >= JAVA_VERSION_18) {
            AddArgumentIfNotExists("-Dfile.encoding=COMPAT");
        }
    }

    /// <summary>
    /// 添加参数（如果不存在）
    /// </summary>
    private void AddArgumentIfNotExists(string argument) {
        var key = argument.Split('=')[0];
        if (!_arguments.Any(arg => arg.StartsWith(key))) {
            _arguments.Add(argument);
        }
    }

    /// <summary>
    /// 修复 Windows 操作系统名称参数
    /// </summary>
    private void FixWindowsOsNameArgument() {
        const string targetArg = "-Dos.name=Windows 10";
        const string fixedArg = "-Dos.name=\"Windows 10\"";

        var index = _arguments.IndexOf(targetArg);
        if (index != -1) {
            _arguments[index] = fixedArg;
        }
    }

    /// <summary>
    /// 添加全屏参数
    /// </summary>
    private void AddFullscreenArgument() {
        if (Config.Launch.GameWindowMode == 0) {
            _arguments.Add("--fullscreen");
        }
    }

    /// <summary>
    /// 添加演示模式参数
    /// </summary>
    private void AddDemoArgument() {
        if (isDemo) {
            _arguments.Add("--demo");
        }
    }

    /// <summary>
    /// 添加自定义参数
    /// </summary>
    private void AddCustomArguments() {
        var instanceGameArgs = Config.Instance.GameArgs[instance.Path];
        var gameArgs = string.IsNullOrEmpty(instanceGameArgs) ? Config.Launch.GameArgs : instanceGameArgs;

        if (!string.IsNullOrWhiteSpace(gameArgs)) {
            _arguments.Add(gameArgs);
        }
    }

    /// <summary>
    /// 处理参数替换和最终格式化
    /// </summary>
    private static string ProcessArgumentReplacements(string argumentString, Dictionary<string, string> replacements) {
        var result = argumentString;

        // 处理版本类型参数的特殊情况
        if (string.IsNullOrWhiteSpace(replacements["${version_type}"])) {
            result = result.Replace(" --versionType ${version_type}", "");
            replacements["${version_type}"] = @"""";
        }

        var processedArguments = new StringBuilder();
        var arguments = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var argument in arguments) {
            var processedArg = GameEnvReplacer.ApplyReplacements(argument, replacements);
            processedArg = QuoteArgumentIfNeeded(processedArg);
            processedArguments.Append(processedArg).Append(' ');
        }

        return processedArguments.ToString().TrimEnd();
    }

    /// <summary>
    /// 根据需要为参数添加引号
    /// </summary>
    private static string QuoteArgumentIfNeeded(string argument) {
        if ((argument.Contains(' ') || argument.Contains(":\"")) && !argument.EndsWith('"')) {
            return $"\"{argument}\"";
        }
        return argument;
    }

    #endregion
}

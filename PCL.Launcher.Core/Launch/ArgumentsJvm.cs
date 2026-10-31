using System.Globalization;
using System.Net;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Launch.Utils;
using PCL.Network;

namespace PCL;

public static partial class GameLaunch
{
    private static string McLaunchArgumentsJvmOld(McInstance instance)
    {
        var dataList = new List<string>
        {
            "-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump"
        };

        var argumentJvm = Config.Instance.JvmArgs[InstanceList.McMcInstanceSelected!.PathInstance];

        if (string.IsNullOrEmpty(argumentJvm))
            argumentJvm = Config.Launch.JvmArgs;

        if (!argumentJvm.Contains("-Dlog4j2.formatMsgNoLookups=true"))
            argumentJvm += " -Dlog4j2.formatMsgNoLookups=true";

        argumentJvm = argumentJvm.Replace(" -XX:MaxDirectMemorySize=256M", "");

        dataList.Insert(0, argumentJvm);
        _AddMemoryJvmArgs(dataList);

        dataList.Add($"\"-Djava.library.path={GetNativesFolder()}\"");
        dataList.Add("-cp ${classpath}");

        _AddAuthlibInjectorJvmArgsOld(dataList);
        _AddDebugLog4jConfigJvmArgs(dataList, instance);
        _AddRendererJvmArgs(dataList);
        _AddProxyJvmArgs(dataList, instance);
        _AddLegacyFixJvmArgs(dataList, instance);
        _AddJavaWrapperJvmArgs(dataList);

        if (instance.JsonObject["mainClass"] is null)
            throw new Exception(Lang.Text("Minecraft.Launch.Error.MissingMainClass"));

        dataList.Add(instance.JsonObject["mainClass"]!.ToString());

        return dataList.Join(" ");
    }

    private static void _AddMemoryJvmArgs(List<string> dataList)
    {
        dataList.Add(
            $"-Xmn{Math.Floor(MemorySettings.GetRam(
                InstanceList.McMcInstanceSelected!,
                !mcLaunchJavaSelected!.Installation.Is64Bit) * 1024d * 0.15d)}m");

        var maxHeapArg = Math.Floor(
            MemorySettings.GetRam(
                InstanceList.McMcInstanceSelected!,
                !mcLaunchJavaSelected.Installation.Is64Bit
            )
            * 1024d);
        dataList.Add($"-Xmx{maxHeapArg}m");
        if (dataList.Any(d => d.Contains("-Xms", StringComparison.OrdinalIgnoreCase)))
            return;

        if (Config.Launch.LockMemory)
        {
            dataList.Add($"-Xms{maxHeapArg}m");
            return;
        }

        var initialRam = MemorySettings.GetInitialRam(
            InstanceList.McMcInstanceSelected!,
            !mcLaunchJavaSelected.Installation.Is64Bit);
        if (initialRam.HasValue)
            dataList.Add(
                $"-Xms{Math.Floor(initialRam.Value * 1024d)
                    .ToString(CultureInfo.InvariantCulture)}m");
    }

    private static void _AddAuthlibInjectorJvmArgsOld(List<string> dataList)
    {
        if (mcLoginLoader.output.Type != "Auth")
            return;

        if (mcLaunchJavaSelected!.Installation.MajorVersion >= 6)
            dataList.Add("-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT");

        var server = (mcLoginAuthLoader.input!.BaseUrl ?? "").Replace("/authserver", "");
        try
        {
            var response = Requester.FetchString(server);
            var prefetched = Convert.ToBase64String(Encoding.UTF8.GetBytes(response));
            dataList.Insert(
                0,
                $"-javaagent:\"{Path.Combine(LauncherPaths.PureAsciiDirectory, "authlib-injector.jar")}\"={server} "
                + $"-Dauthlibinjector.side=client -Dauthlibinjector.yggdrasil.prefetched={prefetched}");
        }
        catch (WebException ex)
        {
            throw new Exception(
                Lang.Text(
                    "Minecraft.Launch.Error.CannotConnectAuthServerWithDetail",
                    server ?? null
                )
                + ex.InnerException,
                ex);
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Launch.Error.CannotConnectAuthServer", server ?? null),
                ex);
        }
    }

    private static void _AddDebugLog4jConfigJvmArgs(List<string> dataList, McInstance instance)
    {
        if (!Config.Instance.UseDebugLof4j2Config[instance.PathIndie])
            return;

        var log4JConfigPath =
            InstanceList.McMcInstanceSelected!.releaseTime.Year >= 2017
                ? LaunchEnvUtils.ExtractDebugLog4j2Config()
                : LaunchEnvUtils.ExtractLegacyDebugLog4j2Config();
        dataList.Insert(0, $"-Dlog4j.configurationFile=\"{log4JConfigPath}\"");
    }

    private static void _AddRendererJvmArgs(List<string> dataList)
    {
        var instanceRenderer = Config.Instance.Renderer[
            InstanceList.McMcInstanceSelected!.PathInstance
        ];
        var renderer = instanceRenderer != 0 ? instanceRenderer - 1 : Config.Launch.Renderer;
        if (renderer == 0)
            return;

        var mesaLoaderWindowsTargetFile = Path.Combine(
            LauncherPaths.PureAsciiDirectory,
            "mesa-loader-windows",
            MesaLoaderWindowsVersion,
            "Loader.jar");
        var rendererName = renderer switch
        {
            1 => "llvmpipe",
            2 => "d3d12",
            _ => "zink"
        };
        dataList.Insert(0, $"-javaagent:\"{mesaLoaderWindowsTargetFile}\"={rendererName}");
    }

    private static void _AddProxyJvmArgs(List<string> dataList, McInstance instance)
    {
        if (!Config.Instance.UseProxy[instance.PathIndie]
            || !Config.Network.HttpProxy.Type.Equals(2)
            || string.IsNullOrWhiteSpace(Config.Network.HttpProxy.CustomAddress))
            return;

        try
        {
            var proxyAddress = new Uri(Config.Network.HttpProxy.CustomAddress);
            var proxyScheme = proxyAddress.Scheme.StartsWithF("https:") ? "https" : "http";
            dataList.Add($"-D{proxyScheme}.proxyHost={proxyAddress.AbsoluteUri}");
            dataList.Add($"-D{proxyScheme}.proxyPort={proxyAddress.Port}");
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Minecraft.Launch.Error.Proxy"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Minecraft.Launch.Error.Proxy"));
        }
    }

    private static void _AddLegacyFixJvmArgs(List<string> dataList, McInstance instance)
    {
        if (!McLaunchNeedsLegacyFix(instance))
            return;

        var legacyFixPath = Path.Combine(LauncherPaths.PureAsciiDirectory, "legacyfix.jar");
        dataList.Add($"-javaagent:\"{legacyFixPath}\"");

        if (instance.releaseTime < new DateTime(2011, 5, 25))
            dataList.Add("-Djava.util.Arrays.useLegacyMergeSort=true");
    }

    private static void _AddJavaWrapperJvmArgs(List<string> dataList)
    {
        if (!LauncherCommon.IsUtf8CodePage()
            || Config.Launch.DisableJlw
            || Config.Instance.DisableJlw[InstanceList.McMcInstanceSelected!.PathInstance])
            return;

        if (mcLaunchJavaSelected!.Installation.MajorVersion >= 9)
            dataList.Add(
                "--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED");
        dataList.Add($"-Doolloo.jlw.tmpdir=\"{LauncherPaths.PureAsciiDirectory.TrimEnd('\\')}\"");
        dataList.Add($"-jar \"{ExtractJavaWrapper()}\"");
    }

    private static string McLaunchArgumentsJvmNew(McInstance instance)
    {
        var dataList = new List<string>();

        _CollectInheritedJvmArguments(dataList, instance);
        SecretLaunchJvmArgs(ref dataList);

        _AddAuthlibInjectorJvmArgsNew(dataList);
        _AddLwjglUnsafeAgentJvmArgs(dataList);
        _AddDebugLog4jConfigJvmArgs(dataList, instance);
        _AddRendererJvmArgs(dataList);
        _AddProxyJvmArgs(dataList, instance);
        _AddJavaWrapperJvmArgs(dataList);

        var result = _DeduplicateJvmArguments(dataList);

        return instance.JsonObject["mainClass"] is null
            ? throw new Exception(Lang.Text("Minecraft.Launch.Error.MissingMainClass"))
            : $"{result} {instance.JsonObject["mainClass"]}";
    }

    private static void _CollectInheritedJvmArguments(List<string> dataList, McInstance instance)
    {
        var currentInstance = instance;
        while (true)
        {
            if (currentInstance.JsonObject["arguments"] is not null
                && currentInstance.JsonObject["arguments"]!["jvm"] is not null)
                foreach (var subJson in currentInstance.JsonObject["arguments"]!["jvm"]!.AsArray())
                {
                    if (subJson!.GetValueKind() == JsonValueKind.String)
                    {
                        dataList.Add(subJson.ToString());
                        continue;
                    }

                    if (!Libraries.McJsonRuleCheck(subJson["rules"]!))
                        continue;

                    if (subJson["value"]!.GetValueKind() == JsonValueKind.String)
                    {
                        dataList.Add(subJson["value"]!.ToString());
                        continue;
                    }

                    dataList.AddRange(
                        subJson["value"]!.AsArray().Select(value => value!.ToString()));
                }

            if (string.IsNullOrEmpty(currentInstance.InheritInstanceName))
                break;

            currentInstance = new McInstance(currentInstance.InheritInstanceName);
        }
    }

    private static void _AddAuthlibInjectorJvmArgsNew(List<string> dataList)
    {
        if (mcLoginLoader.output.Type != "Auth")
            return;

        if (mcLaunchJavaSelected!.Installation.MajorVersion >= 6)
            dataList.Add("-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT");

        var server = (mcLoginAuthLoader.input!.BaseUrl ?? "").Replace("/authserver", "");
        try
        {
            var response = NetFacade.NetGetCodeByRequestRetry(server, Encoding.UTF8)?.ToString();
            var prefetched = Convert.ToBase64String(Encoding.UTF8.GetBytes(response!));
            dataList.Insert(
                0,
                $"-javaagent:\"{Path.Combine(LauncherPaths.PureAsciiDirectory, "authlib-injector.jar")}\"={server} "
                + $"-Dauthlibinjector.side=client -Dauthlibinjector.yggdrasil.prefetched={prefetched}");
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Launch.Error.CannotConnectAuthServer", server ?? null),
                ex);
        }
    }

    private static void _AddLwjglUnsafeAgentJvmArgs(List<string> dataList)
    {
        if (!McLaunchUsesLwjglUnsafeAgent(InstanceList.McMcInstanceSelected!))
            return;

        LauncherCommon.Log(
            $"获取到的 LWJGL 版本：{McLaunchGetLwjglVersion(InstanceList.McMcInstanceSelected!)}");
        dataList.Insert(
            0,
            $"-javaagent:\"{LauncherPaths.PureAsciiDirectory}lwjgl-unsafe-agent.jar\"");
    }

    private static string _DeduplicateJvmArguments(List<string> dataList)
    {
        var deDuplicateDataList = new List<string>();
        for (var i = 0; i < dataList.Count; i++)
        {
            var currentEntry = dataList[i];
            if (dataList[i].StartsWithF("-"))
                while (i < dataList.Count - 1)
                {
                    if (dataList[i + 1].StartsWithF("-"))
                        break;

                    i += 1;
                    currentEntry += $" {dataList[i]}";
                }

            deDuplicateDataList.Add(currentEntry.Trim().Replace("McEmu= ", "McEmu="));
        }

        deDuplicateDataList.Remove("-XX:MaxDirectMemorySize=256M");

        return deDuplicateDataList.Distinct().ToList().Join(" ");
    }
}
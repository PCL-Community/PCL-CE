using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.Utils;

namespace PCL.Core.App.Tools;

[LifecycleScope("dependency-check", "依赖检查")]
[LifecycleService(LifecycleState.Running)]
public sealed partial class DependencyCheckService
{
    [LifecycleStart]
    private static async Task _StartAsync()
    {
        Context.Info(Lang.Text("Tools.Test.Dependency.Checking"));

        if (RuntimeInformation.OSArchitecture.Equals(Architecture.Arm64))
            await _CheckAndAskAsync("Microsoft.D3DMappingLayers", "OpenGL 兼容包", "9nqpsl29bfff")
                .ConfigureAwait(false);

        await _CheckAndAskAsync("Microsoft.WebpImageExtension", "WebP 组件包", "9pg2dk419drg")
            .ConfigureAwait(false);
    }

    private static async Task _CheckAndAskAsync(string packageId, string packageName, string storeId)
    {
        if (!await _CheckPackageAsync(packageId))
        {
            Context.Info($"检测到依赖缺失 (package-id = {packageId})");
            var selection = MsgBoxWrapper.Show(
                Lang.Text("Tools.Test.Dependency.MissingMessage", packageName),
                buttons: [Lang.Text("Common.Action.Confirm"), Lang.Text("Tools.Test.Dependency.Later")]);
            if (selection == 1) _LaunchMsStore(storeId);
        }
    }

    private static void _LaunchMsStore(string id)
    {
        Context.Info($"正在打开微软应用商店 (id = {id})");
        var psi = new ProcessStartInfo()
        {
            FileName = $"ms-windows-store://pdp?launch=true&productid={id}&mode=full",
            UseShellExecute = true
        };
        using var ps = new Process() { StartInfo = psi };
        ps.Start();
    }

    private static async Task<bool> _CheckPackageAsync(string id)
    {
        var command = $"Get-AppxPackage -Name *{id}* | ConvertTo-Json";

        var psi = new ProcessStartInfo()
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -Command \"{command}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var ps = new Process() { StartInfo = psi };
        ps.Start();

        JsonNode? jnode;
        try
        {
            // ConvertTo-Json 结构不一定可靠，不太能 Serialize
            jnode = await JsonNode.ParseAsync(ps.StandardOutput.BaseStream, JsonCompat.NodeOptions, JsonCompat.DocumentOptions);
        }
        catch
        {
            return false;
        }

        if (jnode is null) return false;
        if (jnode.GetValueKind().Equals(JsonValueKind.Array))
        {
            var hasPack = false;
            foreach (var node in jnode.AsArray())
            {
                if (node is not null && CheckPackSuit(node, id))
                {
                    hasPack = true;
                    break;
                }

            }

            return hasPack;
        }
        else
        {
            return CheckPackSuit(jnode, id);
        }

        // 检查当前的 JsonNode 下是否是符合的包
        static bool CheckPackSuit(JsonNode jnode, string id)
        {
            var findedPackName = JsonCompat.ToObject<string>(jnode["Name"]);
            return findedPackName is not null && findedPackName.Contains(id);
        }
    }
}

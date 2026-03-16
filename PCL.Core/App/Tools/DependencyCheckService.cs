using PCL.Core.App.IoC;
using PCL.Core.UI;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace PCL.Core.App.Tools;

[LifecycleScope("dependency-check", "依赖检查")]
[LifecycleService(LifecycleState.Running)]
public sealed partial class DependencyCheckService
{
    [LifecycleStart]
    private static async Task _Start()
    {
        Context.Info("开始环境检查……");

        if (RuntimeInformation.OSArchitecture.Equals(Architecture.Arm64))
            await _CheckAndAsk("Microsoft.D3DMappingLayers", "OpenGL 兼容包", "9nqpsl29bfff")
                .ConfigureAwait(false);

        await _CheckAndAsk("Microsoft.WebpImageExtension", "WebP 组件包", "9pg2dk419drg")
            .ConfigureAwait(false);
    }

    private static async Task _CheckAndAsk(string packageId, string packageName, string storeId)
    {
        if (!await _CheckPackageAsync(packageId))
        {
            Context.Info($"检测到依赖缺失 (package-id = {packageId})");
            var selection = MsgBoxWrapper.Show(
                $"当前系统环境缺失软件运行所需依赖“{packageName}”\n\n点击确定打开微软应用商店安装",
                buttons: ["确定", "稍后"]);
            if (selection == 1) _LaunchMsStore(storeId);
        }
    }

    private static void _LaunchMsStore(string id)
    {
        Context.Info($"正在打开微软应用商店 (id = {id})");
        var psi = new ProcessStartInfo()
        {
            FileName = $"ms-windows-store://pdp?launch=true&hl=zh-cn&gl=cn&referrer=storeforweb&productid={id}&mode=full",
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
            jnode = await JsonNode.ParseAsync(ps.StandardOutput.BaseStream);
        }
        catch
        {
            return false;
        }

        if (jnode == null) return false;
        if (jnode.GetValueKind().Equals(JsonValueKind.Array))
        {
            var hasPack = false;
            foreach (var node in jnode.AsArray())
            {
                if (node != null && CheckPackSuit(node, id))
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
            var findedPackName = jnode["Name"]?.GetValue<string>();
            return findedPackName != null && findedPackName.Contains(id);
        }
    }
}

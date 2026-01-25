using System;
using System.Threading.Tasks;
using PCL.Core.Minecraft.Instance.Interface;

namespace PCL.Core.Minecraft.Launch.Services.Argument;

public class ArgBuildService(IMcInstance instance, bool isDemo, JavaInfo selectedJavaInfo /*, LoginResult loginResult*/) {
    public async Task<string> BuildArgumentsAsync() {
        try {
            var builder = new LaunchArgBuilder(instance, selectedJavaInfo, isDemo /*, loginResult*/);
            var argBuilder = builder
                .AddJvmArguments()
                .AddGameArguments()
                .AddOtherArguments()
                .AddWorldArguments();
            var launchArg = await argBuilder.BuildAsync();

            return launchArg;
        } catch (Exception ex) {
            throw new InvalidOperationException("构建启动参数失败", ex);
        }
    }
}

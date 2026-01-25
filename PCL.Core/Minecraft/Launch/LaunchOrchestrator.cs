using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Minecraft.Launch.Services;
using PCL.Core.Minecraft.Launch.Services.Argument;

namespace PCL.Core.Minecraft.Launch;

public class LaunchOrchestrator(IMcInstance instance) {
    private static StartUpService? _startUpService;
    private static JavaSelectService? _javaSelectService;
    private static ArgBuildService? _argBuildService;
    private static PreLaunchService? _preLaunchService;
    private static CustomLaunchService? _customLaunchService;
    private static MinecraftLaunchService? _minecraftLaunchService;
    private static LaunchMonitorService? _launchMonitorService;
    private static PostLaunchService? _postLaunchService;

    public async Task LaunchMinecraftAsync() {
        using var cts = new CancellationTokenSource();
    
        // 1. 预检查
        _startUpService ??= new StartUpService(instance);
        _startUpService.Validate(cts);
        
        // 2. 认证
        // var authResult = await AuthenticationManager.AuthenticateAsync();
        
        // 3. Java 检查
        _javaSelectService ??= new JavaSelectService(instance);
        var selectedJava = await _javaSelectService.SelectBestJavaAsync();
        
        // 4. 补全文件
        // var argumentsResult = ArgumentBuilder.BuildArguments(options, authResult.Value, javaResult.Value);
        
        // 5. 构建启动参数
        _argBuildService ??= new ArgBuildService(instance, false, selectedJava);
        var launchArg = await _argBuildService.BuildArgumentsAsync();
        
        // 6. 解压文件
        
        // 7. 预启动游戏
        _preLaunchService ??= new PreLaunchService(instance, selectedJava);
        await _preLaunchService.McLaunchPrerunAsync(cts.Token);
        
        // 8. 自定义启动
        _customLaunchService ??= new CustomLaunchService(instance, selectedJava, launchArg); 
        await _customLaunchService.ExecuteCustomCommandAsync(cts.Token);
        
        // 9. 启动游戏
        _minecraftLaunchService ??= new MinecraftLaunchService(instance, selectedJava, launchArg);
        _minecraftLaunchService.LaunchMinecraft();
        
        // 10. 启动监控
        _launchMonitorService ??= new LaunchMonitorService(instance, selectedJava);
        _launchMonitorService.MonitorLaunch();
        
        // 11. 启动后处理
        _postLaunchService ??= new PostLaunchService(instance);
        _postLaunchService.LaunchPostRun();
    }
}


using System;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.Minecraft.Folder;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Minecraft.Launch.Utils;
using PCL.Core.UI;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Minecraft.Launch.Services;

/// <summary>
/// 启动前预检查服务
/// </summary>
public class StartUpService(IMcInstance? instance) {
    /// <summary>
    /// 验证启动配置和环境
    /// </summary>
    public void Validate(CancellationTokenSource source) {
        // 检查路径
        _ValidatePaths(source);

        // 检查实例状态
        _ValidateInstance();

        // 检查档案有效性
        _ValidateProfile();

        // 检查登录要求
        _ValidateLoginRequirements();
        
        McLaunchUtils.Log("预检查通过，准备启动游戏");
    }

    private void _ValidatePaths(CancellationTokenSource source) {
        if (instance == null) {
            throw new InvalidOperationException("未选择 Minecraft 实例");
        }

        // 检查路径中的特殊字符
        if (instance.IsolatedPath.Contains('!') || instance.IsolatedPath.Contains(';')) {
            throw new InvalidOperationException($"游戏路径中不可包含 ! 或 ;（{instance.IsolatedPath}）");
        }

        // UTF-8 代码页下的路径检查
        if (EncodingUtils.IsDefaultEncodingUtf8() && !Config.Hint.NonAsciiGamePath && !instance.Path.IsASCII()) {
            var userChoice = MsgBoxWrapper.Show(
                $"欲启动实例 \"{instance.Name}\" 的路径中存在可能影响游戏正常运行的字符（非 ASCII 字符），是否仍旧启动游戏？\n\n如果不清楚具体作用，你可以先选择 \"继续\"，发现游戏在启动后很快出现崩溃的情况后再尝试修改游戏路径等操作",
                "游戏路径检查",
                buttons: [
                    "继续",
                    "返回处理",
                    "不再提示"
                ]);
            switch (userChoice) {
                case 1:
                    // 继续
                    break;
                case 2:
                    source.Cancel();
                    break;
                case 3:
                    // 不再提示
                    Config.Hint.NonAsciiGamePath = true;
                    break;
            }
        }
    }

    private void _ValidateInstance() {
        try {
            instance!.Load();
            if (instance.CardType == McInstanceCardType.Error) {
                throw new InvalidOperationException($"Minecraft 存在问题：{instance.Desc}");
            }
        } catch (Exception ex) {
            throw new InvalidOperationException($"加载实例失败：{ex.Message}");
        }
    }

    // TODO: 等待档案部分实现
    private void _ValidateProfile() {
        /*
        if (SelectedProfile == null)
            return Result.Failed("请先选择一个档案再启动游戏！");

        // 简化实现
        var checkResult = IsProfileVaild();
        if (!string.IsNullOrEmpty(checkResult))
            return Result.Failed(checkResult);

        return Result.Success();
        */
    }

    // TODO: 等待档案部分实现
    private void _ValidateLoginRequirements() {
        /*
        // 检查是否要求正版验证
        if (McFolderService.FolderManager.Current.Version.HasLabyMod || Setup.Get("VersionServerLoginRequire", McFolderService.FolderManager.Current) == 1) {
            if (SelectedProfile.Type != McLoginType.Ms)
                return Result.Failed("当前实例要求使用正版验证，请使用正版验证档案启动游戏！");
        }

        // 检查是否要求第三方验证
        if (Setup.Get("VersionServerLoginRequire", McFolderService.FolderManager.Current) == 2) {
            if (SelectedProfile.Type != McLoginType.Auth)
                return Result.Failed("当前实例要求使用第三方验证，请使用第三方验证档案启动游戏！");

            var requiredServer = Setup.Get("VersionServerAuthServer", McFolderService.FolderManager.Current);
            if (SelectedProfile.Server.BeforeLast("/authserver") != requiredServer)
                return Result.Failed("当前档案使用的第三方验证服务器与实例要求使用的不一致！");
        }

        // 检查是否要求正版或第三方验证
        if (Setup.Get("VersionServerLoginRequire", McFolderService.FolderManager.Current) == 3) {
            if (SelectedProfile.Type == McLoginType.Legacy)
                return Result.Failed("当前实例要求使用正版验证或第三方验证，请使用符合要求的档案启动游戏！");

            if (SelectedProfile.Type == McLoginType.Auth) {
                var requiredServer = Setup.Get("VersionServerAuthServer", McFolderService.FolderManager.Current);
                if (SelectedProfile.Server.BeforeLast("/authserver") != requiredServer)
                    return Result.Failed("当前档案使用的第三方验证服务器与实例要求使用的不一致！");
            }
        }

        return Result.Success();
        */
    }
}

/// <summary>
/// PreCheckService 的静态工厂类，用于简化使用
/// </summary>
public static class PreCheckServiceFactory {
    /// <summary>
    /// 为当前实例创建 PreCheckService 并执行预检查
    /// </summary>
    public static async Task PreCheckForCurrentInstanceAsync(CancellationTokenSource source) {
        var currentInstance = FolderService.FolderManager.CurrentInst;

        var service = new StartUpService(currentInstance);
        service.Validate(source);

        // TODO 实例预检查逻辑
        await Task.CompletedTask; // 先临时骗一下编译器
    }
}

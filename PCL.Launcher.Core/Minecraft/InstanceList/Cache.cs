using PCL.Core.App;

namespace PCL;

public static partial class InstanceList
{
    private static Dictionary<McInstanceCardType, List<McInstance>>? InitMcInstanceListWithCache(
        string path)
    {
        var results = new Dictionary<McInstanceCardType, List<McInstance>>();
        try
        {
            var cardCount = int.Parse(
                LauncherCommon.ReadIni(
                    $"{path}PCL.ini",
                    "CardCount",
                    (-1).ToString()));
            if (cardCount == -1)
                return null;

            for (int i = 0, loopTo = cardCount - 1; i <= loopTo; i++)
            {
                var cardType =
                    (McInstanceCardType)int.Parse(
                        LauncherCommon.ReadIni(
                            $"{path}PCL.ini",
                            $"CardKey{i + 1}",
                            "0"));
                var instanceList = new List<McInstance>();

                foreach (var folder in LauncherCommon
                             .ReadIni(
                                 $"{path}PCL.ini",
                                 $"CardValue{i + 1}",
                                 ":")
                             .Split(":"))
                {
                    if (string.IsNullOrEmpty(folder))
                        continue;

                    var versionFolder = $@"{path}versions\{folder}\";
                    if (File.Exists($"{versionFolder}.pclignore"))
                    {
                        if (_isFirstMcInstanceListLoad)
                        {
                            LauncherCommon.Log($"[Minecraft] 清理残留的忽略项目：{versionFolder}");
                            File.Delete($"{versionFolder}.pclignore");
                        }
                        else
                        {
                            LauncherCommon.Log($"[Minecraft] 跳过要求忽略的项目：{versionFolder}");
                            continue;
                        }
                    }

                    try
                    {
                        var instance = new McInstance(versionFolder);
                        instanceList.Add(instance);
                        var instanceCfg = States.Instance;
                        instance.CustomDesc = instanceCfg.CustomInfo[instance.PathInstance];
                        var hasCachedInfo = false;

                        if (!instanceCfg.LogoPathConfig.IsDefault(instance.PathInstance))
                            instance.Logo = instanceCfg.LogoPath[instance.PathInstance];

                        if (!instanceCfg.ReleaseTimeConfig.IsDefault(instance.PathInstance))
                            instance.releaseTime = DateTime.Parse(
                                instanceCfg.ReleaseTime[instance.PathInstance]);

                        if (!instanceCfg.StateConfig.IsDefault(instance.PathInstance))
                            instance.state = (McInstanceState)instanceCfg
                                .State[instance.PathInstance];

                        instance.IsStar = instanceCfg
                            .Starred[instance.PathInstance];
                        instance.displayType = (McInstanceCardType)instanceCfg
                            .CardType[instance.PathInstance];

                        if (instance.state != McInstanceState.Error
                            && !instanceCfg.VanillaVersionNameConfig
                                .IsDefault(instance.PathInstance)
                            && !instanceCfg.VanillaVersionConfig
                                .IsDefault(instance.PathInstance))
                        {
                            var instanceInfo = new McInstanceInfo
                            {
                                Fabric = instanceCfg.FabricVersion[instance.PathInstance],
                                LegacyFabric = instanceCfg
                                    .LegacyFabricVersion[instance.PathInstance],
                                Quilt = instanceCfg.QuiltVersion[instance.PathInstance],
                                Forge = instanceCfg.ForgeVersion[instance.PathInstance],
                                LabyMod = instanceCfg.LabyModVersion[instance.PathInstance],
                                NeoForge = instanceCfg.NeoForgeVersion[instance.PathInstance],
                                Cleanroom = instanceCfg.CleanroomVersion[instance.PathInstance],
                                OptiFine = instanceCfg.OptiFineVersion[instance.PathInstance],
                                HasLiteLoader = instanceCfg.HasLiteLoader[instance.PathInstance],
                                VanillaName = instanceCfg.VanillaVersionName[instance.PathInstance],
                                vanilla = new Version(
                                    instanceCfg.VanillaVersion[instance.PathInstance])
                            };
                            instanceInfo.HasFabric = !string.IsNullOrEmpty(instanceInfo.Fabric);
                            instanceInfo.HasLegacyFabric =
                                !string.IsNullOrEmpty(instanceInfo.LegacyFabric);
                            instanceInfo.HasQuilt = !string.IsNullOrEmpty(instanceInfo.Quilt);
                            instanceInfo.HasForge = !string.IsNullOrEmpty(instanceInfo.Forge);
                            instanceInfo.HasNeoForge = !string.IsNullOrEmpty(instanceInfo.NeoForge);
                            instanceInfo.HasCleanroom =
                                !string.IsNullOrEmpty(instanceInfo.Cleanroom);
                            instanceInfo.HasOptiFine = !string.IsNullOrEmpty(instanceInfo.OptiFine);
                            instance.Info = instanceInfo;
                            hasCachedInfo = true;
                        }

                        if (instance.state == McInstanceState.Error)
                        {
                            // 重新获取实例错误信息。
                            instance.state = McInstanceState.Original;
                            instance.Check();
                            if (instance.state == McInstanceState.Original)
                            {
                                LauncherCommon.Log(
                                    $"[Minecraft] 实例 {instance.Name} 的错误状态已解除，重新加载缓存");
                                return null;
                            }
                        }
                        else
                        {
                            if (!hasCachedInfo)
                            {
                                LauncherCommon.Log($"[Minecraft] 实例 {instance.Name} 缺少缓存信息，重新加载");
                                return null;
                            }

                            instance.MarkDescriptionLoaded();
                        }

                        if (string.IsNullOrEmpty(instance.Logo))
                        {
                            LauncherCommon.Log($"[Minecraft] 实例 {instance.Name} 未被加载");
                            return null;
                        }
                    }

                    catch (Exception ex)
                    {
                        LauncherCommon.Log(ex, $"读取实例加载缓存失败（{folder}）");
                        return null;
                    }
                }

                if (instanceList.Count != 0)
                    results.Add(cardType, instanceList);
            }

            return results;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "读取实例缓存失败");
            return null;
        }
    }
}
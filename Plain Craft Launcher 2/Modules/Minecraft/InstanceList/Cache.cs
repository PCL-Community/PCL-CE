using System.IO;
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
                Common.ReadIni(
                    $"{path}PCL.ini",
                    "CardCount",
                    (-1).ToString()));
            if (cardCount == -1)
                return null;

            for (int i = 0, loopTo = cardCount - 1; i <= loopTo; i++)
            {
                var cardType =
                    (McInstanceCardType)int.Parse(
                        Common.ReadIni(
                            $"{path}PCL.ini",
                            $"CardKey{i + 1}",
                            "0"));
                var instanceList = new List<McInstance>();

                foreach (var folder in Common
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
                            Common.Log($"[Minecraft] 清理残留的忽略项目：{versionFolder}");
                            File.Delete($"{versionFolder}.pclignore");
                        }
                        else
                        {
                            Common.Log($"[Minecraft] 跳过要求忽略的项目：{versionFolder}");
                            continue;
                        }
                    }

                    try
                    {
                        var instance = new McInstance(versionFolder);
                        instanceList.Add(instance);
                        var instanceCfg = States.Instance;
                        instance.Desc = instanceCfg.CustomInfo[instance.PathInstance];

                        if (string.IsNullOrEmpty(instance.Desc))
                            instance.Desc = instanceCfg.Info[instance.PathInstance];

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
                            instanceInfo.HasFabric = instanceInfo.Fabric.Length != 0;
                            instanceInfo.HasLegacyFabric = instanceInfo.LegacyFabric.Length != 0;
                            instanceInfo.HasQuilt = instanceInfo.Quilt.Length != 0;
                            instanceInfo.HasForge = instanceInfo.Forge.Length != 0;
                            instanceInfo.HasNeoForge = instanceInfo.NeoForge.Length != 0;
                            instanceInfo.HasCleanroom = instanceInfo.Cleanroom.Length != 0;
                            instanceInfo.HasOptiFine = instanceInfo.OptiFine.Length != 0;
                            instance.Info = instanceInfo;
                        }

                        if (instance.state == McInstanceState.Error)
                        {
                            var oldDesc = instance.Desc;

                            instance.state = McInstanceState.Original;
                            instance.Check();

                            var customInfo = States.Instance.CustomInfo[instance.PathInstance];
                            if (instance.state == McInstanceState.Original
                                || (string.IsNullOrEmpty(customInfo)
                                    && (oldDesc ?? "") != (instance.Desc ?? "")))
                            {
                                Common.Log(
                                    $"[Minecraft] 实例 {instance.Name} 的错误状态已变更，新的状态为：{instance.Desc}");
                                return null;
                            }
                        }

                        if (string.IsNullOrEmpty(instance.Logo))
                        {
                            Common.Log($"[Minecraft] 实例 {instance.Name} 未被加载");
                            return null;
                        }
                    }

                    catch (Exception ex)
                    {
                        Common.Log(ex, $"读取实例加载缓存失败（{folder}）");
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
            Common.Log(ex, "读取实例缓存失败");
            return null;
        }
    }
}

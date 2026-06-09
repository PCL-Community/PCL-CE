using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.Minecraft.Profile.Models;

namespace PCL.Core.Minecraft.Profile;

[LifecycleScope("profile", "档案服务")]
public partial class ProfileService
{
    private static ProfileManagement<ProfileJson<Models.Profile>> _newProfileProvider = new();
    private static ProfileManagement<ProfileJson<OldProfile>> _oldProfileProvider = new();

    [LifecycleStart]
    private static async Task _Start()
    {
        _newProfileProvider.LoadFromString(Config.System.Profiles);
    }

    private static void _MigrateProfile()
    {
        
        var profileLocation = Path.Combine(Paths.SharedData, "profiles.json");
        var profileMigrateFile = Path.Combine(Paths.SharedData, "pcl.ce.migrated");
        if (Path.Exists(profileMigrateFile))
        {
            Context.Debug("已迁移档案信息，跳过检查");
            return;
        }
        Context.Info("开始迁移旧版本档案信息");
        try
        {
            _oldProfileProvider.LoadFromPath(profileLocation);
        }
        catch (UnauthorizedAccessException)
        {
            Context.Error("读取旧版档案信息失败：权限不足");
        }
        catch (Exception ex)
        {
            Context.Error("迁移档案信息失败", ex);
        }
    }
    
    private static void _Import(){}
}
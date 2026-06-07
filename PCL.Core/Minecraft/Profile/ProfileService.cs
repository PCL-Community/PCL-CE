using System;
using System.IO;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.Minecraft.Profile.Models;

namespace PCL.Core.Minecraft.Profile;

[LifecycleScope("profile", "档案服务")]
public partial class ProfileService
{
    private static ProfileManagement<ProfileJson<Models.Profile>> _newProfileProvider = new();
    private static ProfileManagement<ProfileJson<Models.OldProfile>> _oldProfileProvider = new();

    [LifecycleStart]
    private static async Task _Start()
    {
    }

    private static void _MigrateProfile()
    {
        
        var profileLocation = Path.Combine(Paths.SharedData, "profiles.json");
        var profileMigrateFIle = Path.Combine(Paths.SharedData, "pcl.ce.migrated");
        if (!Path.Exists(profileMigrateFIle))
        {
            Context.Debug("已迁移档案信息，跳过检查");
            return;
        }
        Context.Info("开始迁移旧版本档案信息");
        try
        {

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
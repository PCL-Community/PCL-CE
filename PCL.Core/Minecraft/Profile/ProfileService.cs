using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.Minecraft.Profile.Models;

namespace PCL.Core.Minecraft.Profile;

[LifecycleScope("profile", "档案服务")]
[LifecycleService(LifecycleState.Loaded)]
public partial class ProfileService
{
    private static ProfileManagement<McProfile> _newProfileProvider = new();
    private static ProfileManagement<Models.OldProfile> _oldProfileProvider = new();

    [LifecycleStart]
    private static async Task _Start()
    {
        _newProfileProvider.LoadFromString(Config.System.Profiles);
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
            _oldProfileProvider.LoadFromPath(profileLocation);
            foreach (var profile in _oldProfileProvider.GetAll())
            {
                var newProfile = new McProfile
                {
                    UserName = profile.UserName,
                    Uuid = profile.Uuid,
                    SkinPath = "",
                    ExpiredAt = default,
                    AccessToken = profile.AccessToken,
                    RefreshToken = profile.RefreshToken ?? string.Empty,
                    TokenType = profile.TokenType,
                    ProfileType = profile.Type switch
                    {
                        "microsoft" => ProfileType.Microsoft,
                        "authlib" => ProfileType.Authlib,
                        _ => ProfileType.Offline
                    }
                };
                _newProfileProvider.Add(newProfile);
            }
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

    private static bool _isCheckedLicense;

    public static bool HasValidLicense
    {
        get
        {
            if (!_isCheckedLicense)
                field = _newProfileProvider.GetAll().Any(p => p.ProfileType == ProfileType.Microsoft);
            return field;
        }
        private set;
    }
}
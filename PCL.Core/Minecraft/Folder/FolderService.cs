using System.Threading.Tasks;
using PCL.Core.App;
namespace PCL.Core.Minecraft.Folder;


[LifecycleService(LifecycleState.Running)]
[LifecycleScope("folder", "实例目录管理")]
public sealed partial class FolderService 
{
    private static FolderManager? _folderManager;
    public static FolderManager FolderManager => _folderManager!;
    
    [LifecycleStart]
    private static void _Start() {
        if (_folderManager == null) {
            Context.Info("Start to initialize folder manager.");

            _folderManager = new FolderManager();

            // Task.Run(async () => await _folderManager.McFolderListLoadAsync());
        }
    }
}
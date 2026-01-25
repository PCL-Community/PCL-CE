using System.Threading.Tasks;
using PCL.Core.App;
namespace PCL.Core.Minecraft.Folder;


[LifecycleService(LifecycleState.Running)]
public sealed class FolderService : GeneralService {
    private static LifecycleContext? _context;
    private static LifecycleContext Context => _context!;
    
    public FolderService() : base("folder", "实例目录管理") {
        _context = Lifecycle.GetContext(this);
    }

    private static FolderManager? _folderManager;
    public static FolderManager FolderManager => _folderManager!;
    
    public override void Start() {
        if (_folderManager == null) {
            Context.Info("Start to initialize folder manager.");

            _folderManager = new FolderManager();

            // Task.Run(async () => await _folderManager.McFolderListLoadAsync());
        }
    }
}
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>为视图模型安装弹窗服务实现。</summary>
public static class DialogServiceInstaller
{
    private static readonly DialogService Instance = new();

    public static void Install(ViewModelBase vm) => vm.Dialog = Instance;
}

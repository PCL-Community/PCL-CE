using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageInstanceModBrowser
{
    private const int PageSize = 20;

    private static McInstance? _contextInstance;
    private readonly ModComp.CompProjectStorage _storage = new();
    private ModLoader.LoaderTask<ModComp.CompProjectRequest, int>? _loader;
    private bool _isLoading;
    private bool _hasMore = true;
    private string _lastQuery = "";

    public PageInstanceModBrowser()
    {
        InitializeComponent();
        PanSearchBox.Search += (_, _) => StartSearch();
        PanSearchBox.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) StartSearch();
        };
        Loaded += (_, _) =>
        {
            if (_contextInstance is not null && _storage.results.Count == 0 && _loader is null)
                StartSearch();
        };
    }

    public static void SetContext(McInstance instance)
    {
        _contextInstance = instance;
    }

    private void StartSearch()
    {
        if (_contextInstance is null || _isLoading) return;
        _isLoading = true;
        _hasMore = true;
        _lastQuery = PanSearchBox.Text?.Trim() ?? "";

        CardResults.Visibility = Visibility.Collapsed;
        PanLoad.Visibility = Visibility.Visible;
        PanLoadMore.Visibility = Visibility.Collapsed;
        HintError.Visibility = Visibility.Collapsed;
        PanResults.Children.Clear();
        _storage.results.Clear();
        _storage.curseForgeOffset = 0;
        _storage.modrinthOffset = 0;
        _storage.curseForgeTotal = -1;
        _storage.modrinthTotal = -1;

        DoLoad(0);
    }

    private void LoadNextPage()
    {
        if (_isLoading || !_hasMore) return;
        _isLoading = true;
        PanLoad.Visibility = Visibility.Collapsed;
        PanLoadMore.Visibility = Visibility.Visible;
        DoLoad(_storage.results.Count / PageSize);
    }

    private void DoLoad(int page)
    {
        if (_contextInstance is null) return;

        var vanillaName = _contextInstance.Info.VanillaName;
        var loaderType = GetLoaderType(_contextInstance);

        _loader = new ModLoader.LoaderTask<ModComp.CompProjectRequest, int>(
            "搜索模组",
            ModComp.CompProjectsGet,
            () => new ModComp.CompProjectRequest(ModComp.CompType.Mod, _storage, (page + 1) * PageSize)
            {
                gameVersion = vanillaName,
                modLoader = loaderType,
                searchText = _lastQuery,
                sort = ModComp.CompSortType.Downloads,
                source = ModComp.CompSourceType.Any
            })
        { reloadTimeout = 60 * 1000 };

        _loader.OnStateChanged = _ =>
        {
            if (_loader.State == ModBase.LoadState.Finished)
            {
                var libKey = Lang.Text("Download.Comp.Category.Library");
                var newItems = _storage.results
                    .Skip(page * PageSize)
                    .Where(r => !r.Tags.Contains(libKey))
                    .ToList();

                ModBase.RunInUi(() =>
                {
                    PanLoad.Visibility = Visibility.Collapsed;
                    PanLoadMore.Visibility = Visibility.Collapsed;
                    _isLoading = false;
                    RenderItems(newItems);

                    var curseForgeDone = _storage.curseForgeOffset >= _storage.curseForgeTotal &&
                                         _storage.curseForgeTotal >= 0;
                    var modrinthDone = _storage.modrinthOffset >= _storage.modrinthTotal &&
                                       _storage.modrinthTotal >= 0;
                    _hasMore = !curseForgeDone || !modrinthDone;

                    if (PanResults.Children.Count == 0)
                    {
                        HintError.Text = "未找到匹配的模组，请尝试其他关键词";
                        HintError.Visibility = Visibility.Visible;
                    }
                });
            }
            else if (_loader.State == ModBase.LoadState.Failed)
            {
                ModBase.RunInUi(() =>
                {
                    _isLoading = false;
                    PanLoad.Visibility = Visibility.Collapsed;
                    PanLoadMore.Visibility = Visibility.Collapsed;
                    if (PanResults.Children.Count == 0)
                    {
                        HintError.Text = $"搜索失败：{_loader.Error?.Message ?? "请检查网络连接"}";
                        HintError.Visibility = Visibility.Visible;
                    }
                });
            }
        };

        _loader.Start();
    }

    private void RenderItems(List<ModComp.CompProject> items)
    {
        if (items.Count == 0 && PanResults.Children.Count == 0) return;
        CardResults.Visibility = Visibility.Visible;

        foreach (var result in items)
        {
            var virtualItem = result.ToCompItem(false, false);
            var compItem = (MyCompItem)virtualItem; // Init() 触发，返回实际元素
            compItem.SkipDefaultNavigation = true;
            compItem.ShowInstanceButtons = true;
            compItem.Click += (_, _) =>
            {
                PageInstanceModDetail.SetContext(result, _contextInstance!);
                ModMain.frmMain.PageChange(FormMain.PageType.InstanceModDetail);
            };
            PanResults.Children.Add(compItem); // 添加实际元素，而非虚拟包装
        }
    }

    private void PanBack_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isLoading || !_hasMore) return;
        var sv = (MyScrollViewer)sender;
        if (sv.VerticalOffset + sv.ViewportHeight + 200 >= sv.ExtentHeight)
            LoadNextPage();
    }

    private static ModComp.CompLoaderType GetLoaderType(McInstance instance)
    {
        if (instance.Info.HasFabric) return ModComp.CompLoaderType.Fabric;
        if (instance.Info.HasForge) return ModComp.CompLoaderType.Forge;
        if (instance.Info.HasNeoForge) return ModComp.CompLoaderType.NeoForge;
        if (instance.Info.HasQuilt) return ModComp.CompLoaderType.Quilt;
        return ModComp.CompLoaderType.Any;
    }
}

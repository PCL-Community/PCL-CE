using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.ViewModel;
using PCL.Navigation;

namespace PCL;

/// <summary>设置页面左侧导航栏的视图模型。</summary>
public class PageSetupLeftViewModel : ViewModelBase
{
    private static readonly PageDescriptor Page = PageCatalog.RequirePage(PageIds.Setup);

    public SubPageId PageId { get; set; } = Page.FirstVisibleSubPage();

    /// <summary>当前设置页可用的子页数量；少于 2 项时隐藏整个设置侧栏。</summary>
    public static int CountVisiblePages(bool forceShow)
    {
        return Page.VisibleSubPageCount(forceShow);
    }

    public static SubPageId FirstVisible()
    {
        return Page.FirstVisibleSubPage();
    }

    public static bool IsSubPageHidden(SubPageId subPage)
    {
        var descriptor = Page.FindSubPage(subPage);
        return descriptor is not null && !PageDescriptor.IsVisible(descriptor);
    }

    public async Task<bool> ConfirmResetAsync(SubPageId id)
    {
        var messageKey = Page.FindSubPage(id)?.ResetMessageKey;
        if (messageKey is null) return false;
        var result = await Dialog.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Setup.Left.Reset.Title"),
                Content = Lang.Text(messageKey),
                Buttons = DialogButtons.OkCancel,
                IsWarn = true
            });
        return result.IsConfirmed;
    }
}
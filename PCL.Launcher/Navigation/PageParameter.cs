using System.Text.Json.Nodes;
using PCL.Views.Pages;
using PCL.Views.Pages.Download;

namespace PCL.Navigation;

/// <summary>页面导航参数。仅在页面需要携带上下文时使用；没有参数时使用 <see langword="null" />。</summary>
public union PageParameter(
    McInstance,
    Comp.CompProject,
    CompDetailParameter,
    CompSearchContext,
    PageHelpDetail.HelpContent,
    JsonObject,
    string);
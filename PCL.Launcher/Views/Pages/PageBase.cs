using Avalonia.Controls;

namespace PCL.Views.Pages;

/// <summary>页面生命周期约定：路由在进入/离开页面时调用。</summary>
public interface IPageView
{
    void OnNavigatedTo(object? parameter);

    void OnNavigatedFrom();
}

/// <summary>
///     迁移页面的公共基类：把路由生命周期收口到一处，后续页面只需按需重写。
///     不强加数据上下文或视图模型，留给具体页面自行决定。
/// </summary>
public abstract class PageBase : UserControl, IPageView
{
    public virtual void OnNavigatedTo(object? parameter)
    {
    }

    public virtual void OnNavigatedFrom()
    {
    }
}
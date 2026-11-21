using PCL.Navigation;

namespace PCL.Views.Pages;

/// <summary>页面生命周期约定：路由在进入/离开页面时调用。</summary>
public interface IPageView
{
    void OnNavigatedTo(PageParameter? parameter);

    void OnNavigatedFrom();
}

/// <summary>左栏页面容器约定；路由不依赖具体的 MyPageLeft 类型。</summary>
public interface IPageLeftContainer
{
    void TriggerShowAnimation();

    void TriggerHideAnimation();
}

/// <summary>右栏页面容器约定；路由不依赖具体的 MyPageRight 类型。</summary>
public interface IPageRightContainer
{
    void PageOnEnter();

    void PageOnExit();

    void PageOnForceExit();
}
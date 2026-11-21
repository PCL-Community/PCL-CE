using Avalonia.Controls;

namespace PCL.Views.Pages.Launch;

/// <summary>启动页左栏内嵌的登录子视图类型；切换账号状态时由左栏调用 <see cref="Reload" /> 刷新数据。</summary>
public interface ILoginPage
{
    void Reload();
}

/// <summary>
///     登录子视图向启动页左栏回传的导航请求：返回上一级、要求切换新建档案的验证方式，或账号状态已变化需要重新判断子视图。
///     左栏负责真正的子视图切换，子视图只发起请求。
/// </summary>
public interface ILoginPage_Bindable
{
    event Action? BackRequested;

    event Action<GameLaunch.McLoginType>? LoginTypeRequested;

    event Action? RefreshRequested;
}

/// <summary>登录子视图公共基类：统一 <see cref="ILoginPage" /> 默认实现与导航事件。</summary>
public abstract class LoginPageBase : UserControl, ILoginPage, ILoginPage_Bindable
{
    /// <inheritdoc />
    public event Action? BackRequested;

    /// <inheritdoc />
    public event Action<GameLaunch.McLoginType>? LoginTypeRequested;

    /// <inheritdoc />
    public event Action? RefreshRequested;

    /// <inheritdoc />
    public virtual void Reload()
    {
    }

    protected void RequestBack() => BackRequested?.Invoke();

    protected void RequestLoginType(GameLaunch.McLoginType type) => LoginTypeRequested?.Invoke(type);

    protected void RequestRefresh() => RefreshRequested?.Invoke();
}

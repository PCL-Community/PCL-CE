namespace PCL;

/// <summary>页面被导航进入/离开时的回调。</summary>
public interface IPageView
{
    void OnNavigatedTo(object? parameter);
    void OnNavigatedFrom();
}

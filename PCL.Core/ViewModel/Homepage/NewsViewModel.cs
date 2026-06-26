using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Model.Tools.News;
using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Threading.Tasks;
using PCL.Core.IO.Net.Http;

namespace PCL.Core.ViewModel.Homepage;

public partial class NewsViewModel : ObservableObject
{
    private const string BaseApiUrl = "https://net-secondary.web.minecraft-services.net/api/v1.0/zh-cn/search";
    private const int PageSize = 24;
    private static readonly string[] AllowedNewsLinkHosts =
    [
        "minecraft.net",
        "minecraft-services.net",
        "microsoft.com"
    ];
    private int _currentPage = 1;

    public ObservableCollection<NewsItem> NewsItems { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public NewsViewModel()
    {
        _LoadDataCommand.Execute(null);
    }

    [RelayCommand]
#pragma warning disable IDE1006 // 命名样式
    private async Task _LoadDataAsync()
#pragma warning restore IDE1006 // 命名样式
    {
        if (IsLoading) return;

        IsLoading = true;

        try
        {
            var url = $"{BaseApiUrl}?pageSize={PageSize}&sortType=Recent&category=News&newsOnly=true&page={_currentPage}";
            using var resp = await HttpRequest
                .Create(url)
                .SendAsync();

            resp.EnsureSuccessStatusCode();
            var json = await resp.AsJsonAsync<ApiResponse>();

            if (json?.Result?.Results is not null)
            {
                foreach (var item in json.Result.Results)
                {
                    item.Description = WebUtility.HtmlDecode(item.Description);
                    NewsItems.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载错误: {ex.Message}";
            LogWrapper.Error(ex, "Minecrft 信息流主页加载失败");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
#pragma warning disable IDE1006 // 命名样式
    private void _OpenRead(string? url)
#pragma warning restore IDE1006 // 命名样式
    {
        if (!IsSafeNewsLink(url))
        {
            LogWrapper.Warn("Homepage", $"已拦截不安全的 Minecraft 信息流链接：{url ?? "<null>"}");
            return;
        }

        Basics.OpenPath(url!);
    }

    internal static bool IsSafeNewsLink(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = uri.IdnHost;
        foreach (var allowedHost in AllowedNewsLinkHosts)
        {
            if (host.Equals(allowedHost, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith($".{allowedHost}", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

}

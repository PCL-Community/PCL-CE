using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.IO.Net.Http.Client.Request;
using PCL.Core.Logging;
using PCL.Core.Model.Tools.News;
using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Threading.Tasks;

namespace PCL.Core.ViewModel.Homepage;

public partial class NewsViewModel : ObservableObject
{
    private const string BaseApiUrl = "https://net-secondary.web.minecraft-services.net/api/v1.0/zh-cn/search";
    private const int PageSize = 24;
    private int _currentPage = 1;

    public ObservableCollection<NewsItem> NewsItems { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public NewsViewModel()
    {
        LoadDataCommand.Execute(null);
    }

    [RelayCommand]
#pragma warning disable IDE1006 // 命名样式
    private async Task LoadDataAsync()
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

            if (json?.Result?.Results != null)
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
    private void OpenRead(string url)
#pragma warning restore IDE1006 // 命名样式
    {
        Basics.OpenPath(url);
    }

}

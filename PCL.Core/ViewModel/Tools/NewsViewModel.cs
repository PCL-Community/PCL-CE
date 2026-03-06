using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.IO.Net.Http.Client;
using PCL.Core.Model.Tools.News;
using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PCL.Core.ViewModel.Tools;

public partial class NewsViewModel : ObservableObject
{
    private const string BaseApiUrl = "https://net-secondary.web.minecraft-services.net/api/v1.0/zh-cn/search";
    private const int PageSize = 24;
    private int _currentPage = 1;

    // ObservableCollection 会自动通知 UI 更新
    public ObservableCollection<NewsItem> NewsItems { get; } = new();

    // 使用 [ObservableProperty] 自动生成 IsLoading 属性及其 OnPropertyChanged
    [ObservableProperty]
    private bool _isLoading;

    // 使用 [ObservableProperty] 自动生成 ErrorMessage 属性
    [ObservableProperty]
    private string? _errorMessage;

    // 构造函数可以留空，或在此立即加载第一页数据
    public NewsViewModel()
    {
        // 可以在构造时触发加载，但建议通过命令触发以避免构造中执行异步操作
        LoadDataCommand.Execute(null);
    }

    // 异步加载命令：方法名 LoadData，自动生成 ICommand 属性 LoadDataCommand
    [RelayCommand]
    public async Task LoadDataAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var url = $"{BaseApiUrl}?pageSize={PageSize}&sortType=Recent&category=News&newsOnly=true&page={_currentPage}";
            using var resp = await HttpRequestBuilder
                .Create(url)
                .SendAsync(true);
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
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"网络请求失败: {ex.Message}";
        }
        catch (JsonException ex)
        {
            ErrorMessage = $"数据解析失败: {ex.Message}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"未知错误: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

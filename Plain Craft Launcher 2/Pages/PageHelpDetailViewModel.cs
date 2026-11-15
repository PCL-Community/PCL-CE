using System;
using System.IO;
using System.Security;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>帮助详情页面的视图模型。</summary>
public partial class PageHelpDetailViewModel : ViewModelBase
{
    private PageHelpDetail.HelpContent? _content;

    internal PageHelpDetailViewModel(PageHelpDetail.HelpContent content) => _content = content;

    internal PageHelpDetail.HelpContent? Content
    {
        get => _content;
        private set
        {
            _content = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Title));
        }
    }

    public string Title => _content?.Title ?? string.Empty;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var sourcePath = _content?.SourcePath;
        if (sourcePath is null)
            return;
        await RunAsync(async ct =>
        {
            try
            {
                Content = await Task.Run(() => LoadContent(sourcePath), ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Common.Log(ex, "刷新帮助详情页失败", Common.LogLevel.Msgbox,
                    userSummary: Lang.Text("Event.Error.ExecutionFailed", EventType.OpenHelp, sourcePath));
                throw;
            }
        });
    }

    internal static PageHelpDetail.HelpContent LoadContent(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException("未找到帮助 JSON 文件", jsonPath);

        var json = Launcher.ArgumentReplace(File.ReadAllText(jsonPath), SecurityElement.Escape);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("Title", out var titleElement) ||
            titleElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(titleElement.GetString()))
            throw new ArgumentException("帮助 JSON 中未找到有效的 Title 项", nameof(jsonPath));

        var xamlPath = Path.ChangeExtension(jsonPath, ".xaml");
        if (!File.Exists(xamlPath))
            throw new FileNotFoundException("未找到帮助 JSON 对应的 XAML 文件", xamlPath);

        var xaml = File.ReadAllText(xamlPath);
        if (string.IsNullOrWhiteSpace(xaml))
            throw new InvalidDataException("帮助 XAML 文件为空");

        return new PageHelpDetail.HelpContent(jsonPath, titleElement.GetString()!, xaml);
    }
}

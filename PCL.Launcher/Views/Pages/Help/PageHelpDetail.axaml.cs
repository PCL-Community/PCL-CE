using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Secret;
using PCL.UI.Controls;
using PCL.Views.Pages.Homepage;

namespace PCL.Views.Pages;

/// <summary>
///     帮助详情页：展示帮助 JSON 的标题与正文；正文由 <see cref="HelpXamlRenderer" /> 转换，无法转换时退回纯文本。
/// </summary>
public partial class PageHelpDetail : MyPageRight, IRefreshable
{
    private HelpContent? _content;

    public PageHelpDetail()
    {
        InitializeComponent();
        PanScroll = PanBack;
    }

    public string Title => _content?.Title ?? string.Empty;

    public void Refresh()
    {
        if (_content?.SourcePath is not { } sourcePath || !File.Exists(sourcePath))
            return;
        ApplyFromPath(sourcePath);
    }

    public event Action<string>? TitleChanged;

    public override void OnNavigatedTo(object? parameter)
    {
        switch (parameter)
        {
            case HelpContent content:
                ApplyContent(content);
                break;
            case string sourcePath when File.Exists(sourcePath):
                ApplyFromPath(sourcePath);
                break;
        }

        PanBack.ScrollToHome();
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        Refresh();
    }

    private void ApplyFromPath(string sourcePath)
    {
        try
        {
            ApplyContent(LoadContent(sourcePath));
        }
        catch (Exception ex)
        {
            var summary = Lang.Text("Event.Error.ExecutionFailed", "OpenHelp", sourcePath);
            LauncherCommon.Log(
                ex,
                "刷新帮助详情页失败",
                LauncherLogLevel.Msgbox,
                userSummary: summary);
            HintService.Show(Hints.Error(summary));
            ShowPlainText(ex.Message);
        }
    }

    public static string ReadTitle(string jsonPath)
    {
        try
        {
            if (!File.Exists(jsonPath)) return "";
            var json = (JsonObject)LauncherCommon.GetJson(LauncherCommon.ReadFile(jsonPath));
            return ReplaceArguments(json["Title"]?.ToString()?.Trim() ?? "");
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "读取帮助标题失败");
            return "";
        }
    }

    public static HelpContent LoadContent(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException("未找到帮助 JSON 文件", jsonPath);

        var json = (JsonObject)LauncherCommon.GetJson(LauncherCommon.ReadFile(jsonPath));
        var title = json["Title"]?.ToString();
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("帮助 JSON 中未找到有效的 Title 项", nameof(jsonPath));
        title = ReplaceArguments(title);

        var xamlPath = Path.ChangeExtension(jsonPath, ".xaml");
        if (!File.Exists(xamlPath))
            throw new FileNotFoundException("未找到帮助 JSON 对应的 XAML 文件", xamlPath);
        var xaml = LauncherCommon.ReadFile(xamlPath);
        return string.IsNullOrWhiteSpace(xaml)
            ? throw new InvalidDataException("帮助 XAML 文件为空")
            : new HelpContent(jsonPath, title, xaml);
    }

    private void ApplyContent(HelpContent content)
    {
        _content = content;
        var rendered = HelpXamlRenderer.Render(
            ReplaceArguments(content.Xaml),
            ResolveBrush);
        if (rendered is not null)
        {
            PanCustom.Children.Clear();
            PanCustom.Children.Add(rendered);
        }
        else
        {
            ShowPlainText(_ExtractPlainText(ReplaceArguments(content.Xaml)));
        }

        TitleChanged?.Invoke(content.Title);
    }

    private void ShowPlainText(string text)
    {
        PanCustom.Children.Clear();
        var body = new SelectableTextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap
        };
        var card = new Border
        {
            Child = body,
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 15)
        };
        card.Classes.Add("Card");
        PanCustom.Children.Add(card);
    }

    private IBrush? ResolveBrush(string key)
    {
        return this.TryFindResource(key, out var resource) && resource is IBrush brush
            ? brush
            : null;
    }

    /// <summary>替换帮助文本中的启动器参数标记，与 WPF 的 ArgumentReplace 保持一致。</summary>
    private static string ReplaceArguments(string text)
    {
        text = text
            .Replace("{pcl_version}", LauncherCommon.VersionBaseName)
            .Replace(
                "{pcl_version_code}",
                LauncherCommon.VersionCode.ToString(CultureInfo.InvariantCulture))
            .Replace("{pcl_version_branch}", LauncherCommon.VersionBranchName)
            .Replace("{pcl_branch}", LauncherCommon.VersionBranchName)
            .Replace("{identify}", Identify.LauncherId)
            .Replace("{path}", Basics.ExecutableDirectory)
            .Replace("{path_with_name}", Basics.ExecutableName)
            .Replace("{path_temp}", Paths.Temp)
            .Replace("{date}", Lang.Date(DateTime.Now, "d"))
            .Replace("{time}", Lang.Date(DateTime.Now, "T"))
            .Replace("{java}", GameLaunch.mcLaunchJavaSelected?.Installation.JavaFolder ?? "")
            .Replace("{minecraft}", GameFolders.mcFolderSelected ?? "");

        var instance = InstanceList.McMcInstanceSelected;
        if (instance is null)
        {
            text = text
                .Replace("{version_path}", "")
                .Replace("{verpath}", "")
                .Replace("{version_indie}", "")
                .Replace("{verindie}", "")
                .Replace("{name}", "")
                .Replace("{version}", "");
        }
        else
        {
            text = text
                .Replace("{version_path}", instance.PathInstance)
                .Replace("{verpath}", instance.PathInstance)
                .Replace("{version_indie}", instance.PathIndie ?? "")
                .Replace("{verindie}", instance.PathIndie ?? "")
                .Replace("{name}", instance.Name);
            var vanillaName = instance.Info.VanillaName;
            var versionName = vanillaName is "unknown" or "old" or "pending"
                ? instance.Name
                : vanillaName;
            text = text.Replace("{version}", versionName);
        }

        var loginLoader = GameLaunch.mcLoginLoader;
        if (loginLoader.State == LoadState.Finished)
        {
            var login = loginLoader.output;
            text = text
                .Replace("{user}", login.Name ?? "")
                .Replace("{uuid}", login.Uuid?.ToLowerInvariant() ?? "");
            text = loginLoader.input?.LoginType switch
            {
                GameLaunch.McLoginType.Legacy => text.Replace(
                    "{login}",
                    Lang.Text("Launch.Account.Type.Offline")),
                GameLaunch.McLoginType.Ms => text.Replace(
                    "{login}",
                    Lang.Text("Launch.Account.Type.Microsoft")),
                GameLaunch.McLoginType.Auth => text.Replace("{login}", "Authlib-Injector"),
                _ => text.Replace("{login}", "")
            };
        }
        else
        {
            text = text
                .Replace("{user}", "")
                .Replace("{uuid}", "")
                .Replace("{login}", "");
        }

        text = Regex.Replace(
            text,
            @"\{setup:([a-zA-Z0-9]+)\}",
            match =>
            {
                if (!ConfigService.TryGetConfigItemNoType(match.Groups[1].Value, out var item))
                    return "";
                return item.GetValueNoType(instance?.PathInstance)?.ToString() ?? "";
            });
        text = Regex.Replace(
            text,
            @"\{(?:varible|variable):([^:\}]+)(?::([^\}]+))?\}",
            match => CustomEvent.GetCustomVariable(
                match.Groups[1].Value,
                match.Groups[2].Success ? match.Groups[2].Value : ""));
        text = text.Replace("{hint}", HomepageHints.GetRandomHint());
        return text;
    }

    private static string _ExtractPlainText(string xaml)
    {
        try
        {
            var document = XDocument.Parse(
                "<Root xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\""
                + " xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">"
                + xaml
                + "</Root>");
            var lines = document
                .DescendantNodes()
                .OfType<XText>()
                .Select(text => text.Value.Trim())
                .Where(text => text.Length > 0);
            return string.Join("\n", lines);
        }
        catch (Exception)
        {
            return xaml;
        }
    }

    /// <summary>帮助内容：来源 JSON 路径、标题与正文 XAML 片段。</summary>
    public sealed record HelpContent(
        string SourcePath,
        string Title,
        string Xaml);
}
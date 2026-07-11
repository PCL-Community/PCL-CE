// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Net.Http.Headers;
using System.Text.Json;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Instances.Views;

#pragma warning disable CA1822, CS0067

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupFeedback : MyPageRight, IRefreshableSettingsPage, ISettingsPageInteractionSource, IDisposable
{
    private const string IssuesApiBase = "https://api.github.com/repos/MuXue1230-owo/PCL-N/issues?state=all&sort=created&direction=desc&per_page=100&page=";
    private static readonly Uri NewIssueUri = new("https://github.com/MuXue1230-owo/PCL-N/issues/new/choose");

    private readonly List<FeedbackItem> _feedbackItems = [];
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public PageSetupFeedback()
        : this(new HttpClient(), ownsClient: true)
    {
    }

    public PageSetupFeedback(HttpClient client, bool ownsClient = false)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _ownsClient = ownsClient;
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
        if (this.FindControl<MyLoading>("Load") is { } load &&
            this.FindControl<MyCard>("PanLoad") is { } panLoad &&
            this.FindControl<StackPanel>("PanContent") is { } panContent &&
            this.FindControl<MyCard>("PanInfo") is { } panInfo)
        {
            PageLoaderInit(
                load,
                panLoad,
                panContent,
                panInfo,
                LoadFeedbackAsync,
                RenderFeedbackList);
        }
    }

    public int LoadedIssueCount => _feedbackItems.Count;

    public event EventHandler<SettingsPathRequestedEventArgs>? OpenPathRequested;

    public event EventHandler<SettingsUrlRequestedEventArgs>? OpenUrlRequested;

    public event EventHandler<SettingsMessageRequestedEventArgs>? MessageRequested;

    public event EventHandler<SettingsConfirmRequestedEventArgs>? ConfirmRequested;

    public void RefreshPage() => PageLoaderRestart();

    private async Task LoadFeedbackAsync(CancellationToken cancellationToken)
    {
        if (_client.DefaultRequestHeaders.UserAgent.Count == 0)
            _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PCL-N", "1.0"));
        if (_client.DefaultRequestHeaders.Accept.Count == 0)
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        _feedbackItems.Clear();
        HashSet<int> issueNumbers = [];
        for (int page = 1; page <= 100; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using HttpResponseMessage response = await _client.GetAsync(
                new Uri(IssuesApiBase + page.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(true);
            response.EnsureSuccessStatusCode();
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(true);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(true);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("GitHub Issues response is not an array.");

            int pageItemCount = 0;
            foreach (JsonElement issue in document.RootElement.EnumerateArray())
            {
                pageItemCount++;
                cancellationToken.ThrowIfCancellationRequested();
                if (issue.TryGetProperty("pull_request", out JsonElement pullRequest) &&
                    pullRequest.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
                {
                    continue;
                }

                FeedbackItem parsed = ParseIssue(issue);
                if (issueNumbers.Add(parsed.Number))
                    _feedbackItems.Add(parsed);
            }

            if (pageItemCount < 100)
                break;
        }
    }

    private void RenderFeedbackList()
    {
        StackPanel processing = RequirePanel("PanListProcessing");
        StackPanel waitingProcess = RequirePanel("PanListWaitingProcess");
        StackPanel wait = RequirePanel("PanListWait");
        StackPanel pause = RequirePanel("PanListPause");
        StackPanel upNext = RequirePanel("PanListUpnext");
        StackPanel completed = RequirePanel("PanListCompleted");
        StackPanel decline = RequirePanel("PanListDecline");
        StackPanel ignored = RequirePanel("PanListIgnored");
        StackPanel duplicate = RequirePanel("PanListDuplicate");
        ClearFeedbackPanels();
        foreach (FeedbackItem item in _feedbackItems)
        {
            (StackPanel Panel, string Icon) target = GetTargetPanel(
                item, processing, waitingProcess, wait, pause, upNext, completed, decline, ignored, duplicate);
            target.Panel.Children.Add(CreateFeedbackItem(item, target.Icon));
        }

        SetPanelVisibility(processing, RequireControl("PanContentProcessing"));
        SetPanelVisibility(waitingProcess, RequireControl("PanContentWaitingProcess"));
        SetPanelVisibility(wait, RequireControl("PanContentWait"));
        SetPanelVisibility(pause, RequireControl("PanContentPause"));
        SetPanelVisibility(upNext, RequireControl("PanContentUpnext"));
        SetPanelVisibility(completed, RequireControl("PanContentCompleted"));
        SetPanelVisibility(decline, RequireControl("PanContentDecline"));
        SetPanelVisibility(ignored, RequireControl("PanContentIgnored"));
        SetPanelVisibility(duplicate, RequireControl("PanContentDuplicate"));
        foreach (StackPanel panel in new[] { processing, waitingProcess, wait, pause, upNext, completed, decline, ignored, duplicate })
        {
            ControlVisualHelpers.AnimateListEntrance(panel, "Feedback List " + panel.Name);
        }
    }

    private void Feedback_Click(object? sender, EventArgs e)
    {
        OpenUrlRequested?.Invoke(this, new SettingsUrlRequestedEventArgs(NewIssueUri.ToString()));
    }

    private MyListItem CreateFeedbackItem(FeedbackItem item, string icon)
    {
        MyListItem listItem = new()
        {
            Title = item.Title,
            Info = $"#{item.Number} · {item.User} · {item.CreatedAt:yyyy-MM-dd}",
            Height = 45d,
            Type = MyListItem.CheckType.Clickable,
            Logo = InstanceDisplayHelper.BlockAssetRoot + icon,
            LogoScale = 0.85d,
            Tags = item.Type
        };
        listItem.Click += (_, _) => OpenUrlRequested?.Invoke(this, new SettingsUrlRequestedEventArgs(item.Url));
        return listItem;
    }

    private void ClearFeedbackPanels()
    {
        foreach (string name in new[]
                 {
                     "PanListProcessing", "PanListWaitingProcess", "PanListWait", "PanListPause", "PanListUpnext",
                     "PanListCompleted", "PanListDecline", "PanListIgnored", "PanListDuplicate"
                 })
        {
            RequirePanel(name).Children.Clear();
        }
    }

    private static void SetPanelVisibility(StackPanel panel, Control card) =>
        card.IsVisible = panel.Children.Count > 0;

    private static (StackPanel Panel, string Icon) GetTargetPanel(
        FeedbackItem item,
        StackPanel processing,
        StackPanel waitingProcess,
        StackPanel wait,
        StackPanel pause,
        StackPanel upNext,
        StackPanel completed,
        StackPanel decline,
        StackPanel ignored,
        StackPanel duplicate)
    {
        if (item.LabelNames.Contains("处理中", StringComparer.OrdinalIgnoreCase))
            return (processing, "CommandBlock.png");
        if (item.LabelNames.Contains("等待处理", StringComparer.OrdinalIgnoreCase))
            return (waitingProcess, "RedstoneBlock.png");
        if (item.LabelNames.Contains("推迟", StringComparer.OrdinalIgnoreCase) ||
            item.LabelNames.Contains("暂停", StringComparer.OrdinalIgnoreCase))
            return (pause, "RedstoneLampOff.png");
        if (item.LabelNames.Contains("即将处理", StringComparer.OrdinalIgnoreCase))
            return (upNext, "RedstoneLampOn.png");
        if (item.LabelNames.Contains("完成", StringComparer.OrdinalIgnoreCase) || item.State == "closed")
            return (completed, "Grass.png");
        if (item.LabelNames.Contains("作废", StringComparer.OrdinalIgnoreCase) ||
            item.LabelNames.Contains("拒绝", StringComparer.OrdinalIgnoreCase))
            return (decline, "CobbleStone.png");
        if (item.LabelNames.Contains("忽略", StringComparer.OrdinalIgnoreCase))
            return (ignored, "CobbleStone.png");
        if (item.LabelNames.Contains("重复", StringComparer.OrdinalIgnoreCase))
            return (duplicate, "CobbleStone.png");

        return (wait, "Anvil.png");
    }

    private StackPanel RequirePanel(string name) => this.FindControl<StackPanel>(name)
        ?? throw new InvalidOperationException("反馈页缺少列表控件：" + name);

    private Control RequireControl(string name) => this.FindControl<Control>(name)
        ?? throw new InvalidOperationException("反馈页缺少状态控件：" + name);

    private static FeedbackItem ParseIssue(JsonElement issue)
    {
        string type = "未分类";
        if (issue.TryGetProperty("type", out JsonElement typeElement) &&
            typeElement.ValueKind == JsonValueKind.Object &&
            typeElement.TryGetProperty("name", out JsonElement typeName))
        {
            type = typeName.GetString() ?? type;
        }

        List<string> labelIds = [];
        List<string> labelNames = [];
        if (issue.TryGetProperty("labels", out JsonElement labelArray) &&
            labelArray.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement label in labelArray.EnumerateArray())
            {
                if (label.TryGetProperty("id", out JsonElement id))
                    labelIds.Add(id.ToString());
                if (label.TryGetProperty("name", out JsonElement name) && name.GetString() is { Length: > 0 } labelName)
                    labelNames.Add(labelName);
            }
        }

        return new FeedbackItem(
            Number: issue.GetProperty("number").GetInt32(),
            Title: issue.GetProperty("title").GetString() ?? "未命名反馈",
            User: issue.GetProperty("user").GetProperty("login").GetString() ?? "unknown",
            Url: issue.GetProperty("html_url").GetString() ?? "https://github.com/MuXue1230-owo/PCL-N/issues",
            CreatedAt: issue.GetProperty("created_at").GetDateTimeOffset().ToLocalTime(),
            State: issue.GetProperty("state").GetString() ?? "open",
            Type: type,
            LabelIds: labelIds,
            LabelNames: labelNames);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (_ownsClient)
            _client.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record FeedbackItem(
        int Number,
        string Title,
        string User,
        string Url,
        DateTimeOffset CreatedAt,
        string State,
        string Type,
        IReadOnlyList<string> LabelIds,
        IReadOnlyList<string> LabelNames);
}

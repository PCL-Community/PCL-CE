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

public partial class PageSetupFeedback : MyPageRight, IRefreshableSettingsPage, ISettingsPageInteractionSource
{
    private static readonly Uri IssuesApiUri = new("https://api.github.com/repos/MuXue1230-owo/PCL-N/issues?state=all&sort=created&per_page=200");
    private static readonly Uri NewIssueUri = new("https://github.com/MuXue1230-owo/PCL-N/issues/new/choose");

    private readonly List<FeedbackItem> _feedbackItems = [];

    public PageSetupFeedback()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = PanBack;
        if (Load is not null && PanLoad is not null && PanContent is not null && PanInfo is not null)
        {
            PageLoaderInit(
                Load,
                PanLoad,
                PanContent,
                PanInfo,
                LoadFeedbackAsync,
                RenderFeedbackList);
        }
    }

    public event EventHandler<SettingsPathRequestedEventArgs>? OpenPathRequested;

    public event EventHandler<SettingsUrlRequestedEventArgs>? OpenUrlRequested;

    public event EventHandler<SettingsMessageRequestedEventArgs>? MessageRequested;

    public event EventHandler<SettingsConfirmRequestedEventArgs>? ConfirmRequested;

    public void RefreshPage() => PageLoaderRestart();

    private async Task LoadFeedbackAsync(CancellationToken cancellationToken)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PCL-N", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        await using Stream stream = await client.GetStreamAsync(IssuesApiUri, cancellationToken).ConfigureAwait(true);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(true);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("GitHub Issues response is not an array.");

        _feedbackItems.Clear();
        foreach (JsonElement issue in document.RootElement.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (issue.TryGetProperty("pull_request", out JsonElement pullRequest) &&
                pullRequest.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                continue;
            }

            _feedbackItems.Add(ParseIssue(issue));
        }
    }

    private void RenderFeedbackList()
    {
        ClearFeedbackPanels();
        foreach (FeedbackItem item in _feedbackItems)
        {
            (StackPanel Panel, string Icon) target = GetTargetPanel(item);
            target.Panel.Children.Add(CreateFeedbackItem(item, target.Icon));
        }

        SetPanelVisibility(PanListProcessing, PanContentProcessing);
        SetPanelVisibility(PanListWaitingProcess, PanContentWaitingProcess);
        SetPanelVisibility(PanListWait, PanContentWait);
        SetPanelVisibility(PanListPause, PanContentPause);
        SetPanelVisibility(PanListUpnext, PanContentUpnext);
        SetPanelVisibility(PanListCompleted, PanContentCompleted);
        SetPanelVisibility(PanListDecline, PanContentDecline);
        SetPanelVisibility(PanListIgnored, PanContentIgnored);
        SetPanelVisibility(PanListDuplicate, PanContentDuplicate);
        foreach (StackPanel panel in new[]
                 {
                     PanListProcessing, PanListWaitingProcess, PanListWait, PanListPause, PanListUpnext,
                     PanListCompleted, PanListDecline, PanListIgnored, PanListDuplicate
                 })
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
        PanListProcessing.Children.Clear();
        PanListWaitingProcess.Children.Clear();
        PanListWait.Children.Clear();
        PanListPause.Children.Clear();
        PanListUpnext.Children.Clear();
        PanListCompleted.Children.Clear();
        PanListDecline.Children.Clear();
        PanListIgnored.Children.Clear();
        PanListDuplicate.Children.Clear();
    }

    private static void SetPanelVisibility(StackPanel panel, Control card) =>
        card.IsVisible = panel.Children.Count > 0;

    private (StackPanel Panel, string Icon) GetTargetPanel(FeedbackItem item)
    {
        if (item.LabelIds.Contains("6820804544"))
            return (PanListProcessing, "CommandBlock.png");
        if (item.LabelIds.Contains("6820804546"))
            return (PanListWaitingProcess, "RedstoneBlock.png");
        if (item.LabelIds.Contains("8743070786"))
            return (PanListWait, "Anvil.png");
        if (item.LabelIds.Contains("8558220235"))
            return (PanListPause, "RedstoneLampOff.png");
        if (item.LabelIds.Contains("8550609020"))
            return (PanListUpnext, "RedstoneLampOn.png");
        if (item.LabelIds.Contains("6820804547") || item.State == "closed")
            return (PanListCompleted, "Grass.png");
        if (item.LabelIds.Contains("6820804539"))
            return (PanListDecline, "CobbleStone.png");
        if (item.LabelIds.Contains("8064650117"))
            return (PanListIgnored, "CobbleStone.png");
        if (item.LabelIds.Contains("6820804541"))
            return (PanListDuplicate, "CobbleStone.png");

        return (PanListWait, "Anvil.png");
    }

    private static FeedbackItem ParseIssue(JsonElement issue)
    {
        string type = "未分类";
        if (issue.TryGetProperty("type", out JsonElement typeElement) &&
            typeElement.ValueKind == JsonValueKind.Object &&
            typeElement.TryGetProperty("name", out JsonElement typeName))
        {
            type = typeName.GetString() ?? type;
        }

        List<string> labels = [];
        if (issue.TryGetProperty("labels", out JsonElement labelArray) &&
            labelArray.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement label in labelArray.EnumerateArray())
            {
                if (label.TryGetProperty("id", out JsonElement id))
                    labels.Add(id.ToString());
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
            LabelIds: labels);
    }

    private sealed record FeedbackItem(
        int Number,
        string Title,
        string User,
        string Url,
        DateTimeOffset CreatedAt,
        string State,
        string Type,
        IReadOnlyList<string> LabelIds);
}

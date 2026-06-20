// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using PCL.Application.Logging;
using PCL.Core.Logging;
using PCL.UI.Abstractions;

namespace PCL.Desktop.ViewModels.Log;

public sealed record LogLevelOption(
    string Name,
    PortableLogLevel MinimumLevel);

public sealed class LogPageViewModel : ObservableObject, IDisposable
{
    private const int MaximumVisibleLines = 1_000;
    private readonly IClipboardService _clipboardService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IHintService _hintService;
    private readonly ILauncherLogSource _logSource;
    private readonly IUiScheduler _scheduler;
    private readonly List<LauncherLogEntry> _allEntries = [];
    private readonly ObservableCollection<LogLineViewModel> _lines = [];
    private string _searchText = string.Empty;
    private LogLevelOption _selectedLevel;
    private bool _autoScroll = true;
    private bool _disposed;

    public LogPageViewModel(
        ILauncherLogSource logSource,
        IUiScheduler scheduler,
        IClipboardService clipboardService,
        IFileDialogService fileDialogService,
        IHintService hintService)
    {
        _logSource = logSource;
        _scheduler = scheduler;
        _clipboardService = clipboardService;
        _fileDialogService = fileDialogService;
        _hintService = hintService;
        LevelOptions =
        [
            new LogLevelOption("全部等级", PortableLogLevel.Trace),
            new LogLevelOption("调试及以上", PortableLogLevel.Debug),
            new LogLevelOption("信息及以上", PortableLogLevel.Info),
            new LogLevelOption("警告及以上", PortableLogLevel.Warn),
            new LogLevelOption("仅错误", PortableLogLevel.Error)
        ];
        _selectedLevel = LevelOptions[0];
        Lines = new ReadOnlyObservableCollection<LogLineViewModel>(_lines);
        CopyCommand = new AsyncDelegateCommand(CopyVisibleAsync);
        ExportCommand = new AsyncDelegateCommand(ExportVisibleAsync);
        ClearCommand = new DelegateCommand(Clear);

        _allEntries.AddRange(_logSource.GetSnapshot());
        RebuildVisibleLines();
        _logSource.EntryAdded += OnEntryAdded;
    }

    public IReadOnlyList<LogLevelOption> LevelOptions { get; }

    public ReadOnlyObservableCollection<LogLineViewModel> Lines { get; }

    public ICommand CopyCommand { get; }

    public ICommand ExportCommand { get; }

    public ICommand ClearCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
                return;
            RebuildVisibleLines();
        }
    }

    public LogLevelOption SelectedLevel
    {
        get => _selectedLevel;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!SetProperty(ref _selectedLevel, value))
                return;
            RebuildVisibleLines();
        }
    }

    public bool AutoScroll
    {
        get => _autoScroll;
        set => SetProperty(ref _autoScroll, value);
    }

    public bool IsEmpty => _lines.Count == 0;

    public string ResultSummary =>
        _lines.Count == _allEntries.Count
            ? $"共 {_lines.Count.ToString(CultureInfo.InvariantCulture)} 条日志"
            : $"显示 {_lines.Count.ToString(CultureInfo.InvariantCulture)} / {_allEntries.Count.ToString(CultureInfo.InvariantCulture)} 条";

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _logSource.EntryAdded -= OnEntryAdded;
    }

    private void OnEntryAdded(LauncherLogEntry entry)
    {
        _scheduler.Post(
            () =>
            {
                if (_disposed)
                    return;

                _allEntries.Add(entry);
                if (_allEntries.Count > MaximumVisibleLines * 2)
                    _allEntries.RemoveAt(0);

                if (Matches(entry))
                {
                    _lines.Add(new LogLineViewModel(entry));
                    if (_lines.Count > MaximumVisibleLines)
                        _lines.RemoveAt(0);
                }

                NotifySummaryChanged();
            });
    }

    private void RebuildVisibleLines()
    {
        _lines.Clear();
        foreach (LauncherLogEntry entry in _allEntries)
        {
            if (Matches(entry))
                _lines.Add(new LogLineViewModel(entry));
        }

        while (_lines.Count > MaximumVisibleLines)
            _lines.RemoveAt(0);

        NotifySummaryChanged();
    }

    private bool Matches(LauncherLogEntry entry)
    {
        if (entry.Level < SelectedLevel.MinimumLevel)
            return false;
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        return entry.Module.Contains(
                   SearchText,
                   StringComparison.OrdinalIgnoreCase) ||
               entry.Message.Contains(
                   SearchText,
                   StringComparison.OrdinalIgnoreCase) ||
               (entry.ExceptionText?.Contains(
                    SearchText,
                    StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private async Task CopyVisibleAsync()
    {
        if (_lines.Count == 0)
        {
            _hintService.ShowInfo("当前没有可复制的日志。");
            return;
        }

        try
        {
            await _clipboardService.SetTextAsync(BuildVisibleText());
            _hintService.ShowSuccess("已复制当前筛选结果。");
        }
        catch
        {
            _hintService.ShowError("复制失败，请检查系统剪贴板是否可用。");
        }
    }

    private async Task ExportVisibleAsync()
    {
        if (_lines.Count == 0)
        {
            _hintService.ShowInfo("当前没有可导出的日志。");
            return;
        }

        string fileName =
            $"PCL_N_Logs_{DateTime.Now:yyyyMMddHHmmss}.txt";
        string? path = await _fileDialogService.PickSaveFileAsync(
            "导出启动器日志",
            fileName,
            [new FileDialogFilter("文本日志", ["*.txt", "*.log"])]);
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            await File.WriteAllTextAsync(path, BuildVisibleText());
            _hintService.ShowSuccess("日志已导出到选定位置。");
        }
        catch
        {
            _hintService.ShowError("日志导出失败，请确认目标位置可写。");
        }
    }

    private void Clear()
    {
        _logSource.Clear();
        _allEntries.Clear();
        _lines.Clear();
        NotifySummaryChanged();
        _hintService.ShowSuccess("当前日志视图已清空。");
    }

    private string BuildVisibleText() =>
        string.Join(
            Environment.NewLine,
            _lines.Select(static line => line.DisplayText));

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ResultSummary));
    }
}

using System.Collections.ObjectModel;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;
using PCL.Views.Pages.Instance;

namespace PCL;

/// <summary>实例截图页的视图模型：维护文件集合、分页装填、复制/删除与刷新状态。</summary>
public partial class PageInstanceScreenshotViewModel : ViewModelBase
{
    private static readonly string[] AllowedSuffix =
        ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp", "*.tiff"];

    private readonly List<ScreenshotItemViewModel> _files = [];
    private bool _appendLock;
    private int _appendOffset;

    [ObservableProperty] public partial bool ShowContent { get; set; }

    [ObservableProperty] public partial bool IsRefreshing { get; set; }

    [ObservableProperty] public partial ScreenshotItemViewModel? SelectedScreenshot { get; set; }

    public ObservableCollection<ScreenshotItemViewModel> Screenshots { get; } = [];

    public string ScreenshotPath { get; private set; } = string.Empty;

    public bool HasScreenshots => _files.Count > 0;

    public IReadOnlyList<ScreenshotItemViewModel> AllScreenshots => _files;

    public bool CanAppend => !_appendLock && _appendOffset * 20 < _files.Count;

    /// <summary>绑定当前实例并确保截图目录存在。</summary>
    public void SetInstance(McInstance? instance)
    {
        ScreenshotPath = instance is null
            ? string.Empty
            : Path.Combine(instance.PathIndie, "screenshots");
        if (string.IsNullOrEmpty(ScreenshotPath))
            return;
        try
        {
            if (!Directory.Exists(ScreenshotPath))
                Directory.CreateDirectory(ScreenshotPath);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            InstancePageHelper.ReportError("Instance.Screenshot.OpenFailed", ex);
        }
    }

    /// <summary>重新扫描截图目录；返回是否存在截图。</summary>
    public bool Reload()
    {
        IsRefreshing = true;
        try
        {
            _files.Clear();
            Screenshots.Clear();
            _appendOffset = 0;
            _appendLock = false;
            SelectedScreenshot = null;
            Error = null;

            if (string.IsNullOrEmpty(ScreenshotPath) || !Directory.Exists(ScreenshotPath))
            {
                ShowContent = false;
                return false;
            }

            var candidates = AllowedSuffix
                .SelectMany(suffix => Directory.EnumerateFiles(
                    ScreenshotPath,
                    suffix,
                    SearchOption.TopDirectoryOnly))
                .OrderByDescending(File.GetCreationTime)
                .ToList();
            foreach (var path in candidates)
                try
                {
                    if (!File.Exists(path))
                        continue;
                    if (File.GetAttributes(path).HasFlag(FileAttributes.Hidden))
                        continue;
                    if (new FileInfo(path).Length == 0L)
                        continue;
                    _files.Add(
                        new ScreenshotItemViewModel(
                            path,
                            path.Replace(ScreenshotPath, string.Empty)));
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(ex, $"读取截图信息失败：{path}", LauncherLogLevel.Developer);
                }

            ShowContent = _files.Count > 0;
            State = AsyncState.Ready;
            return ShowContent;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            State = AsyncState.Failed;
            ShowContent = false;
            InstancePageHelper.ReportError("Instance.Screenshot.OpenFailed", ex);
            return false;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>从当前游标追加一批截图到可见集合。</summary>
    public IReadOnlyList<ScreenshotItemViewModel> AppendNext(int count)
    {
        if (_appendLock || count <= 0)
            return [];
        var start = _appendOffset * count;
        if (start >= _files.Count)
            return [];

        _appendLock = true;
        try
        {
            _appendOffset++;
            var result = new List<ScreenshotItemViewModel>();
            for (var i = start; i < Math.Min(start + count, _files.Count); i++)
            {
                var item = _files[i];
                Screenshots.Add(item);
                result.Add(item);
            }

            return result;
        }
        finally
        {
            _appendLock = false;
        }
    }

    /// <summary>打开截图所在文件夹。</summary>
    public void OpenFolder()
    {
        if (string.IsNullOrEmpty(ScreenshotPath))
            return;
        try
        {
            if (!Directory.Exists(ScreenshotPath))
                Directory.CreateDirectory(ScreenshotPath);
            InstancePageHelper.OpenFolder(ScreenshotPath);
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Screenshot.OpenFailed", ex);
        }
    }

    public static void OpenScreenshot(ScreenshotItemViewModel item)
    {
        try
        {
            Basics.OpenPath(item.FilePath);
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Screenshot.OpenFailed", ex);
        }
    }

    /// <summary>复制图片位图到剪贴板；剪贴板由页面生命周期提供。</summary>
    public static async Task<bool> CopyAsync(ScreenshotItemViewModel item, IClipboard? clipboard)
    {
        if (!File.Exists(item.FilePath))
        {
            HintService.Show(Hints.Info(Lang.Text("Instance.Screenshot.FileNotFound")));
            return false;
        }

        if (clipboard is null)
            return false;

        Bitmap? bitmap = null;
        try
        {
            for (var attempt = 0; attempt < 6; attempt++)
                try
                {
                    bitmap?.Dispose();
                    await using var stream = File.OpenRead(item.FilePath);
                    bitmap = new Bitmap(stream);
                    await clipboard.SetBitmapAsync(bitmap);
                    HintService.Show(
                        Hints.Success(Lang.Text("Instance.Screenshot.CopiedToClipboard")));
                    return true;
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(
                        ex,
                        $"第 {attempt + 1} 次复制截图到剪贴板失败",
                        LauncherLogLevel.Developer);
                    await Task.Delay(30);
                }

            HintService.Show(Hints.Error(Lang.Text("Instance.Screenshot.CopyFailed")));
            return false;
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Screenshot.CopyFailed", ex);
            return false;
        }
    }

    /// <summary>删除截图（优先回收站）；返回是否从集合中移除。</summary>
    public Task<bool> DeleteAsync(ScreenshotItemViewModel item)
    {
        try
        {
            // 与 WPF 一致：截图删除不再二次确认；优先送入回收站。
            var canRecycle = RecycleBin.IsSupported(item.FilePath);
            var movedToRecycle = false;
            if (canRecycle)
            {
                var outcome = RecycleBin.Move(item.FilePath, out var error);
                switch (outcome)
                {
                    case RecycleBin.Outcome.Failed:
                        LauncherCommon.Log($"删除截图失败：{error}", LauncherLogLevel.Feedback);
                        HintService.Show(
                            Hints.Error(Lang.Text("Instance.Screenshot.DeleteFailed")));
                        return Task.FromResult(false);

                    case RecycleBin.Outcome.Unsupported:
                        canRecycle = false;
                        break;

                    case RecycleBin.Outcome.Moved:
                        movedToRecycle = true;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (!canRecycle)
                File.Delete(item.FilePath);

            _files.Remove(item);
            Screenshots.Remove(item);
            if (ReferenceEquals(SelectedScreenshot, item))
                SelectedScreenshot = null;
            ShowContent = _files.Count > 0;
            HintService.Show(
                Hints.Info(
                    movedToRecycle
                        ? Lang.Text("Instance.Saves.DeletedToRecycleBin")
                        : Lang.Text("Instance.Saves.DeletedPermanent")));
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Screenshot.DeleteFailed", ex);
            return Task.FromResult(false);
        }
    }

    public void Refresh()
    {
        HintService.Show(Hints.Info(Lang.Text("Instance.Saves.Status.Refreshing"), false));
        Reload();
    }
}
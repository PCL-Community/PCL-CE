using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;
using PCL.UI.Controls;

namespace PCL;

/// <summary>皮肤头像控件的视图模型：纹理加载、保存、刷新与披风菜单状态。</summary>
public partial class MySkinViewModel : ViewModelBase
{
    private int _loadRevision;

    [ObservableProperty] public partial string Address { get; set; } = string.Empty;

    [ObservableProperty] public partial bool CanSave { get; set; }

    [ObservableProperty] public partial bool HasCape { get; set; } = true;

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ToolTipText { get; set; } = Lang.Text("Launch.Skin.Change.ToolTip");

    [ObservableProperty] public partial CroppedBitmap? FaceImage { get; set; }

    [ObservableProperty] public partial CroppedBitmap? HatImage { get; set; }

    /// <summary>请求上层打开更换披风流程。</summary>
    public event Action? CapeRequested;

    partial void OnAddressChanged(string value)
    {
        var address = value ?? string.Empty;
        ToolTipText = string.IsNullOrEmpty(address)
            ? Lang.Text("Common.State.Loading")
            : Lang.Text("Launch.Skin.Change.ToolTip");
        CanSave = !string.IsNullOrWhiteSpace(address);
        _ = LoadAsync();
    }

    /// <summary>清空纹理并取消正在进行的加载。</summary>
    public void Clear()
    {
        _loadRevision++;
        Address = "";
        FaceImage = null;
        HatImage = null;
        IsLoading = false;
    }

    /// <summary>重新下载或读取当前地址的纹理。</summary>
    public void RefreshSource()
    {
        FaceImage = null;
        HatImage = null;
        _ = LoadAsync();
    }

    /// <summary>把当前皮肤保存到用户选择的位置。</summary>
    [RelayCommand]
    public async Task SaveSkinAsync()
    {
        var address = Address;
        if (string.IsNullOrWhiteSpace(address))
        {
            HintService.Show(Hints.Error(Lang.Text("Launch.Skin.Fetching")));
            return;
        }

        try
        {
            var localPath = address;
            if (address.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                localPath = await MyImage.DownloadImageAsync(address).ConfigureAwait(false);
            if (string.IsNullOrEmpty(localPath))
                throw new InvalidDataException("皮肤文件不存在");

            var target = SystemDialogs.SelectSaveFile(
                Lang.Text("Launch.Skin.SaveDialog.Title"),
                Path.GetFileName(
                    address.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        ? address
                        : "Skin.png"),
                Lang.Text("Launch.Skin.SaveDialog.Filter"));
            if (string.IsNullOrWhiteSpace(target))
                return;

            await Task.Run(() =>
            {
                using var source = OpenStream(localPath);
                if (source is null)
                    throw new FileNotFoundException("皮肤文件不存在", localPath);
                using var destination = File.Create(target);
                source.CopyTo(destination);
            });
            HintService.Show(Hints.Success(Lang.Text("Launch.Skin.SaveAs")));
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Launch", "保存皮肤失败");
            HintService.Show(Hints.Error(Lang.Text("Launch.Skin.File.Error")));
        }
    }

    [RelayCommand]
    private void ChangeCape()
    {
        CapeRequested?.Invoke();
    }

    public static string GetCapeDisplayName(string capeAlias)
    {
        var safeName = capeAlias.Replace("-", "").Replace(" ", "").Replace("'", "");
        var key = $"Launch.Skin.Cape.Name.{safeName}";
        var name = Lang.Text(key);
        return string.IsNullOrEmpty(name) || name == $"!{key}!" || name == key ? capeAlias : name;
    }

    private async Task LoadAsync()
    {
        var revision = ++_loadRevision;
        var address = Address;
        if (string.IsNullOrWhiteSpace(address))
        {
            FaceImage = null;
            HatImage = null;
            IsLoading = false;
            return;
        }

        IsLoading = true;
        try
        {
            var localPath = address;
            if (address.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                localPath = await MyImage.DownloadImageAsync(address).ConfigureAwait(false);
                if (string.IsNullOrEmpty(localPath))
                    throw new FileNotFoundException("皮肤下载失败", address);
            }

            await using var stream = OpenStream(localPath);
            if (stream is null)
                throw new FileNotFoundException("皮肤文件不存在", localPath);
            var bitmap = new Bitmap(stream);
            if (revision != _loadRevision)
                return;

            if (bitmap.PixelSize.Width < 32 || bitmap.PixelSize.Height < 32)
                throw new InvalidDataException(
                    $"图片大小不足，宽 {bitmap.PixelSize.Width}，高 {bitmap.PixelSize.Height}");

            var scale = Math.Max(1, (int)Math.Round(bitmap.PixelSize.Width / 64d));
            var face = Crop(bitmap, scale * 8, scale * 8);
            var hat = bitmap.PixelSize is { Width: >= 64, Height: >= 32 }
                ? Crop(bitmap, scale * 40, scale * 8)
                : null;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (revision != _loadRevision)
                    return;
                FaceImage = face;
                HatImage = hat;
                IsLoading = false;
            });
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Launch", $"加载皮肤失败：{address}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (revision != _loadRevision)
                    return;
                FaceImage = null;
                HatImage = null;
                IsLoading = false;
            });
        }
    }

    private static Stream? OpenStream(string path)
    {
        return path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)
            ? AssetLoader.Exists(new Uri(path))
                ? AssetLoader.Open(new Uri(path))
                : null
            : File.Exists(path)
                ? File.OpenRead(path)
                : null;
    }

    private static CroppedBitmap Crop(Bitmap source, int x, int y)
    {
        var size = Math.Max(1, source.PixelSize.Width / 8);
        var rect = new PixelRect(x, y, size, size);
        return new CroppedBitmap(source, rect);
    }
}
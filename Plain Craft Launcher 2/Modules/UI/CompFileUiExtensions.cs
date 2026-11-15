using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PCL.Core.App.Localization;

namespace PCL;

/// <summary>Comp 文件项的 WPF 控件构造，保留在 UI 程序集。</summary>
public static class CompFileUiExtensions
{
    extension(Comp.CompFile file)
    {
        public MyVirtualizingElement<MyListItem> ToListItem(
            MyListItem.ClickEventHandler onClick,
            MyIconButton.ClickEventHandler? onSaveClick = null,
            bool badDisplayName = false)
        {
            return new MyVirtualizingElement<MyListItem>(() =>
                file.CreateListItem(onClick, onSaveClick, badDisplayName))
            {
                Height = 42
            };
        }

        private MyListItem CreateListItem(
            MyListItem.ClickEventHandler onClick,
            MyIconButton.ClickEventHandler? onSaveClick,
            bool badDisplayName)
        {
            var title = badDisplayName ? file.FileName : file.DisplayName;
            var item = new MyListItem
            {
                Title = title,
                SnapsToDevicePixels = true,
                Height = 42,
                Type = MyListItem.CheckType.Clickable,
                Tag = file,
                Logo = file.GetStatusLogo()
            };
            LocalizedText.TryBind(item, "Info", () => file.BuildInfoText(title));
            item.Click += onClick;

            if (onSaveClick is not null)
                item.Buttons = [CreateSaveButton(onSaveClick)];

            return item;
        }

        private List<string> BuildListItemInfo(string title)
        {
            var info = new List<string>();
            var fileNameWithoutExtension = file.FileName.BeforeLast(".");

            if (title != fileNameWithoutExtension)
                info.Add(fileNameWithoutExtension);

            if (file.Dependencies.Count != 0)
                info.Add(
                    Lang.Text(
                        "Download.Comp.Detail.FileList.DependencyCount",
                        file.Dependencies.Count));

            var snapshotKeywords = new[]
            {
                "w",
                "snapshot",
                "rc",
                "pre",
                "experimental",
                "-"
            };

            if (file.GameVersions.All(version =>
                    !version.Contains('.')
                    || snapshotKeywords.Any(keyword => version.ContainsF(keyword, true))))
                info.Add(
                    Lang.Text(
                        "Download.Comp.Detail.FileList.GameVersion",
                        string.Join("、", file.GameVersions.Select(Comp.FormatGameVersion))));

            if (file.DownloadCount > 0)
                info.Add(
                    Lang.Text(
                        "Common.Format.DownloadCount",
                        Lang.CompactNumber(file.DownloadCount)));

            info.Add(
                Lang.Text(
                    "Download.Comp.Detail.FileList.Updated",
                    Lang.TimeSpan(file.ReleaseDate - DateTime.Now)));

            if (file.Status != Comp.CompFileStatus.Release)
                info.Add(file.StatusDescription);

            return info;
        }

        private string BuildInfoText(string title)
        {
            return string.Join("  |  ", file.BuildListItemInfo(title));
        }

        private string GetStatusLogo()
        {
            return file.Status switch
            {
                Comp.CompFileStatus.Release => LauncherPaths.ImageRoot + "Icons/R.png",
                Comp.CompFileStatus.Beta => LauncherPaths.ImageRoot + "Icons/B.png",
                _ => LauncherPaths.ImageRoot + "Icons/A.png"
            };
        }
    }

    private static MyIconButton CreateSaveButton(MyIconButton.ClickEventHandler onSaveClick)
    {
        var button = new MyIconButton
        {
            SvgIcon = "lucide/save"
        };
        button.SetLocalizedValue(
            FrameworkElement.ToolTipProperty,
            "Download.Version.SaveAs");

        ToolTipService.SetPlacement(button, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(button, 30);
        ToolTipService.SetHorizontalOffset(button, 2);
        button.Click += onSaveClick;

        return button;
    }
}
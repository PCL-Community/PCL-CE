using System.Windows;
using System.Windows.Controls;
using PCL.Core.App.Localization;

namespace PCL;

/// <summary>Comp 工程项的 WPF 控件构造，保留在 UI 程序集。</summary>
public static class CompProjectUiExtensions
{
    extension(Comp.CompProject project)
    {
        public MyVirtualizingElement<MyCompItem> ToCompItem(
            bool showMcVersionDesc,
            bool showLoaderDesc,
            bool showQuickDownload = false)
        {
            return new MyVirtualizingElement<MyCompItem>(() =>
            {
                var newItem = new MyCompItem
                {
                    Tag = project
                };
                project.ApplyLogoToMyImage(newItem.PathLogo);

                var title = project.GetControlTitle(true);
                newItem.Title = title.Key;

                if (string.IsNullOrEmpty(title.Value))
                    ((StackPanel)newItem.LabTitleRaw.Parent).Children.Remove(
                        newItem.LabTitleRaw);
                else
                    newItem.SubTitle = title.Value;

                newItem.Tags = project.Tags;
                newItem.Description = project.Description.Replace("\r", "").Replace("\n", "");
                newItem.ShowDownloadBtn = showQuickDownload;

                LocalizedText.TryBind(
                    newItem.LabVersion,
                    "Text",
                    () => project.BuildVersionDescription(showMcVersionDesc, showLoaderDesc));

                if (!showMcVersionDesc && !showLoaderDesc)
                {
                    ((Grid)newItem.SvgIconVersion.Parent).Children.Remove(
                        newItem.SvgIconVersion);
                    ((Grid)newItem.LabVersion.Parent).Children.Remove(newItem.LabVersion);
                    newItem.ColumnVersion1.Width = new GridLength(0);
                    newItem.ColumnVersion2.MaxWidth = 0;
                    newItem.ColumnVersion3.Width = new GridLength(0);
                }

                newItem.LabSource.Text = project.FromCurseForge ? "CurseForge" : "Modrinth";

                if (project.LastUpdate is not null)
                {
                    var lastUpdate = project.LastUpdate.Value;
                    LocalizedText.TryBind(
                        newItem.LabTime,
                        "Text",
                        () => Lang.TimeSpan(lastUpdate - DateTime.Now, 1));
                }
                else
                {
                    newItem.LabTime.Visibility = Visibility.Collapsed;
                    newItem.ColumnTime1.Width =
                        newItem.ColumnTime2.Width =
                            newItem.ColumnTime3.Width = new GridLength(0);
                }

                LocalizedText.TryBind(
                    newItem.LabDownload,
                    "Text",
                    () => Lang.CompactNumber(project.DownloadCount));

                return newItem;
            })
            {
                Height = 64
            };
        }

        public MyListItem ToListItem()
        {
            return new MyListItem
            {
                Title = project.TranslatedName,
                Info = project
                    .Description
                    .Replace("\r", "")
                    .Replace("\n", ""),
                Logo = string.IsNullOrEmpty(project.LogoUrl)
                    ? $"{LauncherPaths.ImageRoot}Icons/NoIcon.png"
                    : project.LogoUrl,
                Tags = project.Tags,
                Tag = project,
                LogoCornerRadius = new CornerRadius(6)
            };
        }

        public void ApplyLogoToMyImage(MyImage img)
        {
            if (string.IsNullOrEmpty(project.LogoUrl))
            {
                img.Source = $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";
            }
            else
            {
                img.Source = project.LogoUrl;
                img.FallbackSource = GameDownload.DlSourceModGet(project.LogoUrl);
            }
        }
    }
}
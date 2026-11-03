using PCL.Core.App.Localization;
using System.Windows;

namespace PCL;

/// <summary>Comp 工程项的 WPF 控件构造，保留在 UI 程序集。</summary>
public static class CompProjectUiExtensions
{
    extension(Comp.CompProject project)
    {
        /// <summary>
        ///     将当前工程信息实例化为条目 ViewModel，供虚拟化列表（ItemsControl + DataTemplate）使用。
        /// </summary>
        public MyCompItemViewModel ToCompItemViewModel(
            bool showMcVersionDesc,
            bool showLoaderDesc,
            bool showQuickDownload = false)
        {
            var title = project.GetControlTitle(true);
            var hasSubtitle = !string.IsNullOrEmpty(title.Value);

            var viewModel = new MyCompItemViewModel
            {
                Project = project,
                TitleText = title.Key,
                SubTitleText = hasSubtitle ? title.Value : "",
                SubTitleVisibility = hasSubtitle ? Visibility.Visible : Visibility.Collapsed,
                DescriptionText = project.Description.Replace("\r", "").Replace("\n", ""),
                Tags = project.Tags,
                SourceText = project.FromCurseForge ? "CurseForge" : "Modrinth",
                ShowVersion = showMcVersionDesc || showLoaderDesc,
                ShowTime = project.LastUpdate is not null,
                ShowDownloadButton = showQuickDownload,
                VersionText = LocalizedText.Create(
                    () => project.BuildVersionDescription(showMcVersionDesc, showLoaderDesc)),
                DownloadText = LocalizedText.Create(() => Lang.CompactNumber(project.DownloadCount))
            };

            if (project.LastUpdate is { } lastUpdate)
                viewModel.TimeText = LocalizedText.Create(() => Lang.TimeSpan(lastUpdate - DateTime.Now, 1));

            return viewModel;
        }

        public MyVirtualizingElement<MyCompItem> ToCompItem(
            bool showMcVersionDesc,
            bool showLoaderDesc,
            bool showQuickDownload = false)
        {
            return new MyVirtualizingElement<MyCompItem>(
                () => new MyCompItem
                {
                    ViewModel = project.ToCompItemViewModel(showMcVersionDesc, showLoaderDesc, showQuickDownload)
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

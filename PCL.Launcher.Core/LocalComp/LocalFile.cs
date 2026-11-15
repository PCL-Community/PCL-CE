using static PCL.Comp;

namespace PCL;

public static partial class LocalComp
{
    public partial class LocalCompFile
    {
        /// <summary>
        ///     根据完整文件路径的文件扩展名判断是否为 Mod 文件。
        /// </summary>
        public static bool IsModFile(string? path)
        {
            if (path is null || !path.Contains('.'))
                return false;

            return path.EndsWithF(".jar", true)
                || path.EndsWithF(".zip", true)
                || path.EndsWithF(".litemod", true)
                || path.EndsWithF(".jar.disabled", true)
                || path.EndsWithF(".zip.disabled", true)
                || path.EndsWithF(".litemod.disabled", true)
                || path.EndsWithF(".jar.old", true)
                || path.EndsWithF(".zip.old", true)
                || path.EndsWithF(".litemod.old", true);
        }

        /// <summary>
        ///     检查是否为指定类型的组件文件。
        /// </summary>
        public static bool IsCompFile(string? path, CompType compType)
        {
            if (path is null || !path.Contains('.'))
                return false;

            return compType switch
            {
                CompType.Mod =>
                    IsModFile(path),

                CompType.ResourcePack or CompType.Shader =>
                    path.EndsWithF(".zip", true),

                CompType.DataPack =>
                    path.EndsWithF(".zip", true)
                    || path.EndsWithF(".zip.disabled", true),

                CompType.Schematic =>
                    path.EndsWithF(".litematic", true)
                    || path.EndsWithF(".nbt", true)
                    || path.EndsWithF(".schematic", true)
                    || path.EndsWithF(".schem", true),

                _ => false
            };
        }

        /// <summary>
        ///     获取图标路径。
        /// </summary>
        public string GetLogo()
        {
            if (Comp?.LogoUrl != null)
                return Comp.LogoUrl;
            if (Logo is not null)
                return Logo;

            return IsFolder
                ? "pack://application:,,,/images/Icons/Folder.png"
                : $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";
        }
    }
}
namespace PCL;

public static partial class Comp
{
    public partial class CompProject
    {
        /// <summary>
        ///     CurseForge 文件列表的数字 ID。Modrinth 工程的此项无效。
        /// </summary>
        public readonly List<int> CurseForgeFileIds;

        /// <summary>
        ///     英文描述。
        /// </summary>
        public readonly string Description;

        /// <summary>
        ///     下载量计数。注意，该计数仅为一个来源，无法反应两边加起来的下载量！
        /// </summary>
        public readonly int DownloadCount;

        /// <summary>
        ///     支持的 Drop 编号，从高到低排序，不为 Nothing。
        ///     例如：261（26.1.x）、180（1.18.x）。
        /// </summary>
        public readonly List<int> Drops;

        /// <summary>
        ///     该工程信息来自 CurseForge 还是 Modrinth。
        /// </summary>
        public readonly bool FromCurseForge;

        /// <summary>
        ///     CurseForge 工程的数字 ID。Modrinth 工程的乱码 ID。
        /// </summary>
        public readonly string Id;

        /// <summary>
        ///     最后一次更新的时间。可能为 Nothing。
        /// </summary>
        public readonly DateTime? LastUpdate;

        /// <summary>
        ///     支持的 Mod 加载器列表。可能为空。
        /// </summary>
        public readonly List<CompLoaderType> ModLoaders;

        /// <summary>
        ///     原始的英文名称。
        /// </summary>
        public readonly string RawName;

        /// <summary>
        ///     工程的短名。例如 technical-enchant。
        /// </summary>
        public readonly string Slug;

        /// <summary>
        ///     描述性标签的资源键列表，未翻译。显示时由标签面板按当前语言实时求值。
        /// </summary>
        public readonly List<string> Tags;

        /// <summary>
        ///     工程的种类。
        ///     由于 Modrinth 混合使用 Mod 和数据包，结果不一定准确。
        /// </summary>
        public readonly CompType Type;

        /// <summary>
        ///     来源网站的工程页面网址。确保格式一定标准。
        ///     CurseForge：https://www.curseforge.com/minecraft/mc-mods/jei
        ///     Modrinth：https://modrinth.com/mod/technical-enchant
        /// </summary>
        public readonly string Website;

        /// <summary>
        ///     Logo 图片的下载地址。
        ///     若为 Nothing 则没有，保证不为空字符串。
        /// </summary>
        public string? LogoUrl;

        private bool _loadedDatabase;

        /// <summary>
        ///     从工程 Json 中初始化实例。若出错会抛出异常。
        /// </summary>
        public CompProject(JsonObject data)
        {
            var result = data.ContainsKey("Tags")
                ? _BuildFromCompJson(data)
                : data.ContainsKey("summary")
                    ? _BuildFromCurseForge(data)
                    : _BuildFromModrinth(data);

            if (!data.ContainsKey("Tags"))
            {
                if (result.Tags.Count == 0)
                    result.Tags.Add("Download.Comp.Category.Other");

                result.Tags = [.. result.Tags.Distinct()];

                result.ModLoaders =
                [
                    .. result
                        .ModLoaders
                        .Distinct()
                        .OrderBy(t => t)
                ];
            }

            FromCurseForge = result.FromCurseForge;
            Type = result.Type;
            Slug = result.Slug;
            Id = result.Id;
            CurseForgeFileIds = result.CurseForgeFileIds;
            RawName = result.RawName;
            Description = result.Description;
            Website = result.Website;
            LastUpdate = result.LastUpdate;
            DownloadCount = result.DownloadCount;
            ModLoaders = result.ModLoaders;
            Tags = result.Tags;
            LogoUrl = result.LogoUrl;
            Drops = result.Drops;

            compProjectCache[Id] = this;
        }
    }
}

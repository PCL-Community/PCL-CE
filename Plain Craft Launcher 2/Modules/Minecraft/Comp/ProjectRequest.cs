using System.Net;
using System.Text;

namespace PCL;

public static partial class Comp
{
    public class CompProjectRequest
    {
        /// <summary>
        ///     在进行中文搜索时，CurseForge 的替代搜索文本。
        ///     由于 CurseForge API 在有任意关键词未匹配的时候就不显示结果，所以不能使用与 Modrinth 相同的算法。
        /// </summary>
        public string? curseForgeAltSearchText;

        /// <summary>
        ///     筛选 MC 版本。
        /// </summary>
        public string? gameVersion = null;

        /// <summary>
        ///     筛选 Mod 加载器类别。
        /// </summary>
        public CompLoaderType modLoader = CompLoaderType.Any;

        /// <summary>
        ///     搜索的文本内容。
        /// </summary>
        public string searchText = "";

        /// <summary>
        ///     搜索结果排序方式。
        /// </summary>
        public CompSortType sort = CompSortType.Default;

        /// <summary>
        ///     允许的来源。
        /// </summary>
        public CompSourceType source = CompSourceType.Any;

        /// <summary>
        ///     加载后应输出到的结果存储器。
        /// </summary>
        public CompProjectStorage storage;

        /// <summary>
        ///     筛选资源标签。空字符串代表不限制。格式例如 "406/worldgen"，分别是 CurseForge 和 Modrinth 的 ID。
        /// </summary>
        public string tag = "";

        /// <summary>
        ///     应当尽量达成的结果数量。
        /// </summary>
        public int targetResultCount;

        /// <summary>
        ///     筛选资源种类。
        /// </summary>
        public CompType type;

        /// <summary>
        ///     构造函数。
        /// </summary>
        public CompProjectRequest(
            CompType type,
            CompProjectStorage storage,
            int targetResultCount)
        {
            this.type = type;
            this.storage = storage;
            this.targetResultCount = targetResultCount;
        }

        /// <summary>
        ///     根据加载位置记录，是否还可以继续获取内容。
        /// </summary>
        public bool CanContinue
        {
            get
            {
                if (tag.StartsWithF("/") || !source.HasFlag(CompSourceType.CurseForge))
                    storage.curseForgeTotal = 0;
                if (tag.EndsWithF("/") || !source.HasFlag(CompSourceType.Modrinth))
                    storage.modrinthTotal = 0;

                return storage.curseForgeTotal == -1
                    || storage.modrinthTotal == -1
                    || storage.curseForgeOffset < storage.curseForgeTotal
                    || storage.modrinthOffset < storage.modrinthTotal;
            }
        }

        /// <summary>
        ///     获取对应的 CurseForge API 请求链接。若返回 Nothing 则为不进行 CurseForge 请求。
        /// </summary>
        public string? GetCurseForgeAddress()
        {
            if (!source.HasFlag(CompSourceType.CurseForge))
                return null;

            if (tag.StartsWithF("/"))
                storage.curseForgeTotal = 0;

            if (storage.curseForgeTotal > -1 && storage.curseForgeTotal <= storage.curseForgeOffset)
                return null;

            var address = new StringBuilder(
                $"https://api.curseforge.com/v1/mods/search?gameId=432&sortOrder=desc&pageSize={CompPageSize}");

            var classId = type switch
            {
                CompType.Mod => "6",
                CompType.ModPack => "4471",
                CompType.DataPack => "6945",
                CompType.Shader => "6552",
                CompType.ResourcePack => "12",
                CompType.World => "17",
                _ => null
            };
            if (classId is not null)
                address.Append($"&classId={classId}");

            if (!string.IsNullOrEmpty(tag))
                address.Append($"&categoryId={tag.BeforeFirst("/")}");
            if (modLoader != CompLoaderType.Any)
                address.Append("&modLoaderType=").Append((int)modLoader);
            if (!string.IsNullOrEmpty(gameVersion))
                address.Append("&gameVersion=").Append(gameVersion);

            var searchFilter = curseForgeAltSearchText ?? searchText;
            if (!string.IsNullOrEmpty(searchFilter))
                address.Append("&searchFilter=").Append(WebUtility.UrlEncode(searchFilter));

            if (storage.curseForgeOffset > 0)
                address.Append("&index=").Append(storage.curseForgeOffset);

            var sortField = sort switch
            {
                CompSortType.Relevance => "4",
                CompSortType.Downloads => "6",
                CompSortType.Follows => "2",
                CompSortType.Newest => "11",
                CompSortType.Updated => "3",
                _ => "2"
            };
            address.Append($"&sortField={sortField}");

            return address.ToString();
        }

        /// <summary>
        ///     获取对应的 Modrinth API 请求链接。若返回 Nothing 则为不进行 Modrinth 请求。
        /// </summary>
        public string? GetModrinthAddress()
        {
            if (!source.HasFlag(CompSourceType.Modrinth))
                return null;

            if (tag.EndsWithF("/"))
                storage.modrinthTotal = 0;

            if (storage.modrinthTotal > -1 && storage.modrinthTotal <= storage.modrinthOffset)
                return null;

            var index = sort switch
            {
                CompSortType.Downloads => "downloads",
                CompSortType.Follows => "follows",
                CompSortType.Newest => "newest",
                CompSortType.Updated => "updated",
                _ => "relevance"
            };
            var address = $"https://api.modrinth.com/v2/search?limit={CompPageSize}&index={index}";

            if (!string.IsNullOrEmpty(searchText))
                address += $"&query={WebUtility.UrlEncode(searchText)}";
            if (storage.modrinthOffset > 0)
                address += $"&offset={storage.modrinthOffset}";

            var facets = new List<string>
            {
                $"[\"project_type:{Common.GetStringFromEnum(type)?.ToLower() ?? ""}\"]"
            };
            if (!string.IsNullOrEmpty(tag))
                facets.Add($"[\"categories:'{tag.AfterLast("/")}'\"]");
            if (modLoader != CompLoaderType.Any)
                facets.Add($"[\"categories:'{Common.GetStringFromEnum(modLoader)?.ToLower() ?? ""}'\"]");
            if (!string.IsNullOrEmpty(gameVersion))
                facets.Add($"[\"versions:'{gameVersion}'\"]");

            address += $"&facets=[{string.Join(",", facets)}]";
            return address;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not CompProjectRequest request)
                return false;

            return type == request.type
                && targetResultCount == request.targetResultCount
                && (tag ?? "") == (request.tag ?? "")
                && modLoader == request.modLoader
                && source == request.source
                && (gameVersion ?? "") == (request.gameVersion ?? "")
                && (searchText ?? "") == (request.searchText ?? "")
                && sort == request.sort;
        }

        public static bool operator ==(CompProjectRequest left, CompProjectRequest right)
        {
            return EqualityComparer<CompProjectRequest>.Default.Equals(left, right);
        }

        public static bool operator !=(CompProjectRequest left, CompProjectRequest right)
        {
            return !(left == right);
        }
    }
}

using System.Text.Json.Serialization;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;

namespace PCL;

public static partial class Comp
{
    public class CompFavorites
    {
        private static List<FavData>? _favoritesList;

        /// <summary>
        ///     收藏的工程列表
        /// </summary>
        public static List<FavData>? FavoritesList
        {
            get
            {
                if (_favoritesList is not null) return _favoritesList;

                var rawData = States.Game.CompFavorites;
                List<FavData>? rawList = null;
                try
                {
                    rawList = JsonSerializer.Deserialize<List<FavData>>(
                        rawData,
                        JsonCompat.SerializerOptions);
                }
                catch (Exception)
                {
                    try
                    {
                        var migrate =
                            JsonSerializer.Deserialize<HashSet<string>>(
                                rawData,
                                JsonCompat.SerializerOptions);
                        if (migrate is not null)
                            rawList =
                            [
                                GetNewFav(
                                    Lang.Text("Download.Comp.Detail.Favorites.DefaultName"),
                                    migrate)
                            ];
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                if (rawList is null || rawList.Count == 0)
                    rawList =
                    [
                        GetNewFav(
                            Lang.Text("Download.Comp.Detail.Favorites.DefaultName"),
                            null)
                    ];
                _favoritesList = rawList;
                Save();

                return _favoritesList;
            }
            set
            {
                _favoritesList = value;
                if (_favoritesList is not null)
                    foreach (var item in _favoritesList)
                        item.Notes = item
                            .Notes
                            .Where(n => !string.IsNullOrWhiteSpace(n.Value))
                            .ToDictionary();
                _ = JsonSerializer.Serialize(_favoritesList, JsonCompat.SerializerOptions);
                States.Game.CompFavorites =
                    JsonSerializer.Serialize(_favoritesList, JsonCompat.SerializerOptions);
            }
        }

        public static string GetShareCode(HashSet<string> data)
        {
            try
            {
                return JsonSerializer.Serialize(data, JsonCompat.SerializerOptions);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "[CompFavorites] 生成分享出错");
            }

            return "";
        }

        public static HashSet<string> GetIdsByShareCode(string code)
        {
            try
            {
                return JsonSerializer.Deserialize<HashSet<string>>(
                        code,
                        JsonCompat.SerializerOptions)
                    ?? [];
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "[CompFavorites] 通过分享获取 ID 出错");
            }

            return [];
        }


        /// <summary>
        ///     保存收藏夹数据
        /// </summary>
        public static void Save()
        {
            FavoritesList = _favoritesList;
        }

        /// <summary>
        ///     获取一个新的收藏夹
        /// </summary>
        /// <param name="name"></param>
        /// <param name="favList">没有传 Nothing</param>
        /// <returns></returns>
        public static FavData GetNewFav(string name, HashSet<string>? favList)
        {
            var res = new FavData
            {
                Name = name,
                Id = Guid.NewGuid().ToString(),
                Favs = favList ?? []
            };
            return res;
        }

        public static bool IsFavourite(string id)
        {
            return FavoritesList is not null && FavoritesList.Any(i => i.Favs.Contains(id));
        }

        public class FavData
        {
            /// <summary>
            ///     收藏夹名称
            /// </summary>
            /// <returns></returns>
            [JsonPropertyName("Name")]
            public string Name { get; set; } = "";

            /// <summary>
            ///     Guid
            /// </summary>
            /// <returns></returns>
            [JsonPropertyName("Id")]
            public string Id { get; set; } = "";

            /// <summary>
            ///     收藏的工程 ID 列表
            /// </summary>
            /// <returns></returns>
            [JsonPropertyName("Favs")]
            public HashSet<string> Favs { get; set; } = [];

            /// <summary>
            ///     备注
            /// </summary>
            /// <returns></returns>
            [JsonPropertyName("Notes")]
            public Dictionary<string, string> Notes { get; set; } = new();
        }
    }
}
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Network;

namespace PCL;

public static partial class Comp
{
    public partial class CompProject
    {
        /// <summary>
        ///     关联的数据库条目。若为 Nothing 则没有。
        /// </summary>
        private CompDatabaseEntry? DatabaseEntry
        {
            get
            {
                if (!_loadedDatabase)
                {
                    _loadedDatabase = true;
                    if (Type is CompType.Mod or CompType.DataPack)
                        field = GetCompWikiEntryBySlug(Slug);
                }

                return field;
            }
            set
            {
                _loadedDatabase = true;
                field = value;
            }
        }

        /// <summary>
        ///     MC 百科的页面 ID。若为 0 则没有。
        /// </summary>
        public int WikiId => DatabaseEntry?.WikiId ?? 0;

        /// <summary>
        ///     翻译后的中文名。若数据库没有则等同于 RawName。
        /// </summary>
        public string TranslatedName =>
            Lang.IsChineseMainland && DatabaseEntry?.ChineseName is { Length: > 0 } cn
                ? cn
                : RawName;

        /// <summary>
        ///     中文描述。若为 Nothing 则没有。
        /// </summary>
        public Task<string?> ChineseDescription => GetChineseDescriptionAsync();

        private async Task<string?> GetChineseDescriptionAsync()
        {
            var from = FromCurseForge
                ? "curseforge"
                : "modrinth";
            string? result = null;

            var descHash = $"{Id}{Common.GetStringMd5(Description)}";
            var cacheFilePath = $@"{Common.pathTemp}Cache\CompTranslation.ini";
            var cacheTranslation = Common.ReadIni(cacheFilePath, descHash);
            if (!string.IsNullOrWhiteSpace(cacheTranslation))
            {
                result = Common.Base64Decode(cacheTranslation);
                return result;
            }

            try
            {
                var jsonObject = (JsonObject)await
                    Requester.FetchJsonAsync($"https://mod.mcimirror.top/translate/{from}/{Id}");
                if (jsonObject.ContainsKey("translated"))
                {
                    result = jsonObject["translated"]?.ToString() ?? string.Empty;
                    Common.WriteIni(cacheFilePath, descHash, Common.Base64Encode(result));
                }
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("404"))
                {
                    Launcher.MyMsgBox(
                        Lang.Text("Download.Comp.Detail.DescriptionNoTranslation"),
                        Lang.Text("Download.Comp.Detail.DescriptionTranslationFailed"),
                        Lang.Text("Download.Comp.Detail.KnownButton"));
                    return null;
                }

                Common.Log(
                    ex,
                    "获取中文描述时出现错误",
                    Common.LogLevel.Hint,
                    userSummary: Lang.Text("Minecraft.Comp.Error.OperationFailed"));
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    "获取中文描述时出现错误",
                    Common.LogLevel.Hint,
                    userSummary: Lang.Text("Minecraft.Comp.Error.OperationFailed"));
            }

            return result;
        }

        /// <summary>
        ///     将当前实例转为可用于保存缓存的 Json。
        /// </summary>
        public JsonObject ToJson()
        {
            var json = new JsonObject
            {
                ["DataSource"] = FromCurseForge ? "CurseForge" : "Modrinth",
                ["Type"] = (int)Type,
                ["Slug"] = Slug,
                ["Id"] = Id
            };

            if (CurseForgeFileIds is not null)
                json["CurseForgeFileIds"] = new JsonArray(
                    CurseForgeFileIds
                        .Select(i => (JsonNode)i)
                        .ToArray());

            json["RawName"] = RawName;
            json["Description"] = Description;
            json["Website"] = Website;

            if (LastUpdate is not null)
                json["LastUpdate"] = LastUpdate;

            json["DownloadCount"] = DownloadCount;

            if (ModLoaders is not null && ModLoaders.Count != 0)
                json["ModLoaders"] =
                    new JsonArray(ModLoaders.Select(m => (JsonNode)(int)m).ToArray());

            json["Tags"] = new JsonArray(Tags.Select(s => (JsonNode)s).ToArray());

            if (LogoUrl is not null)
                json["LogoUrl"] = LogoUrl;

            if (Drops.Count != 0)
                json["Drops"] = new JsonArray(Drops.Select(i => (JsonNode)i).ToArray());

            json["CacheTime"] = DateTime.Now;

            return json;
        }

        /// <summary>
        ///     将当前工程信息实例化为控件。
        /// </summary>
        /// <param name="showQuickDownload">是否在卡片右侧显示快速下载按钮（仅搜索结果页应传 true）。</param>
        /// <exception cref="ArgumentNullException"></exception>
        public MyVirtualizingElement<MyCompItem> ToCompItem(
            bool showMcVersionDesc,
            bool showLoaderDesc,
            bool showQuickDownload = false)
        {
            var gameVersionDescription = _BuildGameVersionDescription();

            var modLoadersForDesc = ModLoaders.ToList();
            if (Config.Download.Comp.IgnoreQuilt) modLoadersForDesc.Remove(CompLoaderType.Quilt);

            var (fullDesc, partDesc) = modLoadersForDesc.Count switch
            {
                0 => ModLoaders.Count == 1
                    ? (
                        Lang.Text("Download.Comp.Type.Only", ModLoaders.Single().ToString()),
                        ModLoaders.Single().ToString())
                    : (
                        Lang.Text("Download.Comp.Type.Unknown"),
                        ""),
                1 => (
                    Lang.Text("Download.Comp.Type.Only", modLoadersForDesc.Single().ToString()),
                    modLoadersForDesc.Single().ToString()),
                _ => _GetMultiLoaderDesc(modLoadersForDesc)
            };

            return new MyVirtualizingElement<MyCompItem>(() =>
            {
                var newItem = new MyCompItem
                {
                    Tag = this
                };
                ApplyLogoToMyImage(newItem.PathLogo);

                var title = GetControlTitle(true);
                newItem.Title = title.Key;

                if (string.IsNullOrEmpty(title.Value))
                    ((StackPanel)newItem.LabTitleRaw.Parent).Children.Remove(
                        newItem.LabTitleRaw);
                else
                    newItem.SubTitle = title.Value;

                newItem.Tags = Tags;
                newItem.Description = Description.Replace("\r", "").Replace("\n", "");
                newItem.ShowDownloadBtn = showQuickDownload;

                newItem.LabVersion.Text = (showMcVersionDesc, showLoaderDesc) switch
                {
                    (true, true) =>
                        $"{(string.IsNullOrEmpty(partDesc) ? "" : $"{partDesc} ")}{gameVersionDescription}",
                    (true, false) => gameVersionDescription,
                    (false, true) => fullDesc,
                    _ => ""
                };

                if (!showMcVersionDesc && !showLoaderDesc)
                {
                    ((Grid)newItem.SvgIconVersion.Parent).Children.Remove(
                        newItem.SvgIconVersion);
                    ((Grid)newItem.LabVersion.Parent).Children.Remove(newItem.LabVersion);
                    newItem.ColumnVersion1.Width = new GridLength(0);
                    newItem.ColumnVersion2.MaxWidth = 0;
                    newItem.ColumnVersion3.Width = new GridLength(0);
                }

                newItem.LabSource.Text = FromCurseForge ? "CurseForge" : "Modrinth";

                if (LastUpdate is not null)
                {
                    newItem.LabTime.Text = Lang.TimeSpan(LastUpdate.Value - DateTime.Now, 1);
                }
                else
                {
                    newItem.LabTime.Visibility = Visibility.Collapsed;
                    newItem.ColumnTime1.Width =
                        newItem.ColumnTime2.Width =
                            newItem.ColumnTime3.Width = new GridLength(0);
                }

                newItem.LabDownload.Text = Lang.CompactNumber(DownloadCount);

                return newItem;
            })
            {
                Height = 64
            };
        }

        private string _BuildGameVersionDescription()
        {
            if (Drops is null || Drops.Count == 0)
                return Lang.Text("Download.Comp.Detail.CompItem.SnapshotOnly");

            var segments = new List<string>();
            var isOld = false;

            for (var i = 0; i < Drops.Count; i++)
            {
                var startDrop = Drops[i];
                var endDrop = Drops[i];

                if (startDrop < 100)
                {
                    if (segments.Count != 0 && !isOld) break;
                    isOld = true;
                }

                for (var ii = i + 1; ii < Drops.Count; ii++)
                {
                    if (GameDownload.AllDrops is null
                        || GameDownload.AllDrops.IndexOf(Drops[ii])
                        != GameDownload.AllDrops.IndexOf(endDrop) + 1) break;
                    endDrop = Drops[ii];
                    i = ii;
                }

                var startName = McInstanceInfo.DropToVersion(startDrop);
                var endName = McInstanceInfo.DropToVersion(endDrop);

                if (startDrop == endDrop)
                {
                    segments.Add(startName);
                }
                else if (GameDownload.AllDrops?.Any() == true
                         && startDrop >= GameDownload.AllDrops.First())
                {
                    if (endDrop < 100)
                    {
                        segments.Clear();
                        segments.Add(Lang.Text("Download.Comp.Detail.CompItem.AllVersions"));
                        break;
                    }

                    segments.Add($"{endName}+");
                }
                else if (endDrop < 100)
                {
                    segments.Add($"{startName}-");
                    break;
                }
                else if (GameDownload.AllDrops is null
                         || GameDownload.AllDrops.IndexOf(endDrop)
                         - GameDownload.AllDrops.IndexOf(startDrop)
                         == 1)
                {
                    segments.Add($"{startName}, {endName}");
                }
                else
                {
                    segments.Add($"{startName}~{endName}");
                }
            }

            return string.Join(", ", segments);
        }

        private (string, string) _GetMultiLoaderDesc(List<CompLoaderType> modLoadersForDesc)
        {
            var newestDrop = Drops?.FirstOrDefault() ?? 9999;
            var isAny = ModLoaders.Contains(CompLoaderType.Forge)
                && (newestDrop < 140 || ModLoaders.Contains(CompLoaderType.Fabric))
                && (newestDrop < 200 || ModLoaders.Contains(CompLoaderType.NeoForge))
                && (newestDrop < 140
                    || ModLoaders.Contains(CompLoaderType.Quilt)
                    || Config.Download.Comp.IgnoreQuilt);

            var joined = string.Join(" / ", modLoadersForDesc);
            return isAny
                ? (Lang.Text("Download.Comp.Type.Any"), "")
                : (joined, joined);
        }

        public MyListItem ToListItem()
        {
            var result = new MyListItem
            {
                Title = TranslatedName,
                Info = Description
                    .Replace("\r", "")
                    .Replace("\n", ""),
                Logo = string.IsNullOrEmpty(LogoUrl)
                    ? $"{Common.PathImage}Icons/NoIcon.png"
                    : LogoUrl,
                Tags = Tags,
                Tag = this,
                LogoCornerRadius = new CornerRadius(6)
            };
            return result;
        }

        public void ApplyLogoToMyImage(MyImage img)
        {
            if (string.IsNullOrEmpty(LogoUrl))
            {
                img.Source = $"{Common.PathImage}Icons/NoIcon.png";
            }
            else
            {
                img.Source = LogoUrl;
                img.FallbackSource = GameDownload.DlSourceModGet(LogoUrl);
            }
        }

        public KeyValuePair<string, string> GetControlTitle(bool hasModLoaderDescription)
        {
            if (!_TryGetControlTitleParts(out var title, out var subtitleList))
                return new KeyValuePair<string, string>(title, "");

            var subtitleResult = _BuildControlSubtitle(subtitleList, hasModLoaderDescription);
            return new KeyValuePair<string, string>(title, subtitleResult);
        }

        private bool _TryGetControlTitleParts(out string title, out List<string> subtitleList)
        {
            subtitleList = [];

            if (TranslatedName != RawName)
            {
                title = _GetTranslatedTitleParts(subtitleList);
                return true;
            }

            var nameLists = TranslatedName
                .Split(
                    [" | ", " - ", "(", ")", "[", "]", "{", "}"],
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim(' ', '/', '\\', '"'))
                .Where(w => !string.IsNullOrEmpty(w))
                .ToList();

            if (nameLists.Count <= 1)
            {
                title = RawName;
                return false;
            }

            var normalNameList = new List<string>();
            foreach (var name in nameLists)
            {
                var lowerName = name.ToLower();
                if ((name.Equals(name, StringComparison.CurrentCultureIgnoreCase)
                        && name != "FPS"
                        && name != "HUD")
                    || _IsModLoaderMarker(lowerName))
                    subtitleList.Add(name);
                else
                    normalNameList.Add(name);
            }

            if (normalNameList.Count == 0 || subtitleList.Count == 0)
            {
                title = RawName;
                return false;
            }

            title = string.Join(" - ", normalNameList);
            return true;
        }

        private string _GetTranslatedTitleParts(List<string> subtitleList)
        {
            var title = TranslatedName.BeforeFirst(" (").BeforeFirst(" - ");

            var suffix = "";
            if (TranslatedName.AfterLast(")").Contains(" - "))
                suffix = TranslatedName.AfterLast(")").AfterLast(" - ");

            var englishName = TranslatedName;
            if (!string.IsNullOrEmpty(suffix))
                englishName = englishName.Replace($" - {suffix}", "");

            englishName = englishName.Replace(title, "").Trim('(', ')', ' ');

            subtitleList.AddRange(
                englishName
                    .Split(
                        [" | ", " - ", "(", ")", "[", "]", "{", "}"],
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim(' ', '/'))
                    .Where(w => !string.IsNullOrEmpty(w)));

            if (subtitleList.Count > 1
                && !subtitleList.Any(s => _IsModLoaderMarker(s.ToLower()))
                && !(subtitleList.Count == 2
                    && subtitleList.Last().ToUpper() == subtitleList.Last()))
            {
                subtitleList.Clear();
                subtitleList.Add(englishName);
            }

            if (!string.IsNullOrEmpty(suffix)) subtitleList.Add(suffix);

            return title;
        }

        private static string _BuildControlSubtitle(
            List<string> subtitleList,
            bool hasModLoaderDescription)
        {
            var finalSubtitles = new List<string>();
            foreach (var rawEx in subtitleList.Distinct())
            {
                var ex = rawEx;
                var lowerEx = ex.ToLower();
                var isModLoader = lowerEx.Contains("forge")
                    || lowerEx.Contains("fabric")
                    || lowerEx.Contains("quilt");

                if (!hasModLoaderDescription && isModLoader) continue;
                if (ex.Length < 16 && lowerEx.Contains("fabric") && lowerEx.Contains("forge"))
                    continue;

                if (isModLoader
                    && !ex.Contains('版')
                    && lowerEx
                        .Replace("forge", "")
                        .Replace("fabric", "")
                        .Replace("quilt", "")
                        .Length
                    <= 3)
                    ex = ex
                            .Replace("Edition", "", StringComparison.OrdinalIgnoreCase)
                            .Replace("edition", "", StringComparison.OrdinalIgnoreCase)
                            .Trim()
                            .Capitalize()
                        + Lang.Text("Download.Comp.Detail.CompItem.EditionSuffix");

                ex = ex
                    .Replace("forge", "Forge")
                    .Replace("neo", "Neo")
                    .Replace("fabric", "Fabric")
                    .Replace("quilt", "Quilt");
                finalSubtitles.Add(ex.Trim());
            }

            return finalSubtitles.Count != 0
                ? $"  |  {string.Join("  |  ", finalSubtitles)}"
                : "";
        }

        private static bool _IsModLoaderMarker(string input)
        {
            return (input.Contains("forge") || input.Contains("fabric") || input.Contains("quilt"))
                && !input
                    .Replace("forge", "")
                    .Replace("fabric", "")
                    .Replace("quilt", "")
                    .RegexCheck("[a-z]+");
        }

        public bool IsLike(CompProject project)
        {
            if (SameText(Id, project.Id))
                return true;

            if (FromCurseForge == project.FromCurseForge)
                return false;

            if (!HasSameItems(ModLoaders, project.ModLoaders))
                return false;

            if (Type != CompType.Shader && !HasSameItems(Drops, project.Drops))
                return false;

            if (LastUpdate is { } lastUpdate
                && project.LastUpdate is { } projectLastUpdate
                && Math.Abs((lastUpdate - projectLastUpdate).TotalDays) > 7)
                return false;

            if (!HasMatchingText(project))
                return false;

            Common.Log(
                $"[Comp] 将 {RawName} ({Slug}) 与 {project.RawName} ({project.Slug}) 认定为相似工程");

            if (DatabaseEntry is null)
                DatabaseEntry = project.DatabaseEntry;
            else if (project.DatabaseEntry is null)
                project.DatabaseEntry = DatabaseEntry;

            return true;

            static bool SameText(string? left, string? right)
            {
                return (left ?? "") == (right ?? "");
            }

            static bool HasSameItems<T>(ICollection<T> left, ICollection<T> right)
            {
                return left.Count == right.Count && !left.Except(right).Any();
            }

            bool HasMatchingText(CompProject other)
            {
                return SameText(TranslatedName, other.TranslatedName)
                    || SameText(RawName, other.RawName)
                    || SameText(Description, other.Description)
                    || SameText(GetRaw(Slug), GetRaw(other.Slug));
            }

            static string GetRaw(string data)
            {
                return string.Concat(data.Where(char.IsLetterOrDigit)).ToLower();
            }
        }

        public override string ToString()
        {
            return $"{Id} ({Slug}): {RawName}";
        }

        public override bool Equals(object obj)
        {
            var project = obj as CompProject;
            return project is not null && (Id ?? "") == (project.Id ?? "");
        }

        public static bool operator ==(CompProject left, CompProject right)
        {
            return EqualityComparer<CompProject>.Default.Equals(left, right);
        }

        public static bool operator !=(CompProject left, CompProject right)
        {
            return !(left == right);
        }
    }
}

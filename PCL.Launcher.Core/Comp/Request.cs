namespace PCL;

public static partial class Comp
{
    public class CompRequest
    {
        /// <summary>
        ///     通过项目 Id 判断是否来自 CurseForge
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool IsFromCurseForge(string id)
        {
            var res = 0;
            return int.TryParse(id, out res);
        }

        /// <summary>
        ///     通过一堆 ID 从 Modrinth 那获取项目信息
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public static async Task<List<CompProject>> GetListByIdsFromModrinthAsync(List<string> ids)
        {
            var res = new List<CompProject>();
            try
            {
                await Task.Run(() =>
                {
                    var rawProjectsData = GameDownload.DlModRequest<JsonArray>(
                        $"https://api.modrinth.com/v2/projects?ids=[\"{ids.Join("\",\"")}\"]");
                    res.AddRange(
                        from rawData in rawProjectsData.OfType<JsonObject>()
                        select new CompProject(rawData));
                });
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "从 Modrinth 获取数据失败");
            }

            return res;
        }

        /// <summary>
        ///     通过一堆 ID 从 CurseForge 那获取项目信息
        /// </summary>
        /// <param name="Ids"></param>
        /// <returns></returns>
        public static async Task<List<CompProject>> GetListByIdsFromCurseforgeAsync(
            List<string> ids)
        {
            var res = new List<CompProject>();
            try
            {
                await Task.Run(() =>
                {
                    var jsonBody = $"{{\"modIds\": [{string.Join(",", ids)}]}}";

                    var response = GameDownload.DlModRequest<JsonObject>(
                        "https://api.curseforge.com/v1/mods",
                        "POST",
                        jsonBody,
                        "application/json"
                    );

                    if (response["data"] is not JsonArray rawProjectsData) return;
                    var projectList = rawProjectsData
                        .Cast<JsonObject>()
                        .Select(data => new CompProject(data))
                        .ToList();

                    res.AddRange(projectList);
                });
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "Failed to get project data from CurseForge");
            }

            return res;
        }

        public static List<CompProject> GetCompProjectsByIds(List<string> input)
        {
            return GetCompProjectsByIdsAsync(input).GetAwaiter().GetResult();
        }

        public static async Task<List<CompProject>> GetCompProjectsByIdsAsync(List<string> input)
        {
            if (input.Count == 0)
                return [];

            var modrinthIds = new List<string>();
            var curseForgeIds = new List<string>();
            foreach (var id in input)
                if (IsFromCurseForge(id))
                    curseForgeIds.Add(id);
                else
                    modrinthIds.Add(id);

            var tasks = new List<Task<List<CompProject>>>();
            if (curseForgeIds.Count != 0)
                tasks.Add(GetListByIdsFromCurseforgeAsync(curseForgeIds));
            if (modrinthIds.Count != 0)
                tasks.Add(GetListByIdsFromModrinthAsync(modrinthIds));

            await Task.WhenAll(tasks.ToArray());
            var result = new List<CompProject>();
            foreach (var task in tasks)
                result.AddRange(task.Result);

            return result;
        }
    }
}
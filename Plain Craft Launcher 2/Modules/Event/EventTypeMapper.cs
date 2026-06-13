namespace PCL
{
    public static class EventTypeMapper
    {
        private static readonly Dictionary<string, string> ChineseToEnglish = new()
        {
            { "打开网页", "OpenUrl" },
            { "打开文件", "OpenFile" },
            { "执行命令", "ExecuteCommand" },
            { "启动游戏", "LaunchGame" },
            { "复制文本", "CopyText" },
            { "刷新主页", "RefreshHomepage" },
            { "刷新页面", "RefreshPage" },
            { "今日人品", "DailyFortune" },
            { "清理垃圾", "ClearTrash" },
            { "弹出窗口", "ShowDialog" },
            { "弹出提示", "ShowHint" },
            { "切换页面", "SwitchPage" },
            { "导入整合包", "ImportModpack" },
            { "安装整合包", "InstallModpack" },
            { "下载文件", "DownloadFile" },
            { "修改设置", "ModifySetting" },
            { "写入设置", "WriteSetting" },
            { "修改变量", "ModifyVariable" },
            { "写入变量", "WriteVariable" }
        };

        private static readonly Dictionary<string, string> EnglishToChinese =
            ChineseToEnglish.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

        public static readonly HashSet<string> UnsupportedTypes = new(StringComparer.Ordinal)
        {
            "打开帮助", "刷新帮助", "内存优化", "加入房间", "检查更新"
        };

        public static bool TryToEnglish(string chineseName, out string englishName) =>
            ChineseToEnglish.TryGetValue(chineseName, out englishName);

        public static bool TryToChinese(string englishName, out string chineseName) =>
            EnglishToChinese.TryGetValue(englishName, out chineseName);

        public static bool IsUnsupportedType(string name) =>
            UnsupportedTypes.Contains(name);

        public static bool IsKnownChineseType(string name) =>
            ChineseToEnglish.ContainsKey(name);

        public static bool TryParse(string value, out EventType result)
        {
            if (Enum.TryParse(value, true, out result))
                return true;

            if (TryToEnglish(value, out var englishName))
                return Enum.TryParse(englishName, true, out result);

            return false;
        }
    }
}

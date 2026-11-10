using System.IO;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;

namespace PCL;

public static partial class Music
{
    /// <summary>
    ///     接下来要播放的音乐文件路径。未初始化时为 Nothing。
    /// </summary>
    public static List<string>? musicWaitingList;

    /// <summary>
    ///     全部音乐文件路径。未初始化时为 Nothing。
    /// </summary>
    public static List<string>? musicAllList;

    /// <summary>
    ///     初始化音乐播放列表。
    /// </summary>
    /// <param name="forceReload">强制全部重新载入列表。</param>
    /// <param name="preventFirst">在重载列表时避免让某项成为第一项。</param>
    private static void MusicListInit(bool forceReload, string? preventFirst = null)
    {
        if (forceReload)
            musicAllList = null;

        try
        {
            if (musicAllList is null)
            {
                musicAllList = [];
                var musicDir = Path.Combine(Common.ExePath, "PCL", "Musics");
                Directory.CreateDirectory(musicDir);
                foreach (var file in Common.EnumerateFiles(musicDir))
                {
                    var ext = file.Extension.ToLowerInvariant();
                    if (new[]
                        {
                            ".ini",
                            ".jpg",
                            ".txt",
                            ".cfg",
                            ".lrc",
                            ".db",
                            ".png"
                        }
                        .Contains(ext))
                        continue;
                    musicAllList.Add(file.FullName);
                }
            }

            musicWaitingList = Config.Preference.Music.ShufflePlayback
                ? RandomUtils.Shuffle(new List<string>(musicAllList))
                : [.. musicAllList];

            if (preventFirst is not null
                && musicWaitingList.Count > 0
                && string.Equals(
                    musicWaitingList[0],
                    preventFirst,
                    StringComparison.OrdinalIgnoreCase))
            {
                musicWaitingList.RemoveAt(0);
                musicWaitingList.Add(preventFirst);
            }
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                "初始化音乐列表失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Music.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     获取下一首播放的音乐路径并将其从列表中移除。
    ///     如果没有，可能会返回 Nothing。
    /// </summary>
    private static string? DequeueNextMusicAddress()
    {
        if (musicAllList is null
            || musicAllList.Count == 0
            || musicWaitingList is null
            || musicWaitingList.Count == 0)
            MusicListInit(false);

        if (musicWaitingList is { Count: > 0 })
        {
            var nextMusic = musicWaitingList[0];
            musicWaitingList.RemoveAt(0);
            if (musicWaitingList.Count == 0) MusicListInit(false, nextMusic);
            return nextMusic;
        }

        return null;
    }
}
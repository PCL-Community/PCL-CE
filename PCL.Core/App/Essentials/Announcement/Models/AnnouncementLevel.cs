namespace PCL.Core.App.Essentials.Announcement.Models;

public enum AnnouncementLevel
{
    /// <summary>
    /// 最低的等级，属于可看可不看的那种
    /// </summary>
    Lowest,
    /// <summary>
    /// 用户应该稍微有点了解的公告
    /// </summary>
    Medium,
    /// <summary>
    /// 必须让用户知道并理解的公告内容
    /// </summary>
    Highest
}
namespace PCL;

public class VersionAnnouncementDataModel
{
    public List<VersionAnnouncementContentModel> content { get; set; }
}

public class VersionAnnouncementContentModel
{
    public string title { get; set; }
    public string detail { get; set; }
    public string id { get; set; }
    public string date { get; set; }
    public AnnouncementBtnInfoModel btn1 { get; set; }
    public AnnouncementBtnInfoModel btn2 { get; set; }
}

public class AnnouncementBtnInfoModel
{
    public string text { get; set; }
    public string command { get; set; }
    public string command_paramter { get; set; }
}
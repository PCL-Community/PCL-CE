using System;
using System.Globalization;
using PCL.Core.App;

namespace PCL.Core.Utils;

public static class RegionUtils
{
    /// <summary>
    /// 获取区域限制状态
    /// </summary>
    public static bool IsRestrictedFeatAllowed =>
        Config.System.Debug.AllowRestrictedFeature || (TimeZoneInfo.Local.Id == "China Standard Time" &&
                                                       (CultureInfo.CurrentCulture.Name == "zh-CN" || CultureInfo.CurrentUICulture.Name == "zh-CN"));
}
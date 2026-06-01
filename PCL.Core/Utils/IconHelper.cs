using System;
using System.Drawing;
using System.IO;
using PCL.Core.App;

namespace PCL.Core.Utils;

public static class IconHelper
{
    public static string GetIconPath()
    {
        var paths = Path.Combine(Paths.Temp, "icon.png");
        if (!File.Exists(paths))
        {
            CreateIcon();
        }

        return paths;
    }

    private static void CreateIcon()
    {
        var icon = Icon.ExtractAssociatedIcon(Basics.ExecutablePath)!;
        var bitmap = icon.ToBitmap();
        bitmap.Save(Path.Combine(Paths.Temp, "icon.png"));
    }
}
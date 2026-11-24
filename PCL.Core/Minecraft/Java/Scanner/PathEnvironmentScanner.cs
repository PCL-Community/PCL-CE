using System;
using System.Collections.Generic;
using System.IO;
using PCL.Core.Logging;

namespace PCL.Core.Minecraft.Java.Scanner;

public class PathEnvironmentScanner : IJavaScanner
{
    public void Scan(ICollection<string> results)
    {
        try
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar)) return;

            foreach (var dir in pathVar.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Directory.Exists(dir)) continue;

                var javaExe = JavaExecutableLayout.GetJavaExePath(dir);
                if (File.Exists(javaExe)) results.Add(javaExe);
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "PATH环境变量扫描失败");
        }
    }
}
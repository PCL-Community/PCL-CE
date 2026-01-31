using PCL.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PCL.Core.Minecraft.Java.Scanner;

public class WhereCommandScanner : IJavaScanner
{
    public void Scan(ICollection<string> results)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "java",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0) return;

            var paths = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => File.Exists(p));

            foreach (var path in paths)
                results.Add(path);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "where 命令扫描失败");
        }
    }
}

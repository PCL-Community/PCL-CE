using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using PCL.Core.Logging;

namespace PCL.Core.Minecraft.Java.Scanner;

public class RegistryJavaScanner : IJavaScanner
{
    private static readonly string[] _RegistryPaths =
    [
        @"SOFTWARE\JavaSoft\Java Development Kit",
        @"SOFTWARE\JavaSoft\Java Runtime Environment",
        @"SOFTWARE\WOW6432Node\JavaSoft\Java Development Kit",
        @"SOFTWARE\WOW6432Node\JavaSoft\Java Runtime Environment"
    ];

    private static readonly string[] _BrandRegistryPaths =
    [
        @"SOFTWARE\Azul Systems\Zulu",
        @"SOFTWARE\BellSoft\Liberica"
    ];

    public void Scan(ICollection<string> results)
    {
        try
        {
            _ScanJavaSoftRegistry(results);
            _ScanBrandRegistry(results);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "注册表扫描失败");
        }
    }

    private static void _ScanJavaSoftRegistry(ICollection<string> results)
    {
        foreach (var regPath in _RegistryPaths)
        {
            using var regKey = Registry.LocalMachine.OpenSubKey(regPath);
            if (regKey is null) continue;

            foreach (var subKeyName in regKey.GetSubKeyNames())
            {
                using var subKey = regKey.OpenSubKey(subKeyName);
                var javaHome = subKey?.GetValue("JavaHome") as string;
                if (string.IsNullOrEmpty(javaHome)
                    || Path.GetInvalidPathChars().Any(javaHome.Contains)) continue;

                var javaExePath =
                    JavaExecutableLayout.GetJavaExePath(Path.Combine(javaHome, "bin"));
                if (File.Exists(javaExePath)) results.Add(javaExePath);
            }
        }
    }

    private static void _ScanBrandRegistry(ICollection<string> results)
    {
        foreach (var keyPath in _BrandRegistryPaths)
        {
            using var brandKey = Registry.LocalMachine.OpenSubKey(keyPath);
            if (brandKey is null) continue;

            foreach (var subKeyName in brandKey.GetSubKeyNames())
            {
                using var subKey = brandKey.OpenSubKey(subKeyName);
                var installPath = subKey?.GetValue("InstallationPath") as string;
                if (string.IsNullOrEmpty(installPath)
                    || Path.GetInvalidPathChars().Any(installPath.Contains)) continue;

                var javaExePath =
                    JavaExecutableLayout.GetJavaExePath(Path.Combine(installPath, "bin"));
                if (File.Exists(javaExePath)) results.Add(javaExePath);
            }
        }
    }
}
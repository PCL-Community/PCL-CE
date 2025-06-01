using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.IO.Compression;
using System.Net.Http.Headers;

namespace PCL.Core.Helper.Java
{
    public class JavaHelper
    {
        public static async Task<List<JavaModel>> ScanJava()
        {
            var javaList = new List<JavaModel>();
            var javaPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var SearchTasks = new List<Task>();
            var Searchers = new TaskFactory();
            SearchTasks.Add(Searchers.StartNew(() => ScanRegistryForJava(ref javaPaths)));
            SearchTasks.Add(Searchers.StartNew(() => ScanDefaultInstallPaths(ref javaPaths)));
            SearchTasks.Add(Searchers.StartNew(() => ScanPathEnvironmentVariable(ref javaPaths)));
            SearchTasks.Add(Searchers.StartNew(() => ScanMicrosoftStoreJava(ref javaPaths)));
            await Searchers.ContinueWhenAll(SearchTasks.ToArray(), completedTask => { });

            foreach (var javaExePath in javaPaths)
            {
                var javaModel = Prase(javaExePath);
                if (javaModel != null)
                {
                    javaList.Add(javaModel);
                }
            }

            return javaList
                .OrderByDescending(x => x.Version)
                .ToList();
        }

        public static JavaModel Prase(string JavaExePath)
        {
            try
            {
                var JavaFileVersion = FileVersionInfo.GetVersionInfo(JavaExePath);
                var JavaVersion = Version.Parse(JavaFileVersion.FileVersion);
                var CompanyName = JavaFileVersion.CompanyName
                    ?? JavaFileVersion.FileDescription
                    ?? JavaFileVersion.ProductName
                    ?? string.Empty;
                var JavaBrand = DetermineBrand(CompanyName);

                var CurrentJavaPath = Path.GetDirectoryName(JavaExePath);
                var IsJavaJre = !File.Exists(Path.Combine(CurrentJavaPath, "javac.exe"));
                var IsJava64Bit = Utils.Programe.IsExecutableFile64Bit(JavaExePath);
                var ShouldDisableByDefault = (IsJavaJre && JavaVersion.Major >= 16)
                    || (!IsJava64Bit && Environment.Is64BitOperatingSystem);

                return new JavaModel
                {
                    Path = CurrentJavaPath,
                    Version = JavaVersion,
                    IsJre = IsJavaJre,
                    Is64Bit = IsJava64Bit,
                    IsEnabled = !ShouldDisableByDefault,
                    Brand = JavaBrand
                };
            }
            catch
            {
                // 忽略无法获取版本的Java路径
            }
            return null;
        }

        private static void ScanRegistryForJava(ref HashSet<string> javaPaths)
        {
            var registryPaths = new List<string>
            {
                @"SOFTWARE\JavaSoft\Java Development Kit",
                @"SOFTWARE\JavaSoft\Java Runtime Environment",
                @"SOFTWARE\WOW6432Node\JavaSoft\Java Development Kit",
                @"SOFTWARE\WOW6432Node\JavaSoft\Java Runtime Environment"
            };

            foreach (var regPath in registryPaths)
            {
                using (RegistryKey regKey = Registry.LocalMachine.OpenSubKey(regPath))
                {
                    if (regKey != null)
                    {
                        foreach (var subKeyName in regKey.GetSubKeyNames())
                        {
                            using (RegistryKey subKey = regKey.OpenSubKey(subKeyName))
                            {
                                string javaHome = subKey?.GetValue("JavaHome") as string;
                                if (!string.IsNullOrEmpty(javaHome))
                                {
                                    string javaExePath = Path.Combine(javaHome, "bin\\java.exe");
                                    if (File.Exists(javaExePath))
                                    {
                                        javaPaths.Add(javaExePath);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void ScanDefaultInstallPaths(ref HashSet<string> javaPaths)
        {
            var programFilesPaths = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            };

            foreach (var pfPath in programFilesPaths)
            {
                string javaDir = Path.Combine(pfPath, "Java");
                if (Directory.Exists(javaDir))
                {
                    foreach (var dirPath in Directory.GetDirectories(javaDir))
                    {
                        string[] potentialJavas =
                        {
                            Path.Combine(dirPath, "bin", "java.exe"),
                            Path.Combine(dirPath, "jre", "bin", "java.exe")
                        };
                        var existingJavas = potentialJavas
                            .Where(File.Exists)
                            .ToList();
                        foreach (var item in existingJavas)
                        {
                            javaPaths.Add(item);
                        }
                    }
                }
            }
        }

        private static void ScanPathEnvironmentVariable(ref HashSet<string> javaPaths)
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv)) return;

            string[] paths = pathEnv.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var targetPath in paths)
            {
                string javaExePath = Path.Combine(targetPath, "java.exe");
                if (File.Exists(javaExePath))
                {
                    javaPaths.Add(javaExePath);
                }
            }
        }

        private static void ScanMicrosoftStoreJava(ref HashSet<string> javaPaths)
        {
            //TODO: 扫描  Microsoft Java
        }

        private static JavaBrandType DetermineBrand(string output)
        {
            if (output.IndexOf("Eclipse", StringComparison.OrdinalIgnoreCase) >= 0)
                return JavaBrandType.EclipseTemurin;
            if (output.IndexOf("Bellsoft", StringComparison.OrdinalIgnoreCase) >= 0)
                return JavaBrandType.Bellsoft;
            if (output.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0)
                return JavaBrandType.Microsoft;
            if (output.IndexOf("Amazon", StringComparison.OrdinalIgnoreCase) >= 0)
                return JavaBrandType.AmazonCorretto;
            if (output.IndexOf("Azul", StringComparison.OrdinalIgnoreCase) >= 0)
                return JavaBrandType.AzulZulu;
            if (output.IndexOf("Oracle", StringComparison.OrdinalIgnoreCase) >= 0)
                return JavaBrandType.Oracle;
            if (output.IndexOf("Alibaba", StringComparison.OrdinalIgnoreCase) >= 0)
                return JavaBrandType.Dragonwell;
            return JavaBrandType.Unknown;
        }

        public static List<JavaModel> SelectSuitableJava(Version MinVerison, Version MaxVersion)
        {
            var javaList = ScanJava().Result;
            return javaList
                .Where(java => java.Version >= MinVerison && java.Version <= MaxVersion)
                .OrderByDescending(java => java.Version)
                .ToList();
        }

    }
}

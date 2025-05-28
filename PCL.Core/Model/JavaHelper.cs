using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PCL.Core.Java
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
                try
                {
                    var output = await GetJavaVersionOutput(javaExePath);
                    var version = new Version(0,0,0);
                    var brand = JavaBrandType.Other;
                    ParseJavaVersion(output, ref version, ref brand);

                    javaList.Add(new JavaModel
                    {
                        Path = javaExePath,
                        Version = version,
                        Brand = brand
                    });
                }
                catch
                {
                    // 忽略无法获取版本的Java路径
                }
            }

            return javaList;
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
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            foreach (var pfPath in programFilesPaths)
            {
                string javaDir = Path.Combine(pfPath, "Java");
                if (Directory.Exists(javaDir))
                {
                    foreach (var dirPath in Directory.GetDirectories(javaDir))
                    {
                        string javaExePath = Path.Combine(dirPath, "bin", "java.exe");
                        if (File.Exists(javaExePath))
                        {
                            javaPaths.Add(javaExePath);
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

        private static async Task<string> GetJavaVersionOutput(string javaExePath)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = javaExePath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                try
                {
                    process.Start();
                    string output = await process.StandardError.ReadToEndAsync();
                    process.WaitForExit();
                    return output;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        private static void ParseJavaVersion(string output, ref Version version, ref JavaBrandType brand)
        {
            version = null;
            brand = JavaBrandType.Other;

            if (string.IsNullOrEmpty(output)) return;

            // 提取版本号
            Match versionMatch = Regex.Match(output, "version \"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (!versionMatch.Success) return;

            string versionString = versionMatch.Groups[1].Value;

            // 提取数字部分
            MatchCollection matches = Regex.Matches(versionString, @"\d+");
            if (matches.Count == 0) return;

            var parts = new List<int>();
            foreach (Match match in matches)
            {
                parts.Add(int.Parse(match.Value));
            }

            try
            {
                int major = parts[0];
                int minor = parts.Count > 1 ? parts[1] : 0;
                int build = parts.Count > 2 ? parts[2] : 0;
                int revision = parts.Count > 3 ? parts[3] : 0;
                version = new Version(major, minor, build, revision);
            }
            catch
            {
                // 版本解析失败
            }

            // 确定品牌
            brand = DetermineBrand(output);
        }

        private static JavaBrandType DetermineBrand(string output)
        {
            if (output.IndexOf("AdoptOpenJDK", StringComparison.OrdinalIgnoreCase) >= 0)
                return JavaBrandType.AdoptOpenJDK;
            if (output.IndexOf("Corretto", StringComparison.OrdinalIgnoreCase) >= 0)
                return JavaBrandType.AmazonCorretto;
            if (output.IndexOf("Zulu", StringComparison.OrdinalIgnoreCase) >= 0)
                return JavaBrandType.AzulZulu;
            if (output.IndexOf("OpenJDK", StringComparison.OrdinalIgnoreCase) >= 0)
                return JavaBrandType.OpenJDK;
            if (output.IndexOf("Java(TM) SE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                output.IndexOf("Oracle", StringComparison.OrdinalIgnoreCase) >= 0)
                return JavaBrandType.Oracle;

            return JavaBrandType.Other;
        }
    }
}
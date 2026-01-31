using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Java.Scanner;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PCL.Core.Test
{
    [TestClass]
    public class JavaTest
    {
        [TestMethod]
        public async Task TestJavaSearch()
        {
            // Java 搜索是否稳定
            var jas = new JavaManager(
                new PeHeaderParser(),
                [
                new RegistryJavaScanner(),
                new DefaultPathsScanner(),
                new PathEnvironmentScanner(),
                new MicrosoftStoreJavaScanner(),
                new WhereCommandScanner()
            ]);
            await jas.ScanJavaAsync();
            var firstSacnned = jas.GetSortedJavaList();
            foreach (var ja in firstSacnned)
            {
                Console.WriteLine(ja.ToString());
                Assert.IsGreaterThan(0, ja.Installation.Version.Major, "Java version is not valid: " + ja.Installation.JavaFolder);
                Assert.IsFalse(string.IsNullOrWhiteSpace(ja.Installation.JavaFolder));
            }
            await jas.ScanJavaAsync();
            var secondScaned = jas.GetSortedJavaList();
            Assert.HasCount(secondScaned.Count, firstSacnned);
            // Java 搜索是否能够正确选择
            Assert.IsTrue(secondScaned.Count == 0 || (secondScaned.Count > 0 && (await jas.SelectSuitableJavaAsync(new Version(1, 8, 0), new Version(30, 0, 0))).Length > 0));
            // Java 是否有重复
            Assert.IsFalse(secondScaned.GroupBy(x => x.Installation.JavaExePath).Any(x => x.Count() > 1));
        }
    }
}

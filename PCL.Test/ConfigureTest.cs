using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using PCL.Core.Helper.Configure;

namespace PCL.Test
{
    [TestClass]
    public class ConfigureTest
    {
        [TestMethod]
        public void TestJson()
        {
            var path = Path.Combine(Path.GetTempPath(), "PCLTest", $"{new Random().Next().ToString()}.json");

            var j1 = new JsonConfigure(path);
            j1.Set("awa",  "qwq");
            Assert.IsTrue(j1.Get<string>("awa") == "qwq");

            var j2 = new JsonConfigure(path);
            Assert.IsTrue(j2.Get<string>("awa") == "qwq");
            j2.Set("qwq", "awa");
            Assert.IsTrue(j2.Get<string>("qwq") == "awa");
        }

        [TestMethod]
        public void TestIni()
        {
            var path = Path.Combine(Path.GetTempPath(), "PCLTest", $"{new Random().Next().ToString()}.ini");

            var j1 = new IniConfigure(path);
            j1.Set("awa", "qwq");
            Assert.IsTrue(j1.Get<string>("awa") == "qwq");

            var j2 = new IniConfigure(path);
            Assert.IsTrue(j2.Get<string>("awa") == "qwq");
            j2.Set("qwq", "awa");
            Assert.IsTrue(j2.Get<string>("qwq") == "awa");
        }
    }
}

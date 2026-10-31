using System;
using System.Dynamic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils;

namespace PCL.Core.Test
{
    [TestClass]
    public class DynamicJsonTest
    {
        [TestMethod]
        public void TestDynamicJson()
        {
            dynamic obj = new { a = 123, b = new { c = this, d = "text" } };
            var options = new JsonSerializerOptions(JsonCompat.SerializerOptions)
            {
                WriteIndented = true
            };
            string json = JsonSerializer.Serialize(obj, options);
            Console.WriteLine(json);

            Assert.IsFalse(string.IsNullOrWhiteSpace(json), "序列化结果为空");

            using var document = JsonDocument.Parse(json);
            Assert.AreEqual(123, document.RootElement.GetProperty("a").GetInt32(), "a 字段序列化结果不符");
            Assert.AreEqual("text", document.RootElement.GetProperty("b").GetProperty("d").GetString(),
                "b.d 字段序列化结果不符");

            dynamic? obj2 = JsonSerializer.Deserialize<ExpandoObject>(json, options);
            Assert.IsNotNull(obj2, "反序列化结果为 null");
            Console.WriteLine(obj2?.ToString());
        }
    }
}

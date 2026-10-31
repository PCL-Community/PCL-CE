using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
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

            // 注意：不能声明为 dynamic，否则后续所有成员访问都会走运行时绑定，
            // ExpandoObject 上没有 Count 之类的成员，会抛 RuntimeBinderException。
            ExpandoObject? obj2 = JsonSerializer.Deserialize<ExpandoObject>(json, options);
            Assert.IsNotNull(obj2, "反序列化结果为 null");

            // 反序列化结果必须真的带上字段：此前只校验「返回了对象」，
            // 一个空的 ExpandoObject 也能通过，等于没验证字段是否丢失。
            var map = _AsMap(obj2);
            Assert.AreEqual(2, map.Count, "反序列化结果的字段数量不符");
            Assert.IsTrue(map.ContainsKey("a"), "反序列化结果缺少 a 字段");
            Assert.IsTrue(map.ContainsKey("b"), "反序列化结果缺少 b 字段");

            Assert.AreEqual(123, _ReadInt32(map["a"]), "反序列化后的 a 字段值不符");

            var nested = _AsMap(map["b"]);
            Assert.IsTrue(nested.ContainsKey("d"), "反序列化结果缺少 b.d 字段");
            Assert.AreEqual("text", _ReadString(nested["d"]), "反序列化后的 b.d 字段值不符");
        }

        /// <summary>
        /// System.Text.Json 反序列化到 <see cref="ExpandoObject"/> 时，嵌套对象可能是
        /// <see cref="ExpandoObject"/>，也可能仍以 <see cref="JsonElement"/> 形式存在，这里统一取字典视图。
        /// </summary>
        private static IDictionary<string, object?> _AsMap(object? value) => value switch
        {
            IDictionary<string, object?> map => map,
            JsonElement { ValueKind: JsonValueKind.Object } element => element
                .EnumerateObject()
                .ToDictionary(property => property.Name, property => (object?)property.Value),
            _ => throw new AssertFailedException($"无法把 {value?.GetType().Name ?? "null"} 当作对象读取")
        };

        /// <summary>
        /// 叶子值可能是 <see cref="JsonElement"/>，也可能已被装箱为基础类型，这里统一取值。
        /// </summary>
        private static int _ReadInt32(object? value) => value switch
        {
            JsonElement element => element.GetInt32(),
            int intValue => intValue,
            _ => throw new AssertFailedException($"无法从 {value?.GetType().Name ?? "null"} 读取整数")
        };

        private static string? _ReadString(object? value) => value switch
        {
            JsonElement element => element.GetString(),
            string text => text,
            _ => throw new AssertFailedException($"无法从 {value?.GetType().Name ?? "null"} 读取字符串")
        };
    }
}

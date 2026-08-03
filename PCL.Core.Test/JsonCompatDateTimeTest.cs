using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils;

namespace PCL.Core.Test
{
    [TestClass]
    public class JsonCompatDateTimeTest
    {
        [TestMethod]
        public void ParsesStandardIso()
        {
            Assert.IsTrue(JsonCompat.TryParseDateTime("2025-11-25T13:30:42+00:00", out var dt));
            Assert.AreEqual(new DateTimeOffset(2025, 11, 25, 13, 30, 42, TimeSpan.Zero).LocalDateTime, dt);
        }

        [TestMethod]
        public void ParsesNegativeZeroOffset()
        {
            // 部分社区清单用 -00:00 表示 UTC，应与 +00:00 等价
            Assert.IsTrue(JsonCompat.TryParseDateTime("2009-05-20T00:00:00-00:00", out var dt));
            Assert.AreEqual(new DateTimeOffset(2009, 5, 20, 0, 0, 0, TimeSpan.Zero).LocalDateTime, dt);
        }

        [TestMethod]
        public void ParsesEndOfDayHour24AsNextDayMidnight()
        {
            // ISO 8601：24:00:00 表示当日终点，等于次日零点。真实数据见 UVMC 清单版本 c0.27_st。
            Assert.IsTrue(JsonCompat.TryParseDateTime("2009-10-24T24:00:00+00:00", out var dt));
            Assert.AreEqual(new DateTimeOffset(2009, 10, 25, 0, 0, 0, TimeSpan.Zero).LocalDateTime, dt);
        }

        [TestMethod]
        public void RejectsInvalidTimeMasqueradingAsEndOfDay()
        {
            // 24:30:00 不是合法的当日终点写法，应解析失败而非被强行归一化
            Assert.IsFalse(JsonCompat.TryParseDateTime("2009-10-24T24:30:00+00:00", out _));
        }

        [TestMethod]
        public void RejectsGarbage()
        {
            Assert.IsFalse(JsonCompat.TryParseDateTime("not a date", out _));
            Assert.IsFalse(JsonCompat.TryParseDateTime("", out _));
            Assert.IsFalse(JsonCompat.TryParseDateTime(null, out _));
        }
    }
}

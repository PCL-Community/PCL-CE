// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using PCL.Core.Utils;

namespace PCL.Core.Portable.Test
{
    [TestClass]
    public sealed class SemVerTests
    {
        [TestMethod]
        public void TestParse()
        {
            var t1 = SemVer.Parse("2.3.4");
            Assert.IsTrue(
                t1.Major == 2
                && t1.Minor == 3
                && t1.Patch == 4
                );
            Console.WriteLine(t1.ToString());
            var t2 = SemVer.Parse("2.3.4-beta.1");
            Assert.IsTrue(
                t2.Major == 2
                && t2.Minor == 3
                && t2.Patch == 4
                && t2.Prerelease == "beta.1"
                );
            Console.WriteLine(t2.ToString());
            var t3 = SemVer.Parse("2.3.4-beta.1+11451aq");
            Assert.IsTrue(
                t3.Major == 2
                && t3.Minor == 3
                && t3.Patch == 4
                && t3.Prerelease == "beta.1"
                && t3.BuildMetadata == "11451aq"
                );
            Console.WriteLine(t3.ToString());
            var t4 = SemVer.Parse("456.759.159-alpha.2");
            Assert.IsTrue(
                t4.Major == 456
                && t4.Minor == 759
                && t4.Patch == 159
                && t4.Prerelease == "alpha.2"
                );
            Console.WriteLine(t4.ToString());

            var t5 = SemVer.Parse("v2.14.5-beta.1.2147483647");
            Assert.IsNotNull(t5);
            Console.WriteLine(t5.ToString());
            Assert.IsGreaterThan(t5, t4);
        }

        [TestMethod]
        [DataRow("1.0.0-alpha", "1.0.0-alpha.1")]
        [DataRow("1.0.0-alpha.1", "1.0.0-alpha.beta")]
        [DataRow("1.0.0-alpha.beta", "1.0.0-beta")]
        [DataRow("1.0.0-beta", "1.0.0-beta.2")]
        [DataRow("1.0.0-beta.2", "1.0.0-beta.11")]
        [DataRow("1.0.0-beta.11", "1.0.0-rc.1")]
        [DataRow("1.0.0-rc.1", "1.0.0")]
        public void ComparisonFollowsSemVerPrecedence(string lower, string higher)
        {
            Assert.IsLessThan(0, SemVer.Parse(lower).CompareTo(SemVer.Parse(higher)));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("1")]
        [DataRow("1.0")]
        [DataRow("01.0.0")]
        [DataRow("1.01.0")]
        [DataRow("1.0.01")]
        [DataRow("1.0.0-")]
        [DataRow("1.0.0-alpha..1")]
        [DataRow("1.0.0-01")]
        [DataRow("1.0.0+")]
        [DataRow("1.0.0+meta..data")]
        public void RejectsInvalidVersions(string value)
        {
            Assert.IsFalse(SemVer.TryParse(value, out _));
        }

        [TestMethod]
        public void ComparisonDoesNotAllocate()
        {
            var left = SemVer.Parse("1.2.3-beta.10");
            var right = SemVer.Parse("1.2.3-beta.11");

            _ = left.CompareTo(right);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 10_000; index++)
                _ = left.CompareTo(right);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
        }
    }
}

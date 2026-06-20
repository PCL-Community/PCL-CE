// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Minecraft.Java;
using PCL.Domain.Minecraft.Java;

namespace PCL.Application.Test;

[TestClass]
public sealed class JavaPreferenceParserTests
{
    [TestMethod]
    public void Parse_ShouldReturnAutoSelect_WhenPreferenceIsEmpty()
    {
        JavaPreference preference = JavaPreferenceParser.Parse("");

        Assert.IsInstanceOfType<AutoSelectJavaPreference>(preference);
    }

    [TestMethod]
    public void Parse_ShouldSupportLegacyUseGlobalText()
    {
        JavaPreference preference = JavaPreferenceParser.Parse(JavaPreferenceParser.LegacyUseGlobalText);

        Assert.IsInstanceOfType<UseGlobalJavaPreference>(preference);
    }

    [TestMethod]
    public void Parse_ShouldSupportLegacyAbsoluteJavaPath()
    {
        string javaPath = OperatingSystem.IsWindows()
            ? @"C:\Java\bin\java.exe"
            : "/opt/java/bin/java";

        JavaPreference preference = JavaPreferenceParser.Parse(javaPath);

        Assert.IsInstanceOfType<ExistingJavaPreference>(preference);
        Assert.AreEqual(javaPath, ((ExistingJavaPreference)preference).JavaExecutablePath);
    }

    [TestMethod]
    public void Parse_ShouldDowngradeLegacyRelativeJavaPathToGlobal()
    {
        JavaPreference preference = JavaPreferenceParser.Parse(@"jre\bin\java.exe");

        Assert.IsInstanceOfType<UseGlobalJavaPreference>(preference);
    }

    [TestMethod]
    public void Parse_ShouldSupportJsonExistingJavaPreference()
    {
        string javaPath = OperatingSystem.IsWindows()
            ? @"C:\Java\bin\java.exe"
            : "/opt/java/bin/java";

        JavaPreference preference = JavaPreferenceParser.Parse(
            $$"""{"kind":"exist","JavaExePath":"{{EscapeJson(javaPath)}}"}""");

        Assert.IsInstanceOfType<ExistingJavaPreference>(preference);
        Assert.AreEqual(javaPath, ((ExistingJavaPreference)preference).JavaExecutablePath);
    }

    [TestMethod]
    public void Parse_ShouldAcceptSafeRelativePathInsideBaseDirectory()
    {
        string baseDirectory = Path.Combine(Path.GetTempPath(), "pcl-java-preference");

        JavaPreference preference = JavaPreferenceParser.Parse(
            """{"kind":"relative","RelativePath":"jre/bin/java"}""",
            baseDirectory);

        Assert.IsInstanceOfType<UseRelativeJavaPreference>(preference);
        Assert.AreEqual("jre/bin/java", ((UseRelativeJavaPreference)preference).RelativePath);
    }

    [TestMethod]
    public void Parse_ShouldRejectRelativePathEscapingBaseDirectory()
    {
        string baseDirectory = Path.Combine(Path.GetTempPath(), "pcl-java-preference");

        JavaPreference preference = JavaPreferenceParser.Parse(
            """{"kind":"relative","RelativePath":"../outside/java"}""",
            baseDirectory);

        Assert.IsInstanceOfType<UseGlobalJavaPreference>(preference);
    }

    [TestMethod]
    public void Parse_ShouldFallbackToLegacy_WhenJsonIsMalformed()
    {
        JavaPreference preference = JavaPreferenceParser.Parse("{not-json");

        Assert.IsInstanceOfType<UseGlobalJavaPreference>(preference);
    }

    private static string EscapeJson(string value) =>
        value.Replace(@"\", @"\\", StringComparison.Ordinal);
}

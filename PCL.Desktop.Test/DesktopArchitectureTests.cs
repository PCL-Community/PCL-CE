// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Desktop;

namespace PCL.Desktop.Test;

[TestClass]
public sealed class DesktopArchitectureTests
{
    private static readonly string[] ForbiddenAssemblyNames =
    [
        "PresentationCore",
        "PresentationFramework",
        "System.Management",
        "WindowsBase",
        "PCL.Core",
        "PCL.Plugin",
        "PCL.Online",
        "Plain Craft Launcher 2"
    ];

    [TestMethod]
    public void DesktopAssembly_DoesNotReferenceWindowsOrLegacyUiAssemblies()
    {
        string[] references = typeof(App)
            .Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name ?? string.Empty)
            .ToArray();

        foreach (string forbidden in ForbiddenAssemblyNames)
        {
            CollectionAssert.DoesNotContain(
                references,
                forbidden,
                $"PCL.Desktop must not reference {forbidden}.");
        }
    }

    [TestMethod]
    public void DesktopSource_DoesNotUseWpfApis()
    {
        string desktopRoot = FindDesktopProjectRoot();
        string[] forbiddenTokens =
        [
            "using System.Windows;",
            "using System.Windows.Controls",
            "using System.Windows.Documents",
            "using System.Windows.Markup",
            "using System.Windows.Media",
            "using System.Windows.Threading",
            "PresentationFramework",
            "PresentationCore",
            "WindowsBase",
            "PCL.Online",
            "Plain Craft Launcher 2"
        ];

        List<string> violations = [];
        foreach (string file in Directory.EnumerateFiles(desktopRoot, "*.*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(desktopRoot, file);
            if (ShouldSkipSourceScan(relative) || !IsScannedSourceFile(file))
                continue;

            string text = File.ReadAllText(file);
            foreach (string forbidden in forbiddenTokens)
            {
                if (text.Contains(forbidden, StringComparison.Ordinal))
                    violations.Add($"{relative}: {forbidden}");
            }
        }

        Assert.AreEqual(
            0,
            violations.Count,
            "PCL.Desktop Avalonia sources must not use WPF or legacy UI APIs:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void DesktopNavigation_UsesGeneratedStaticRegistry()
    {
        string desktopRoot = FindDesktopProjectRoot();
        string repoRoot = Directory.GetParent(desktopRoot)?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        string hostSource = File.ReadAllText(Path.Combine(desktopRoot, "Hosting", "DesktopHost.cs"));
        string registrySource = File.ReadAllText(Path.Combine(desktopRoot, "Hosting", "DesktopNavigationRegistry.cs"));
        string generatorSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "PCL.Desktop.SourceGenerators",
            "DesktopNavigationRegistryGenerator.cs"));

        StringAssert.Contains(hostSource, "DesktopNavigationRegistry.RegisterGeneratedHostModules(builder)");
        Assert.IsFalse(hostSource.Contains("BuiltInLaunchModule", StringComparison.Ordinal));
        Assert.IsFalse(hostSource.Contains("BuiltInDownloadModule", StringComparison.Ordinal));
        Assert.AreEqual(4, CountOccurrences(registrySource, "[DesktopNavigationPage("));
        StringAssert.Contains(registrySource, "NavigationRouteId");
        StringAssert.Contains(generatorSource, "new global::PCL.UI.Abstractions.Navigation.NavigationRouteId");
        StringAssert.Contains(generatorSource, "new global::PCL.Application.Hosting.HostModuleId");
        Assert.IsFalse(generatorSource.Contains("StaticHostModule", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DesktopSettings_UsesGeneratedStaticRegistry()
    {
        string desktopRoot = FindDesktopProjectRoot();
        string repoRoot = Directory.GetParent(desktopRoot)?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        string setupLeftSource = File.ReadAllText(Path.Combine(
            desktopRoot,
            "Features",
            "Settings",
            "Views",
            "PageSetupLeft.axaml.cs"));
        string registrySource = File.ReadAllText(Path.Combine(
            desktopRoot,
            "Features",
            "Settings",
            "Views",
            "SetupPageRegistry.cs"));
        string generatorSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "PCL.Desktop.SourceGenerators",
            "SetupPageRegistryGenerator.cs"));

        StringAssert.Contains(setupLeftSource, "SetupPageRegistry.CreatePage(page)");
        Assert.IsFalse(setupLeftSource.Contains("page switch", StringComparison.Ordinal));
        Assert.AreEqual(10, CountOccurrences(registrySource, "[SetupPage("));
        Assert.IsFalse(registrySource.Contains("SetupPageSubType.Plugin", StringComparison.Ordinal));
        StringAssert.Contains(setupLeftSource, "HostSettingsPageFactory.Create(descriptor)");
        StringAssert.Contains(setupLeftSource, "ItemHostSettings_");
        StringAssert.Contains(generatorSource, "SetupPageRegistry.g.cs");
        StringAssert.Contains(generatorSource, "public static partial global::PCL.Desktop.Controls.Legacy.MyPageRight CreatePage");
    }

    [TestMethod]
    public void DesktopInstancePages_UseGeneratedStaticRegistry()
    {
        string desktopRoot = FindDesktopProjectRoot();
        string repoRoot = Directory.GetParent(desktopRoot)?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        string leftSource = File.ReadAllText(Path.Combine(
            desktopRoot,
            "Features",
            "Instances",
            "Views",
            "PageInstanceLeft.axaml.cs"));
        string toolsSource = File.ReadAllText(Path.Combine(
            desktopRoot,
            "Features",
            "Instances",
            "Views",
            "PageInstanceToolsRight.axaml.cs"));
        string resourceSource = File.ReadAllText(Path.Combine(
            desktopRoot,
            "Features",
            "Instances",
            "Views",
            "PageInstanceResourceRight.axaml.cs"));
        string registrySource = File.ReadAllText(Path.Combine(
            desktopRoot,
            "Features",
            "Instances",
            "Views",
            "InstancePageRegistry.cs"));
        string generatorSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "PCL.Desktop.SourceGenerators",
            "InstancePageRegistryGenerator.cs"));

        Assert.IsFalse(leftSource.Contains("Enum.IsDefined(typeof(InstancePageSubType)", StringComparison.Ordinal));
        StringAssert.Contains(leftSource, "InstancePageRegistry.IsDefined");
        StringAssert.Contains(toolsSource, "InstancePageRegistry.UsesGenericFolderPage");
        Assert.IsFalse(toolsSource.Contains("page switch", StringComparison.Ordinal));
        StringAssert.Contains(resourceSource, "InstancePageRegistry.GetResourceKind(page)");
        Assert.IsFalse(resourceSource.Contains("ResourceKindFromPage", StringComparison.Ordinal));
        Assert.AreEqual(12, CountOccurrences(registrySource, "[InstancePage("));
        StringAssert.Contains(generatorSource, "InstancePageRegistry.g.cs");
        StringAssert.Contains(generatorSource, "GetResourceKind");
    }

    [TestMethod]
    public void DesktopDownloadPages_UseGeneratedStaticRegistry()
    {
        string desktopRoot = FindDesktopProjectRoot();
        string repoRoot = Directory.GetParent(desktopRoot)?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        string leftSource = File.ReadAllText(Path.Combine(
            desktopRoot,
            "Features",
            "Downloads",
            "Views",
            "PageDownloadLeft.axaml.cs"));
        string registrySource = File.ReadAllText(Path.Combine(
            desktopRoot,
            "Features",
            "Downloads",
            "Views",
            "DownloadPageRegistry.cs"));
        string generatorSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "PCL.Desktop.SourceGenerators",
            "DownloadPageRegistryGenerator.cs"));

        StringAssert.Contains(leftSource, "DownloadPageRegistry.CreatePage(page, _pageContext)");
        Assert.IsFalse(leftSource.Contains("new PageDownloadInstall", StringComparison.Ordinal));
        Assert.AreEqual(1, CountOccurrences(registrySource, "[DownloadPage("));
        StringAssert.Contains(generatorSource, "DownloadPageRegistry.g.cs");
        StringAssert.Contains(generatorSource, "DownloadPageFactoryContext context");
    }

    [TestMethod]
    public void DesktopLoaderCards_UseSharedStaticRegistry()
    {
        string desktopRoot = FindDesktopProjectRoot();
        string downloadInstallSource = File.ReadAllText(Path.Combine(
            desktopRoot,
            "Features",
            "Downloads",
            "Views",
            "PageDownloadInstall.axaml.cs"));
        string instanceInstallSource = File.ReadAllText(Path.Combine(
            desktopRoot,
            "Features",
            "Instances",
            "Views",
            "PageInstanceInstallRight.axaml.cs"));
        string registrySource = File.ReadAllText(Path.Combine(
            desktopRoot,
            "Features",
            "Shared",
            "MinecraftLoaderCardRegistry.cs"));
        string idSource = File.ReadAllText(Path.Combine(
            desktopRoot,
            "Features",
            "Shared",
            "MinecraftLoaderCardId.cs"));

        StringAssert.Contains(downloadInstallSource, "MinecraftLoaderCardRegistry.AllCards");
        StringAssert.Contains(instanceInstallSource, "MinecraftLoaderCardRegistry.AllCards");
        Assert.IsFalse(downloadInstallSource.Contains("LoaderCardNames", StringComparison.Ordinal));
        Assert.IsFalse(instanceInstallSource.Contains("LoaderCardNames", StringComparison.Ordinal));
        StringAssert.Contains(registrySource, "MinecraftLoaderCardId.LegacyFabricApi");
        StringAssert.Contains(registrySource, "MinecraftLoaderCardId.OptiFabric");
        StringAssert.Contains(registrySource, "ReadOnlySpan<MinecraftLoaderCardDescriptor>");
        StringAssert.Contains(idSource, "readonly record struct MinecraftLoaderCardId");
    }

    [TestMethod]
    public void DesktopVersionSurfacesUseUnifiedMetadata()
    {
        string desktopRoot = FindDesktopProjectRoot();
        string repoRoot = Directory.GetParent(desktopRoot)?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        string csprojSource = File.ReadAllText(Path.Combine(desktopRoot, "PCL.Desktop.csproj"));
        string updatePageSource = File.ReadAllText(Path.Combine(
            desktopRoot,
            "Features",
            "Settings",
            "Views",
            "PageSetupUpdate.axaml.cs"));
        string generatorSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "PCL.Desktop.SourceGenerators",
            "BuildInfoGenerator.cs"));

        StringAssert.Contains(csprojSource, "CompilerVisibleProperty Include=\"InformationalVersion\"");
        StringAssert.Contains(csprojSource, "CompilerVisibleProperty Include=\"Version\"");
        StringAssert.Contains(csprojSource, "EmbeddedResource Include=\"metadata.json\"");
        StringAssert.Contains(updatePageSource, "PclMetadata.Current.DisplayVersion");
        Assert.IsFalse(updatePageSource.Contains("Assembly.GetCustomAttribute", StringComparison.Ordinal));
        Assert.IsFalse(updatePageSource.Contains("Assembly.GetName()", StringComparison.Ordinal));
        StringAssert.Contains(generatorSource, "build_property.");
        StringAssert.Contains(generatorSource, "\"InformationalVersion\"");
        StringAssert.Contains(generatorSource, "PclBuildInfo.g.cs");
    }

    [TestMethod]
    public void ReleaseWorkflowsPublishAvaloniaForEveryDesktopRuntime()
    {
        string desktopRoot = FindDesktopProjectRoot();
        string repoRoot = Directory.GetParent(desktopRoot)?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        string workflowRoot = Path.Combine(repoRoot, ".github", "workflows");
        string reusable = File.ReadAllText(Path.Combine(workflowRoot, "reusable-build.yml"));
        string stable = File.ReadAllText(Path.Combine(workflowRoot, "release-stable_publish.yml"));
        string beta = File.ReadAllText(Path.Combine(workflowRoot, "release-beta_publish.yml"));

        foreach (string runtime in new[] { "win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64" })
        {
            StringAssert.Contains(stable, runtime);
            StringAssert.Contains(beta, runtime);
        }

        foreach (string workflow in new[] { stable, beta })
        {
            StringAssert.Contains(workflow, "SelfContained");
            StringAssert.Contains(workflow, "NoRuntime");
            StringAssert.Contains(workflow, "PCL.Desktop");
        }

        StringAssert.Contains(reusable, "PCL.Desktop/PCL.Desktop.csproj");
        StringAssert.Contains(reusable, "PublishSingleFile=true");
        StringAssert.Contains(reusable, "gh release download --repo MuXue1230-owo/PCL.Plugin");
        StringAssert.Contains(reusable, "PclPluginAssembly");
        StringAssert.Contains(reusable, "PclPluginSdkAssembly");
        StringAssert.Contains(reusable, "PclPluginUiAssembly");
        StringAssert.Contains(reusable, "PclPluginUiAvaloniaAssembly");
        StringAssert.Contains(reusable, "PclPluginBouncyCastleAssembly");
        StringAssert.Contains(reusable, "PclPluginJsonCanonicalizerAssembly");
        StringAssert.Contains(reusable, "PclPluginEs6NumberSerializerAssembly");
        StringAssert.Contains(stable, "include_plugin: true");
        StringAssert.Contains(beta, "include_plugin: true");
        Assert.IsFalse(reusable.Contains("Plain Craft Launcher 2", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DesktopPlugin_IsEmbeddedWithoutJoiningTheSolution()
    {
        string desktopRoot = FindDesktopProjectRoot();
        string repoRoot = Directory.GetParent(desktopRoot)?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        string projectSource = File.ReadAllText(Path.Combine(desktopRoot, "PCL.Desktop.csproj"));
        string loaderSource = File.ReadAllText(Path.Combine(desktopRoot, "Hosting", "EmbeddedRuntimeExtensionLoader.cs"));
        string solutionSource = File.ReadAllText(Path.Combine(repoRoot, "PCL-N.slnx"));

        StringAssert.Contains(projectSource, "PCL.Desktop.Embedded.PCL.Plugin.dll");
        StringAssert.Contains(projectSource, "PCL.Desktop.Embedded.PCL.N.Plugin.Abstractions.dll");
        StringAssert.Contains(projectSource, "PCL.Desktop.Embedded.PCL.N.Plugin.Sdk.dll");
        StringAssert.Contains(projectSource, "PCL.Desktop.Embedded.PCL.N.Plugin.UI.dll");
        StringAssert.Contains(projectSource, "PCL.Desktop.Embedded.PCL.N.Plugin.UI.Avalonia.dll");
        StringAssert.Contains(projectSource, "PCL.Desktop.Embedded.BouncyCastle.Cryptography.dll");
        StringAssert.Contains(projectSource, "PCL.Desktop.Embedded.jsoncanonicalizer.dll");
        StringAssert.Contains(projectSource, "PCL.Desktop.Embedded.es6numberserializer.dll");
        StringAssert.Contains(projectSource, "PublishTrimmed>false");
        StringAssert.Contains(loaderSource, "AssemblyLoadContext.Default.LoadFromStream(buffer)");
        StringAssert.Contains(loaderSource, "if (!HasResource(ResourceName))");
        StringAssert.Contains(loaderSource, "LoadResourceAssembly(AbstractionsResourceName)");
        StringAssert.Contains(loaderSource, "LoadRequiredDependency(SdkResourceName)");
        StringAssert.Contains(loaderSource, "LoadRequiredDependency(BouncyCastleResourceName)");
        StringAssert.Contains(loaderSource, "LoadRequiredDependency(JsonCanonicalizerResourceName)");
        StringAssert.Contains(loaderSource, "LoadRequiredDependency(Es6NumberSerializerResourceName)");
        Assert.IsFalse(solutionSource.Contains("PCL.Plugin", StringComparison.Ordinal));
        Assert.IsFalse(projectSource.Contains("ProjectReference Include=\"../PCL.Plugin", StringComparison.Ordinal));
        Assert.IsFalse(solutionSource.Contains("PCL.Plugin.Host.Abstractions", StringComparison.Ordinal));
        Assert.IsFalse(projectSource.Contains("PCL.Plugin.Host.Abstractions", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PclN_DoesNotImplementThePluginPlatform()
    {
        string desktopRoot = FindDesktopProjectRoot();
        string repoRoot = Directory.GetParent(desktopRoot)?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        string applicationRoot = Path.Combine(repoRoot, "PCL.Application");
        string legacyPlatformRoot = Path.Combine(applicationRoot, "Hosting", "PluginPlatform");

        Assert.IsFalse(Directory.Exists(legacyPlatformRoot) &&
                       Directory.EnumerateFiles(legacyPlatformRoot, "*.cs", SearchOption.AllDirectories).Any(),
            "PCL-N must not contain a plugin platform implementation directory.");

        string[] projectFiles = Directory.EnumerateFiles(repoRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}PCL.Plugin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (string projectFile in projectFiles)
        {
            string project = File.ReadAllText(projectFile);
            Assert.IsFalse(project.Contains("ProjectReference Include=\"../PCL.Plugin", StringComparison.OrdinalIgnoreCase), projectFile);
            Assert.IsFalse(project.Contains("PCL.N.Plugin.Abstractions.csproj", StringComparison.OrdinalIgnoreCase), projectFile);
            Assert.IsFalse(project.Contains("PCL.N.Plugin.Sdk.csproj", StringComparison.OrdinalIgnoreCase), projectFile);
        }

        string[] forbiddenSourceTokens =
        [
            "using PCL.N.Plugin",
            "namespace PCL.Application.Hosting.PluginPlatform",
            "interface IPluginHost",
            "class PclPluginPlatformHost",
            "record PluginSafetySettings",
            "interface IPluginCatalogService",
            "class PluginPlatformBootstrap"
        ];
        foreach (string sourceFile in Directory.EnumerateFiles(repoRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (string.Equals(sourceFile, Path.Combine(repoRoot, "PCL.Desktop.Test", "DesktopArchitectureTests.cs"), StringComparison.OrdinalIgnoreCase) ||
                sourceFile.Contains($"{Path.DirectorySeparatorChar}PCL.Plugin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                sourceFile.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                sourceFile.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string source = File.ReadAllText(sourceFile);
            foreach (string token in forbiddenSourceTokens)
                Assert.IsFalse(source.Contains(token, StringComparison.Ordinal), $"{sourceFile}: {token}");
        }
    }

    [TestMethod]
    public void PortableWorkflow_BuildsTheCurrentSolution()
    {
        string desktopRoot = FindDesktopProjectRoot();
        string repoRoot = Directory.GetParent(desktopRoot)?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        string workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "portable-core.yml"));

        StringAssert.Contains(workflow, "dotnet restore PCL-N.slnx");
        StringAssert.Contains(workflow, "dotnet build PCL-N.slnx");
        Assert.IsFalse(workflow.Contains("PCL.Portable.slnx", StringComparison.Ordinal));
    }

    private static string FindDesktopProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "PCL.Desktop", "PCL.Desktop.csproj");
            if (File.Exists(candidate))
                return Path.Combine(directory.FullName, "PCL.Desktop");

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate PCL.Desktop project root.");
    }

    private static bool IsScannedSourceFile(string file) =>
        file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
        file.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) ||
        file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldSkipSourceScan(string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/');
        return normalized.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}

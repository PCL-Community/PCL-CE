using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Platform;

namespace PCL.Core.Test.Platform;

[TestClass]
public sealed class PlatformModuleShapeTest
{
    [TestMethod]
    public void PlatformModuleExcludesUiServices()
    {
        var names = typeof(PlatformModule)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("FileDialogs", names);
        Assert.DoesNotContain("SystemTheme", names);
        Assert.DoesNotContain("Notifications", names);
    }

    [TestMethod]
    public void UiModuleContainsUiServices()
    {
        var names = typeof(UiModule)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.Contains(nameof(UiModule.FileDialogs), names);
        Assert.Contains(nameof(UiModule.SystemTheme), names);
        Assert.Contains(nameof(UiModule.Notifications), names);
    }

    [TestMethod]
    public void CapabilitiesDoNotDuplicateServiceAbilities()
    {
        var names = typeof(IPlatformCapabilities)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("JavaExecutableName", names);
        Assert.DoesNotContain("SupportsJavaw", names);
        Assert.DoesNotContain("SupportsGpuPreference", names);
        Assert.DoesNotContain("SupportsNativeMenu", names);
        Assert.DoesNotContain("SupportsTaskbarProgress", names);
        Assert.DoesNotContain("SupportsInAppUpdate", names);
    }
}
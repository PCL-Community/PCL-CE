using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;

namespace PCL.Test;

/// <summary>架构边界：业务程序集不得引用 WPF。</summary>
[TestClass]
public sealed class ArchitectureTest
{
    [TestMethod]
    public void LauncherCore_DoesNotReferenceWpf()
    {
        var references = typeof(LauncherCommon)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToList();

        Assert.DoesNotContain("PresentationFramework", references);
        Assert.DoesNotContain("PresentationCore", references);
        Assert.DoesNotContain("WindowsBase", references);
    }

    [TestMethod]
    public void Core_DoesNotReferenceWpf()
    {
        var references = typeof(Basics)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToList();

        Assert.DoesNotContain("PresentationFramework", references);
        Assert.DoesNotContain("PresentationCore", references);
        Assert.DoesNotContain("WindowsBase", references);
    }
}
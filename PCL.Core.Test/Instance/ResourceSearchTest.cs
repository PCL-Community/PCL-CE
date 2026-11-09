using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test.Instance;

[TestClass]
public class ResourceSearchTest
{
    private string _root = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "PCLTest",
            "ResourceSearch",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "Sodium.jar"), "");
        File.WriteAllText(Path.Combine(_root, "JEI.jar"), "");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [TestMethod]
    public void SearchIsCaseInsensitive()
    {
        var vm = new PageInstanceCompResourceViewModel
        {
            SearchText = "sodium"
        };
        var results = vm.FilterBySearch(
        [
            new LocalComp.LocalCompFile(Path.Combine(_root, "Sodium.jar")),
            new LocalComp.LocalCompFile(Path.Combine(_root, "JEI.jar"))
        ]);

        Assert.HasCount(1, results);
        Assert.AreEqual("Sodium.jar", results[0].FileName);
    }

    [TestMethod]
    public void SearchFindsNestedFolderItems()
    {
        var subFolder = Path.Combine(_root, "sub");
        Directory.CreateDirectory(subFolder);
        File.WriteAllText(Path.Combine(subFolder, "NestedMod.jar"), "");

        var vm = new PageInstanceCompResourceViewModel
        {
            RootFolderPath = _root
        };
        vm.Entries.AddRange(
        [
            new LocalComp.LocalCompFile(Path.Combine(_root, "Sodium.jar")),
            new LocalComp.LocalCompFile(Path.Combine(subFolder, "__FOLDER__")),
            new LocalComp.LocalCompFile(Path.Combine(subFolder, "NestedMod.jar"))
        ]);

        var rootEntries = vm.GetListSource();
        Assert.HasCount(2, rootEntries);
        Assert.Contains(entry => entry.IsFolder, rootEntries);

        vm.SearchText = "NestedMod";
        var searchEntries = vm.FilterBySearch(vm.GetListSource());

        Assert.HasCount(1, searchEntries);
        Assert.AreEqual("NestedMod.jar", searchEntries[0].FileName);
    }

    [TestMethod]
    public void SearchMatchesUppercaseQuery()
    {
        var vm = new PageInstanceCompResourceViewModel
        {
            SearchText = "JEI"
        };
        var results = vm.FilterBySearch(
        [
            new LocalComp.LocalCompFile(Path.Combine(_root, "Sodium.jar")),
            new LocalComp.LocalCompFile(Path.Combine(_root, "JEI.jar"))
        ]);

        Assert.HasCount(1, results);
        Assert.AreEqual("JEI.jar", results[0].FileName);
    }
}
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Views.Pages.Download;

namespace PCL.Core.Test.Download;

[TestClass]
public class CompCategoryTagsTest
{
    [TestMethod]
    public void ResourcePack_MatchesWpfPage()
    {
        var tags = CompCategoryTags.For(Comp.CompType.ResourcePack);

        Assert.AreSequenceEqual(
            [
                "Download.Comp.Category.Style",
                "Download.Comp.Category.Feature",
                "Download.Comp.Category.Resolution"
            ],
            [.. tags.Where(tag => tag.IsGroupHeader).Select(tag => tag.TextKey)],
            "分组标题必须与 WPF 的三组一致");

        var items = tags.Where(tag => !tag.IsGroupHeader).ToArray();
        Assert.AreSequenceEqual(
            [
                "403/vanilla-like",
                "400/realistic",
                "401/",
                "402/",
                "399/",
                "/themed",
                "/simplistic",
                "/decoration",
                "/combat",
                "/utility",
                "/tweaks",
                "/cursed",
                "/entities",
                "/audio",
                "5244/fonts",
                "/models",
                "/locale",
                "/gui",
                "/core-shaders",
                "404/",
                "4465/modded",
                "/8x-",
                "393/16x",
                "394/32x",
                "/48x",
                "395/64x",
                "396/128x",
                "397/256x",
                "398/512x+"
            ],
            [.. items.Select(tag => tag.Tag)],
            "资源包分类的 Tag 顺序必须与 WPF 一致，修复前的错位会让筛选返回错误分类");

        Assert.IsTrue(items.All(tag => tag.IsIndented), "分组内的分类项都应缩进");
    }

    [TestMethod]
    public void Shader_MatchesWpfPage()
    {
        var tags = CompCategoryTags.For(Comp.CompType.Shader);

        Assert.AreSequenceEqual(
            [
                "Download.Comp.Category.Style",
                "Download.Comp.Category.Feature",
                "Download.Comp.Category.Performance"
            ],
            [.. tags.Where(tag => tag.IsGroupHeader).Select(tag => tag.TextKey)]);

        Assert.AreSequenceEqual(
            [
                "6555/vanilla-like",
                "6554/fantasy",
                "6553/realistic",
                "/semi-realistic",
                "/cartoon",
                "/colored-lighting",
                "/path-tracing",
                "/pbr",
                "/reflections",
                "/potato",
                "/low",
                "/medium",
                "/high"
            ],
            [.. tags.Where(tag => !tag.IsGroupHeader).Select(tag => tag.Tag)],
            "光影分类的 Tag 顺序必须与 WPF 一致");

        Assert.IsTrue(tags.Where(tag => !tag.IsGroupHeader).All(tag => tag.IsIndented));
    }

    [TestMethod]
    public void RemainingPages_HaveNoGroupHeadersAndUniqueTags()
    {
        foreach (var type in new[]
                 {
                     Comp.CompType.Mod,
                     Comp.CompType.ModPack,
                     Comp.CompType.DataPack,
                     Comp.CompType.World
                 })
        {
            var tags = CompCategoryTags.For(type);
            Assert.DoesNotContain(tag => tag.IsGroupHeader, tags, $"{type} 页面在 WPF 里没有分组标题");
            Assert.IsNotEmpty(tags, $"{type} 分类表不能为空");
            Assert.HasCount(
                tags.Count,
                tags.Select(tag => tag.Tag).Distinct(),
                $"{type} 分类表存在重复 Tag，会导致筛选结果重复");
            Assert.IsTrue(
                tags.All(tag => tag.TextKey.StartsWith(
                    "Download.Comp.Category.",
                    StringComparison.Ordinal)),
                $"{type} 分类名必须走 Download.Comp.Category.* 语言键");
        }
    }
}
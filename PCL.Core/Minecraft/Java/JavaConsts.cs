using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCL.Core.Minecraft.Java;
public class JavaConsts
{
    public static readonly string[] ExcludeFolderNames = ["javapath", "java8path", "common files", "netease"];

    public static readonly string[] MostPossibleKeywords =
    [
        "java", "jdk", "jre",
        "dragonwell", "azul", "zulu", "oracle", "open", "amazon", "corretto",
        "eclipse", "temurin", "hotspot", "semeru", "kona", "bellsoft"
    ];

    public static readonly string[] PossibleKeywords =
    [
        "environment", "env", "runtime", "x86_64", "amd64", "arm64",
        "pcl", "hmcl", "baka", "minecraft"
    ];

    public static readonly string[] AllKeyworkds = [.. PossibleKeywords, .. MostPossibleKeywords];
}

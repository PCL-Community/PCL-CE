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
        "environment", "env", "runtime", "x86_64", "amd64", "arm64", "x64",
        "pcl", "hmcl", "baka", "minecraft"
    ];

    public static readonly string[] AllKeywords = [.. PossibleKeywords, .. MostPossibleKeywords];
}

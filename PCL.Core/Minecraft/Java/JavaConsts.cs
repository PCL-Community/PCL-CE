namespace PCL.Core.Minecraft.Java;
public class JavaConsts
{
    public static readonly string[] ExcludeFolderNames =
    [
        "javapath", "java8path", "common files", "netease",
        "node_modules", ".git", "$recycle.bin", "system volume information"
    ];

    public static readonly string[] MostPossibleKeywords =
    [
        "java", "jdk", "jre",
        "dragonwell", "azul", "zulu", "oracle", "open", "amazon", "corretto",
        "eclipse", "adoptium", "temurin", "hotspot", "semeru", "kona", "bellsoft",
        "liberica", "graal", "mandrel", "jetbrains", "jbr", "sapmachine", "openlogic"
    ];

    public static readonly string[] PossibleKeywords =
    [
        "environment", "env", "runtime", "x86_64", "amd64", "arm64",
        "pcl", "hmcl", "baka", "minecraft"
    ];

    public static readonly string[] AllKeyworkds = [.. PossibleKeywords, .. MostPossibleKeywords];
}

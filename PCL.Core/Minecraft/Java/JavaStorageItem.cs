namespace PCL.Core.Minecraft.Java;

public class JavaStorageItem
{
    public required string Path { get; init; }
    public bool IsEnable { get; init; }
    public JavaSource? Source { get; init; }
}

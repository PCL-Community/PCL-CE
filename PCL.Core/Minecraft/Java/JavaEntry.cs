namespace PCL.Core.Minecraft.Java;

public sealed class JavaEntry
{
    public required JavaInstallation Installation { get; init; }
    public bool IsEnabled { get; set; } = true;
    public JavaSource Source { get; set; } = JavaSource.AutoScanned;

    public override string ToString() =>
        $"{(IsEnabled ? "[✓]" : "[ ]")} {Installation}";
}

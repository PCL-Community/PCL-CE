namespace PCL;

public class MmcPackInfo
{
    public JsonObject additionalJson = new();
    public bool isCleanroomOverrided;
    public bool isFabricOverrided;
    public bool isForgeOverrided;
    public bool isMcArgsEdited;
    public bool isMinecraftOverrided;
    public bool isNeoForgeOverrided;
    public JsonArray jvmArgs = [];
    public JsonArray libraries = [];
    public JsonObject overridedJson = new();
    public string? tweakers = null;
}
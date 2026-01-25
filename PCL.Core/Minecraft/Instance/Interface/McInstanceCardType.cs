namespace PCL.Core.Minecraft.Instance.Interface;


public enum McInstanceCardType {
    Auto, // Used only for forcing automatic instance classification

    // PCL 逻辑版本类型
    Star,
    Custom,
    Hidden,

    // Patchers 类型版本
    Modded,
    NeoForge,
    Fabric,
    Forge,
    Quilt,
    LegacyFabric,
    Cleanroom,
    LiteLoader,

    Client,
    OptiFine,
    LabyMod,

    // 正常 MC 版本类型
    Release,
    Snapshot,
    Fool,
    Old,

    UnknownPatchers,
    
    Error
}


using fNbt;

namespace PCL.Core.Minecraft.Saves.Writers;

/// <summary>
/// 存档数据写入器接口
/// </summary>
public interface ILevelDataWriter
{
    void ModifyAllowCommands(NbtCompound gameLevel, int newValue);
    void ModifyDifficulty(NbtCompound gameLevel, int newDifficulty, bool? newLocked);
}
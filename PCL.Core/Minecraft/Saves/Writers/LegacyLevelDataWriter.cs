using fNbt;

namespace PCL.Core.Minecraft.Saves.Writers;

/// <summary>
/// 旧格式写入器
/// </summary>
public class LegacyLevelDataWriter : ILevelDataWriter
{
    public void ModifyAllowCommands(NbtCompound gameLevel, int newValue)
    {
        var allowCmdTag = gameLevel.Get<NbtByte>("allowCommands");
        if (allowCmdTag != null)
            allowCmdTag.Value = (byte)newValue;
    }

    public void ModifyDifficulty(NbtCompound gameLevel, int newDifficulty, bool? newLocked)
    {
        var difficultyTag = gameLevel.Get<NbtByte>("Difficulty");
        if (difficultyTag != null)
            difficultyTag.Value = (byte)newDifficulty;

        if (newLocked.HasValue)
        {
            if (gameLevel.Contains("DifficultyLocked"))
            {
                var lockedTag = gameLevel.Get<NbtByte>("DifficultyLocked");
                if (lockedTag != null)
                    lockedTag.Value = (byte)(newLocked.Value ? 1 : 0);
            }
            else if (newLocked.Value)
            {
                gameLevel.Add(new NbtByte("DifficultyLocked", 1));
            }
        }
    }
}
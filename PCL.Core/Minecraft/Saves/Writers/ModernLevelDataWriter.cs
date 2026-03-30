using fNbt;

namespace PCL.Core.Minecraft.Saves.Writers;

/// <summary>
/// 新格式写入器
/// </summary>
public class ModernLevelDataWriter : ILevelDataWriter
{
    public void ModifyAllowCommands(NbtCompound gameLevel, int newValue)
    {
        var allowCmdTag = gameLevel.Get<NbtByte>("allowCommands");
        if (allowCmdTag != null)
            allowCmdTag.Value = (byte)newValue;
    }

    public void ModifyDifficulty(NbtCompound gameLevel, int newDifficulty, bool? newLocked)
    {
        var difficultySettings = gameLevel.Get<NbtCompound>("difficulty_settings");
        if (difficultySettings == null)
        {
            difficultySettings = new NbtCompound("difficulty_settings");
            gameLevel.Add(difficultySettings);
        }

        string difficultyStr = newDifficulty switch
        {
            0 => "peaceful",
            1 => "easy",
            2 => "normal",
            3 => "hard",
            _ => "normal"
        };

        var difficultyTag = difficultySettings.Get<NbtString>("difficulty");
        if (difficultyTag != null)
            difficultyTag.Value = difficultyStr;
        else
            difficultySettings.Add(new NbtString("difficulty", difficultyStr));

        if (newLocked.HasValue)
        {
            if (difficultySettings.Contains("locked"))
            {
                var lockedTag = difficultySettings.Get<NbtByte>("locked");
                if (lockedTag != null)
                    lockedTag.Value = (byte)(newLocked.Value ? 1 : 0);
            }
            else if (newLocked.Value)
            {
                difficultySettings.Add(new NbtByte("locked", 1));
            }
        }
    }
}
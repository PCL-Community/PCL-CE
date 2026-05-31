using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Minecraft.Saves.Editing;

/// <summary>
/// 存档编辑器接口 —— 负责将修改写入 level.dat。
/// 实现类需声明自己支持的 DataVersion 范围。
/// </summary>
public interface ISaveEditor
{
    /// <summary>返回此编辑器能否处理指定 DataVersion 的存档。</summary>
    bool CanHandle(int? dataVersion);

    /// <summary>
    /// 将 <paramref name="changes"/> 中的修改写入 <paramref name="levelDatPath"/> 所指向的 level.dat 文件。
    /// 返回 true 表示至少有一项修改被成功写入。
    /// </summary>
    Task<bool> ApplyChangesAsync(string levelDatPath, SaveChanges changes, CancellationToken ct);
}

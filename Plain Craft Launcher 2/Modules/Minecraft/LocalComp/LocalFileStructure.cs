using System.IO;
using fNbt;

namespace PCL;

public static partial class LocalComp
{
    public partial class LocalCompFile
    {
        /// <summary>
        ///     读取 Litematic 文件的 NBT 数据。
        /// </summary>
        private void LoadLitematicNbtData()
        {
            try
            {
                Common.Log($"开始读取 Litematic NBT 数据：{path}", Common.LogLevel.Debug);
                var scheNbt = _ReadNbtFile();
                if (scheNbt.RootTag.Get("Version") is NbtInt versionTag)
                    _litematicVersion = versionTag.Value;

                var metadataTag = scheNbt.RootTag.Get<NbtCompound>("Metadata");
                if (metadataTag is not null)
                {
                    Common.Log("找到 Litematic Metadata 节点", Common.LogLevel.Debug);
                    _ApplyLitematicMetadata(metadataTag);
                }
                else
                {
                    Common.Log("未找到 Litematic Metadata 节点", Common.LogLevel.Debug);
                }

                Common.Log("Litematic NBT 数据读取完成", Common.LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"读取 Litematic NBT 数据时出错（{path}）");
            }
        }

        private void _ApplyLitematicMetadata(NbtCompound metadata)
        {
            if (metadata.Get<NbtString>("Name") is { } nameTag
                && !string.IsNullOrWhiteSpace(nameTag.Value)
                && nameTag.Value != "Unnamed")
                _litematicOriginalName = nameTag.Value;

            if (metadata.Get<NbtString>("Description") is { } descriptionTag
                && !string.IsNullOrWhiteSpace(descriptionTag.Value))
                _description = descriptionTag.Value;

            if (metadata.Get<NbtString>("Author") is { } authorTag
                && !string.IsNullOrWhiteSpace(authorTag.Value))
                _authors = authorTag.Value;

            if (metadata.Get<NbtLong>("TimeCreated") is { } timeCreatedTag)
                _litematicTimeCreated = timeCreatedTag.Value;
            if (metadata.Get<NbtLong>("TimeModified") is { } timeModifiedTag)
                _litematicTimeModified = timeModifiedTag.Value;

            _ApplyLitematicEnclosingSize(metadata);

            if (metadata.Get<NbtInt>("RegionCount") is { } regionCountTag)
                _litematicRegionCount = regionCountTag.Value;
            if (metadata.Get<NbtInt>("TotalBlocks") is { } totalBlocksTag)
                _litematicTotalBlocks = totalBlocksTag.Value;
            if (metadata.Get<NbtInt>("TotalVolume") is { } totalVolumeTag)
                _litematicTotalVolume = totalVolumeTag.Value;
        }

        private void _ApplyLitematicEnclosingSize(NbtCompound metadata)
        {
            var enclosingSizeTag = metadata.Get<NbtCompound>("EnclosingSize");
            if (enclosingSizeTag is null)
                return;

            var xTag = enclosingSizeTag.Get<NbtInt>("x");
            var yTag = enclosingSizeTag.Get<NbtInt>("y");
            var zTag = enclosingSizeTag.Get<NbtInt>("z");
            if (xTag is not null && yTag is not null && zTag is not null)
                _litematicEnclosingSize = $"{xTag.Value} × {yTag.Value} × {zTag.Value}";
        }

        /// <summary>
        ///     读取 .schem 文件的 NBT 数据（Sponge Schematic 格式）。
        /// </summary>
        private void LoadSchemNbtData()
        {
            try
            {
                Common.Log($"开始读取 Schem NBT 数据：{path}", Common.LogLevel.Debug);
                var scheNbt = _ReadNbtFile();
                _ApplySchemRoot(scheNbt.RootTag);
                Common.Log("Schem NBT 数据读取完成", Common.LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"读取 Schem NBT 数据时出错（{path}）");
            }
        }

        private void _ApplySchemRoot(NbtCompound root)
        {
            if (root.Get<NbtInt>("Version") is { } versionTag)
                _spongeVersion = versionTag.Value;
            if (root.Get<NbtInt>("DataVersion") is { } dataVersionTag)
                _structureDataVersion = dataVersionTag.Value;

            var widthTag = root.Get<NbtShort>("Width");
            var heightTag = root.Get<NbtShort>("Height");
            var lengthTag = root.Get<NbtShort>("Length");
            if (widthTag is not null && heightTag is not null && lengthTag is not null)
            {
                _ApplyDimensions(widthTag.Value, heightTag.Value, lengthTag.Value);
                _litematicTotalBlocks = _litematicTotalVolume;
            }

            if (root.Get<NbtCompound>("Palette") is not null)
                _litematicRegionCount = 1;

            var metadataTag = root.Get<NbtCompound>("Metadata");
            if (metadataTag is null)
                return;

            if (metadataTag.Get<NbtString>("Name") is { } nameTag
                && !string.IsNullOrWhiteSpace(nameTag.Value))
                _schemOriginalName = nameTag.Value;

            if (metadataTag.Get<NbtString>("Author") is { } authorTag
                && !string.IsNullOrWhiteSpace(authorTag.Value))
            {
                _structureAuthor = authorTag.Value;
                _authors ??= _structureAuthor;
            }
        }

        /// <summary>
        ///     读取 .schematic 文件的 NBT 数据（MCEdit/WorldEdit 格式）。
        /// </summary>
        private void LoadSchematicNbtData()
        {
            try
            {
                Common.Log($"开始读取 Schematic NBT 数据：{path}", Common.LogLevel.Debug);
                var scheNbt = _ReadNbtFile();
                _ApplySchematicRoot(scheNbt.RootTag);
                Common.Log("Schematic NBT 数据读取完成", Common.LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"读取 Schematic NBT 数据时出错（{path}）");
            }
        }

        private void _ApplySchematicRoot(NbtCompound root)
        {
            var widthTag = root.Get<NbtShort>("Width");
            var heightTag = root.Get<NbtShort>("Height");
            var lengthTag = root.Get<NbtShort>("Length");
            if (widthTag is not null
                && heightTag is not null
                && lengthTag is not null)
                _ApplyDimensions(widthTag.Value, heightTag.Value, lengthTag.Value);

            if (root.Get<NbtString>("Materials") is { } materialsTag)
                Common.Log($"Schematic 材料类型：{materialsTag.Value}", Common.LogLevel.Debug);
        }

        /// <summary>
        ///     读取 .nbt 文件的 NBT 数据（Minecraft 结构文件格式）。
        /// </summary>
        private void LoadStructureNbtData()
        {
            try
            {
                Common.Log($"开始读取 NBT 结构文件数据：{path}", Common.LogLevel.Debug);
                var scheNbt = _ReadNbtFile();
                _ApplyStructureRoot(scheNbt.RootTag);
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"读取 NBT 结构文件数据时出错（{path}）");
            }
        }

        private void _ApplyStructureRoot(NbtCompound root)
        {
            if (root.Get<NbtString>("author") is { } authorTag
                && !string.IsNullOrWhiteSpace(authorTag.Value))
            {
                _structureAuthor = authorTag.Value;
                _authors ??= _structureAuthor;
            }

            if (root.Get<NbtList>("size") is { } sizeTag
                && sizeTag.ToArray() is [var x, var y, var z, ..])
            {
                _litematicEnclosingSize = $"{x.IntValue} × {y.IntValue} × {z.IntValue}";
                _litematicTotalVolume = x.IntValue * y.IntValue * z.IntValue;
            }

            if (root.Get<NbtList>("blocks") is { } blocksTag)
                _litematicTotalBlocks =
                    blocksTag.Count(nbtTag => nbtTag.TagType == NbtTagType.Compound);

            if (root.Get<NbtList>("palette") is not null)
                _litematicRegionCount = 1;
        }

        private NbtFile _ReadNbtFile()
        {
            using var fs = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            var nbt = new NbtFile();
            nbt.LoadFromStream(fs, NbtCompression.AutoDetect);
            return nbt;
        }

        private void _ApplyDimensions(short width, short height, short length)
        {
            _litematicEnclosingSize = $"{width} × {height} × {length}";
            _litematicTotalVolume = (short)(width * height) * length;
        }
    }
}
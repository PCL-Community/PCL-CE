using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.IO;

namespace PCL;

public sealed record InstanceRenameResult(
    string OldName,
    string NewName,
    string OldPath,
    string NewPath,
    McInstance Instance);

public static class InstanceRenameService
{
    public static InstanceRenameResult Rename(
        McInstance instance,
        string newName,
        string minecraftFolder,
        Func<string, McInstance>? loadInstance = null,
        Action<string>? invalidateCache = null)
    {
        var oldName = instance.Name;
        var oldPath = instance.PathInstance;
        var versionsFolder = Path.Combine(minecraftFolder, "versions");
        var newPath = Path.Combine(versionsFolder, newName);
        var tempPath = Path.Combine(versionsFolder, $"{newName}_temp");
        var isCaseChangedOnly = string.Equals(
            newName.ToLowerInvariant(),
            oldName.ToLowerInvariant(),
            StringComparison.Ordinal);

        if (!isCaseChangedOnly && Directory.Exists(newPath))
            throw new IOException($"目标实例目录已存在：{newPath}");

        JsonObject jsonObject;
        try
        {
            var jsonPath = Path.Combine(oldPath, $"{oldName}.json");
            jsonObject = JsonNode.Parse(Files.ReadAllTextOrEmpty(jsonPath)) as JsonObject
                ?? (JsonObject)instance.JsonObject.DeepClone();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "重命名读取 Json 时失败", LauncherLogLevel.Developer);
            jsonObject = (JsonObject)instance.JsonObject.DeepClone();
        }

        var invalidate = invalidateCache ?? InvalidateConfigCache;
        _Invalidate(invalidate, oldPath);

        Directory.Move(oldPath, tempPath);
        try
        {
            Directory.Move(tempPath, newPath);
        }
        catch
        {
            try
            {
                Directory.Move(tempPath, oldPath);
            }
            catch
            {
            }

            throw;
        }

        var oldNatives = Path.Combine(newPath, $"{oldName}-natives");
        if (Directory.Exists(oldNatives))
        {
            var newNatives = Path.Combine(newPath, $"{newName}-natives");
            if (isCaseChangedOnly)
            {
                var middle = Path.Combine(newPath, $"{oldName}natives_temp");
                Directory.Move(oldNatives, middle);
                Directory.Move(middle, newNatives);
            }
            else
            {
                if (Directory.Exists(newNatives))
                    Directory.Delete(newNatives, true);
                Directory.Move(oldNatives, newNatives);
            }
        }

        var oldJar = Path.Combine(newPath, $"{oldName}.jar");
        if (File.Exists(oldJar))
        {
            var newJar = Path.Combine(newPath, $"{newName}.jar");
            if (isCaseChangedOnly)
            {
                var middle = Path.Combine(newPath, $"{oldName}_temp.jar");
                File.Move(oldJar, middle);
                File.Move(middle, newJar);
            }
            else
            {
                if (File.Exists(newJar))
                    File.Delete(newJar);
                File.Move(oldJar, newJar);
            }
        }

        var setupIni = Path.Combine(newPath, "PCL", "Setup.ini");
        if (File.Exists(setupIni))
            Files.WriteAllText(
                setupIni,
                Files.ReadAllTextOrEmpty(setupIni).Replace(oldPath, newPath));

        var instanceIni = Path.Combine(minecraftFolder, "PCL.ini");
        if ((Ini.Read(instanceIni, "Version") ?? string.Empty) == oldName)
            Ini.Write(instanceIni, "Version", newName);

        try
        {
            jsonObject["id"] = newName;
            Files.WriteAllText(
                Path.Combine(newPath, $"{newName}.json"),
                jsonObject.ToString());
            if (!isCaseChangedOnly)
            {
                var oldJson = Path.Combine(newPath, $"{oldName}.json");
                if (File.Exists(oldJson))
                    File.Delete(oldJson);
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "重命名实例 Json 失败", LauncherLogLevel.Developer);
        }

        _Invalidate(invalidate, oldPath);
        _Invalidate(invalidate, newPath);

        var reloaded = loadInstance?.Invoke(newPath) ?? new McInstance(newPath).Load();
        if (InstanceList.McMcInstanceSelected is { } selected
            && selected.PathInstance == oldPath)
        {
            InstanceList.McMcInstanceSelected = reloaded;
            States.Game.SelectedInstance = newName;
        }

        return new InstanceRenameResult(oldName, newName, oldPath, newPath, reloaded);
    }

    public static void InvalidateConfigCache(string path)
    {
        try
        {
            if (ConfigService.GetProvider(ConfigSource.GameInstance) is
                DynamicCacheConfigStorage storage)
                storage.InvalidateCache(path);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "清理实例配置缓存失败", LauncherLogLevel.Developer);
        }
    }

    private static void _Invalidate(Action<string> invalidate, string path)
    {
        try
        {
            invalidate(path);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "清理实例配置缓存失败", LauncherLogLevel.Developer);
        }
    }
}
using System;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json.Nodes;
using ICSharpCode.SharpZipLib.Zip;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Exceptions;
using PCL.Core.IO;
using PCL.Core.Utils.Exts;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.Instance.Utils;

public class ForgeInstaller
{
    public required string JarPath;
    public required string JavaPath;
    public required Version ForgeVersion;

    public required string InstancePath;

    public required string MinecraftBaseFolder;

    private ZipFile? _zipFile;
    private JsonNode? _installerProfile;

    private async Task<JsonNode?> _GetInstallerProfileAsync()
    {
        if(_installerProfile is not null) return _installerProfile;
        if(_zipFile is null) _zipFile = new ZipFile(File.OpenRead(JarPath));
        var profileEntry = _zipFile.GetEntry("installer_profile.json");
        if(profileEntry is null) throw new ForgeInstallerException("找不到安装配置文件");
        _installerProfile = await JsonNode.ParseAsync(_zipFile.GetInputStream(profileEntry));
        return _installerProfile;
    }

    private ForgeInstallImplVersion _SelectImplVersion()
    {
        if(ForgeVersion.Major > 20) return ForgeInstallImplVersion.Latest;
        if(_GetInstallerProfileAsync().GetAwaiter().GetResult()?["install"] is not null)
            return ForgeInstallImplVersion.Version1;
        return ForgeInstallImplVersion.Version2;
    }

    /// <summary>
    /// 开始 Forge 安装（自动选择安装方式）
    /// </summary>
    public async Task StartInstallAsync()
    {
        var installerProfile = await _GetInstallerProfileAsync();
        if (installerProfile is null) throw new ForgeInstallerException("未找到安装配置文件");
        await StartInstallAsync(_SelectImplVersion());
    }
    /// <summary>
    /// 用于指定 Forge 安装实现的重载方法（仅用于自动检测无法确定合适的安装方法时使用）
    /// </summary>
    /// <param name="version">实现版本</param>
    public async Task StartInstallAsync(ForgeInstallImplVersion version)
    {
        switch(version){
            case ForgeInstallImplVersion.Version1:
                LogWrapper.Debug("Installer","选择的实现版本：Legacy");
                await _OldInstallMethod1Async();
                break;
            case ForgeInstallImplVersion.Version2:
                LogWrapper.Debug("Installer","选择的实现版本：Middle");
                await _OldInstallMethod2Async();
                break;
            default:
                LogWrapper.Debug("Installer","选择的实现版本：Latest");
                await _NewInstallAsync();
                break;
        }
    }

    #region "旧版本 Forge 安装实现（版本 1）"

    private async Task _OldInstallMethod1Async()
    {
        
    }

    #endregion

    #region "旧版本 Forge 安装实现（版本 2）"

    private async Task _OldInstallMethod2Async()
    {
        var libraryPath = Path.Combine(MinecraftBaseFolder,"libraries");
        var profile = await _GetInstallerProfileAsync();
        var jsonPath = profile?["json"]?.ToString().TrimStart('/');
        using var jsonEntryStream = _zipFile!.GetInputStream(_zipFile.GetEntry(jsonPath));
        using var reader = new StreamReader(jsonEntryStream);
        var json = JsonNode.Parse(await reader.ReadToEndAsync());
        var instanceName = Path.GetDirectoryName(InstancePath);
        json?["id"] = instanceName;
        File.WriteAllText(Path.Combine(InstancePath,$"{instanceName}.json"),json?.ToJsonString());
        var libraryCount = 0;
        foreach(ZipEntry entry in _zipFile)
        {
            if(entry.Name.StartsWith("/maven"))
            {
                var targetPath = Path.Combine(libraryPath, entry.Name);
                if(entry.IsDirectory) {
                    Directory.CreateDirectory(targetPath);
                    continue;
                }
                libraryCount++;
                using var fs = File.OpenWrite(targetPath);
                using var zipStream = _zipFile.GetInputStream(entry);
                var task = zipStream?.CopyToAsync(fs) ?? throw new NullReferenceException();
                await task;
            }
        }
        LogWrapper.Debug("Installer",$"已解压 {libraryCount} 个文件。");
    }

    #endregion

    #region "新版本 Forge 安装实现"

    private List<string> _CollectProcessor()
    {
        return [];
    }

    private async Task _NewInstallAsync()
    {
        
    }    

    #endregion    

}

public enum ForgeInstallImplVersion
{
    Version1,
    Version2,
    Latest
}
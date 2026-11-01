import AppKit
import Foundation
import UniformTypeIdentifiers
import Darwin
import PCLCore

@MainActor extension LauncherStore {
    var versionSettingsTarget: InstalledVersion? {
        installedVersions.first { $0.id == versionSettingsTargetID }
    }

    var installNameError: String? {
        guard currentRemoteSelection != nil else { return nil }
        do {
            try MinecraftInstaller.validateProfileID(installNameDraft)
            let json = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(installNameDraft)/\(installNameDraft).json", under: gameDirectory)
            if FileManager.default.fileExists(atPath: json.path) { return "该版本名称已存在，请修改版本名！" }
            return nil
        } catch { return error.localizedDescription }
    }

    func selectRemoteVersion(_ version: RemoteVersion) {
        guard !isBusy, !isGameRunning else { return }
        currentRemoteSelection = version
        installNameDraft = version.id
        resetModdedSelection(for: version)
        page = .downloads
    }

    func backToDownloadList() { currentRemoteSelection = nil; resetModdedSelection() }

    func installSelectedVersion() {
        guard let version = currentRemoteSelection, !isBusy, !isGameRunning else { return }
        if let error = installNameError { errorMessage = error; return }
        if moddedState.selectedLoader != nil || moddedState.selectedOptiFine != nil || moddedState.selectedLiteLoader != nil || moddedState.selectedOptiFabric != nil {
            installSelectedModdedVersion(); return
        }
        let name = installNameDraft
        currentRemoteSelection = nil
        resetModdedSelection()
        install(version, profileID: name)
        page = .activity
    }

    func selectVersion(_ version: InstalledVersion) {
        guard !isBusy, !isGameRunning else { return }
        preferences.selectedVersion = version.id
        versionSettingsTargetID = nil
        showHiddenVersions = false
        page = .launch
    }

    func remoteVersionID(for version: InstalledVersion) -> String {
        originalVersionIDs[version.id] ?? version.id
    }

    func versionDisplayName(for version: InstalledVersion) -> String {
        let minecraft = remoteVersionID(for: version)
        let lower = minecraft.lowercased()
        let base: String
        if version.type == "special" { base = "愚人节版本 \(minecraft)" }
        else if version.type == "old_alpha" || version.type == "old_beta" { base = "远古版本" }
        else if lower.contains("pre") { base = "预发布版 \(minecraft)" }
        else if lower.contains("rc") { base = "发布候选 \(minecraft)" }
        else if lower.contains("experimental") { base = "实验性快照\(minecraft)" }
        else if lower == "pending" { base = "实验性快照" }
        else if version.type == "snapshot" { base = "快照版 \(minecraft.replacingOccurrences(of: "-snapshot", with: ""))" }
        else { base = "正式版 \(minecraft)" }
        var components = [base]
        if let loader = version.modLoader { components.append("\(loader.kind.displayName) \(loader.version)") }
        if let lite = version.liteLoaderVersion { components.append("LiteLoader \(lite)") }
        if let opti = version.optiFineVersion { components.append("OptiFine \(opti.replacingOccurrences(of: "HD_U_", with: ""))") }
        return components.joined(separator: ", ")
    }

    func defaultVersionIcon(for version: InstalledVersion) -> String {
        switch version.modLoader?.kind {
        case .fabric, .quilt: return "Fabric"
        case .forge: return "Anvil"
        case .neoForge: return "NeoForge"
        case nil:
            if version.liteLoaderVersion != nil { return "Egg" }
            if version.optiFineVersion != nil { return "GrassPath" }
            switch version.type {
            case "snapshot": return "CommandBlock"
            case "old_alpha", "old_beta": return "CobbleStone"
            case "special": return "GoldBlock"
            default: return "Grass"
            }
        }
    }

    func profileSettings(for version: InstalledVersion) -> VersionProfileSettings {
        versionProfileCache[version.id] ?? (try? VersionSettingsRepository.load(version: version)) ?? VersionProfileSettings()
    }

    func effectiveJavaPath(for version: InstalledVersion) -> String {
        let profile = profileSettings(for: version)
        switch profile.javaConfigurationMode {
        case "global": return preferences.javaPath
        case "custom": return profile.javaPath
        default: return ""
        }
    }

    var versionAvailableMemoryMB: Int {
        var statistics = vm_statistics64()
        var count = mach_msg_type_number_t(MemoryLayout<vm_statistics64>.size / MemoryLayout<integer_t>.size)
        let host = mach_host_self()
        defer { mach_port_deallocate(mach_task_self_, host) }
        let status = withUnsafeMutablePointer(to: &statistics) { pointer in
            pointer.withMemoryRebound(to: integer_t.self, capacity: Int(count)) {
                host_statistics64(host, HOST_VM_INFO64, $0, &count)
            }
        }
        var pageSize: vm_size_t = 0
        guard status == KERN_SUCCESS, host_page_size(host, &pageSize) == KERN_SUCCESS else { return memoryLimit }
        let reclaimable = Int((UInt64(statistics.free_count) + UInt64(statistics.inactive_count)) * UInt64(pageSize) / 1_048_576)
        return max(512, min(Int(ProcessInfo.processInfo.physicalMemory / 1_048_576), reclaimable))
    }

    func effectiveVersionMemoryMB(for version: InstalledVersion, profile: VersionProfileSettings? = nil) -> Int {
        let settings = profile ?? profileSettings(for: version)
        let runtime = settings.isolateGameDirectory ? version.directory : gameDirectory
        let mods = (try? FileManager.default.contentsOfDirectory(at: runtime.appendingPathComponent("mods"), includingPropertiesForKeys: nil)) ?? []
        return settings.resolvedMemoryMB(globalValue: preferences.memoryMB, availableMemoryMB: versionAvailableMemoryMB,
                                         modCount: mods.filter { ["jar", "litemod"].contains($0.pathExtension.lowercased()) }.count)
    }

    func customVersionIcon(for version: InstalledVersion, iconName: String? = nil) -> NSImage? {
        guard (iconName ?? profileSettings(for: version).iconName) == "PCL/Logo.png",
              let url = try? MinecraftVersionMetadata.safeFile(relativePath: "PCL/Logo.png", under: version.directory) else { return nil }
        return NSImage(contentsOf: url)
    }

    func chooseVersionIcon() {
        guard let version = versionSettingsTarget, !isBusy, !isGameRunning else { return }
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.png, .jpeg]
        panel.canChooseDirectories = false; panel.allowsMultipleSelection = false
        panel.message = "选择自定义版本图标"
        guard panel.runModal() == .OK, let source = panel.url else { return }
        do {
            let data = try Data(contentsOf: source)
            guard data.count <= 32 * 1024 * 1024, let image = NSBitmapImageRep(data: data),
                  image.pixelsWide > 0, image.pixelsHigh > 0, image.pixelsWide <= 16384, image.pixelsHigh <= 16384,
                  let png = image.representation(using: .png, properties: [:]) else { throw settingsError("请选择有效的 PNG 或 JPEG 图片。") }
            let raw = version.directory.appendingPathComponent("PCL/Logo.png")
            guard (try? FileManager.default.destinationOfSymbolicLink(atPath: raw.path)) == nil else { throw settingsError("自定义图标不能是符号链接。") }
            let destination = try MinecraftVersionMetadata.safeFile(relativePath: "PCL/Logo.png", under: version.directory)
            try FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
            try png.write(to: destination, options: .atomic)
            versionSettingsDraft.iconName = "PCL/Logo.png"
            saveVersionSettings()
        } catch { report(error) }
    }

    func openVersionSettings(_ version: InstalledVersion) {
        guard !isBusy, !isGameRunning else { return }
        if page != .versionSettings { versionSettingsReturnPage = page == .library ? .library : .launch }
        hydratingVersionSettings = true
        versionSettingsTargetID = version.id
        versionSettingsTab = .overview
        profileSaveError = nil
        versionWindowInputError = nil
        do { versionSettingsDraft = try VersionSettingsRepository.load(version: version) }
        catch { versionSettingsDraft = profileSettings(for: version); profileSaveError = error.localizedDescription }
        hydratingVersionSettings = false
        page = .versionSettings
    }

    func closeVersionSettings() {
        guard versionWindowInputError == nil else { return }
        saveVersionSettings()
        guard profileSaveError == nil else { return }
        page = versionSettingsReturnPage
        versionSettingsTargetID = nil
    }

    func saveVersionSettings() {
        guard !hydratingVersionSettings, let version = versionSettingsTarget else { return }
        guard !isBusy, !isGameRunning else { return }
        if versionProfileCache[version.id] == versionSettingsDraft { profileSaveError = nil; return }
        do {
            try VersionSettingsRepository.save(versionSettingsDraft, for: version)
            versionProfileCache[version.id] = versionSettingsDraft
            profileSaveError = nil
            statusMessage = "版本设置已保存"
        } catch { profileSaveError = error.localizedDescription }
    }

    func resetVersionSettings() {
        guard let version = versionSettingsTarget, !isBusy, !isGameRunning else { return }
        let defaults = VersionProfileSettings()
        versionWindowInputError = nil
        // The original settings reset affects launch options; display metadata stays intact.
        var profile = profileSettings(for: version)
        profile.useGlobalLaunchSettings = defaults.useGlobalLaunchSettings
        profile.memoryMB = defaults.memoryMB
        profile.javaPath = defaults.javaPath
        profile.extraJVMArguments = defaults.extraJVMArguments
        profile.isolateGameDirectory = defaults.isolateGameDirectory
        profile.width = defaults.width
        profile.height = defaults.height
        profile.memoryMode = defaults.memoryMode
        profile.javaMode = defaults.javaMode
        profile.jvmArgumentsMode = defaults.jvmArgumentsMode
        profile.windowMode = defaults.windowMode
        profile.javaVersionRange = defaults.javaVersionRange
        profile.extraGameArguments = defaults.extraGameArguments
        profile.customInfo = defaults.customInfo
        profile.serverAddress = defaults.serverAddress
        profile.garbageCollector = defaults.garbageCollector
        versionSettingsDraft = profile
        saveVersionSettings()
    }

    func requestResetVersionSettings() {
        guard versionSettingsTarget != nil, !isBusy, !isGameRunning else { return }
        let alert = NSAlert()
        alert.messageText = "初始化确认"
        alert.informativeText = "是否要初始化该版本的版本独立设置？该操作不可撤销。"
        alert.addButton(withTitle: "确定"); alert.addButton(withTitle: "取消")
        guard alert.runModal() == .alertFirstButtonReturn else { return }
        resetVersionSettings()
        versionSettingsTab = .settings
    }

    func launchConfiguration(for version: InstalledVersion, java suppliedJava: JavaRuntime? = nil) throws -> LaunchConfiguration {
        if page == .versionSettings && versionSettingsTargetID == version.id {
            if let error = versionWindowInputError { throw settingsError(error) }
            saveVersionSettings()
            if let error = profileSaveError { throw settingsError(error) }
        }
        let profile = profileSettings(for: version)
        try profile.validate()
        let java = try VersionJavaSelection(version: version, profile: profile, globalJavaPath: preferences.javaPath,
                                            runtimes: javaRuntimes).resolve(fallback: suppliedJava)
        let player = try OfflineLogin.validateName(preferences.playerName, versionID: remoteVersionID(for: version))
        let independentWindow = profile.windowConfigurationMode == "custom"
        return LaunchConfiguration(gameDirectory: gameDirectory, versionID: version.id, playerName: player,
            javaExecutable: java.executable, memoryMB: effectiveVersionMemoryMB(for: version, profile: profile),
            width: independentWindow ? profile.width : 1100, height: independentWindow ? profile.height : 700,
            extraJVMArguments: profile.jvmConfigurationMode == "global" ? preferences.extraJVMArguments : profile.extraJVMArguments,
            offlineSkinModel: offlineSkinModel, runtimeGameDirectory: profile.isolateGameDirectory ? version.directory : nil,
            extraGameArguments: profile.extraGameArguments, serverAddress: profile.serverAddress, customInfo: profile.customInfo,
            garbageCollector: profile.garbageCollector, javaMajorVersion: java.majorVersion)
    }

    func chooseVersionJava() {
        guard versionSettingsTarget != nil, !isBusy, !isGameRunning else { return }
        let panel = NSOpenPanel()
        panel.canChooseFiles = true; panel.canChooseDirectories = false; panel.showsHiddenFiles = true
        panel.message = "选择此版本使用的 bin/java 可执行文件"
        panel.directoryURL = URL(fileURLWithPath: "/Library/Java/JavaVirtualMachines")
        guard panel.runModal() == .OK, let url = panel.url else { return }
        guard let java = JavaDiscovery.inspect(executable: url) else { profileSaveError = "此文件不是可用的 Java。"; return }
        if !javaRuntimes.contains(where: { $0.id == java.id }) { javaRuntimes.append(java) }
        versionSettingsDraft.javaPath = java.executable.path
        versionSettingsDraft.javaMode = "custom"
        saveVersionSettings()
    }

    func renameVersion() {
        guard let version = versionSettingsTarget, !isBusy, !isGameRunning else { return }
        guard let name = prompt(title: "修改版本名", message: "输入新的版本名称。版本文件夹、JSON 和 JAR 会同时重命名。", value: version.id) else { return }
        do {
            saveVersionSettings()
            if let error = profileSaveError { throw settingsError(error) }
            let newID = try VersionManager.rename(version: version, to: name, gameDirectory: gameDirectory)
            if preferences.selectedVersion == version.id { preferences.selectedVersion = newID }
            versionSettingsTargetID = nil
            Task {
                await refreshLocal()
                if let renamed = installedVersions.first(where: { $0.id == newID }) { openVersionSettings(renamed) }
            }
        } catch { report(error) }
    }

    func editVersionDescription() {
        guard versionSettingsTarget != nil, !isBusy, !isGameRunning else { return }
        guard let text = prompt(title: "修改版本描述", message: "该描述将显示在版本列表中。", value: versionSettingsDraft.displayDescription) else { return }
        versionSettingsDraft.displayDescription = text
        saveVersionSettings()
    }

    func toggleVersionFavorite() {
        guard versionSettingsTarget != nil, !isBusy, !isGameRunning else { return }
        versionSettingsDraft.isFavorite.toggle()
        saveVersionSettings()
    }

    func toggleVersionFavorite(_ version: InstalledVersion) {
        guard !isBusy, !isGameRunning else { return }
        do {
            var profile: VersionProfileSettings
            if versionSettingsTargetID == version.id { profile = versionSettingsDraft }
            else if let cached = versionProfileCache[version.id] { profile = cached }
            else { profile = try VersionSettingsRepository.load(version: version) }
            profile.isFavorite.toggle()
            try VersionSettingsRepository.save(profile, for: version)
            versionProfileCache[version.id] = profile
            if versionSettingsTargetID == version.id { versionSettingsDraft = profile }
            statusMessage = profile.isFavorite ? "版本 \(version.id) 已收藏" : "版本 \(version.id) 已取消收藏"
        } catch { report(error) }
    }

    func revealVersionSubfolder(_ relative: String) {
        guard let version = versionSettingsTarget else { return }
        do {
            let profile = profileSettings(for: version)
            let runtime = profile.isolateGameDirectory ? version.directory : gameDirectory
            let folder: URL
            if relative.isEmpty { folder = version.directory }
            else { folder = try MinecraftVersionMetadata.safeFile(relativePath: relative, under: runtime) }
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            NSWorkspace.shared.open(folder)
        } catch { report(error) }
    }

    func exportVersionLaunchScript() {
        guard let version = versionSettingsTarget, !isBusy, !isGameRunning else { return }
        let config: LaunchConfiguration
        do { config = try launchConfiguration(for: version) } catch { report(error); return }
        let panel = NSSavePanel()
        panel.nameFieldStringValue = "\(version.id).command"
        panel.message = "导出可在 macOS 终端执行的离线启动脚本"
        guard panel.runModal() == .OK, let destination = panel.url else { return }
        Task {
            do {
                try await Task.detached(priority: .userInitiated) {
                    let plan = try MinecraftLauncher.makeLaunchPlan(configuration: config)
                    try VersionManager.exportLaunchScript(plan: plan, to: destination)
                }.value
                appendLog("启动脚本已导出：\(destination.path)")
            } catch { report(error) }
        }
    }

    func deleteVersion() {
        guard let version = versionSettingsTarget else { return }
        deleteVersion(version)
    }

    func deleteVersion(_ version: InstalledVersion) {
        guard !isBusy, !isGameRunning else { return }
        let sourceGameDirectory = gameDirectory
        do {
            let folder = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(version.id)", under: sourceGameDirectory)
            guard folder.resolvingSymlinksInPath().standardizedFileURL == version.directory.resolvingSymlinksInPath().standardizedFileURL else {
                throw settingsError("版本所在的游戏文件夹已改变，请刷新版本列表后重试。")
            }
            let profile = profileSettings(for: version)
            var message = "你确定要删除版本 \(version.id) 吗？"
            if profile.isolateGameDirectory {
                message += "\n该版本对应的存档、资源包、Mod 等文件也将被一并删除！"
                let saves = try MinecraftVersionMetadata.safeFile(relativePath: "saves", under: folder)
                if FileManager.default.fileExists(atPath: saves.path) {
                    let entries = try FileManager.default.contentsOfDirectory(at: saves,
                        includingPropertiesForKeys: [.isDirectoryKey, .contentModificationDateKey])
                    let worlds = try entries.compactMap { url -> (String, Date)? in
                        let values = try url.resourceValues(forKeys: [.isDirectoryKey, .contentModificationDateKey])
                        guard values.isDirectory == true else { return nil }
                        return (url.lastPathComponent, values.contentModificationDate ?? .distantPast)
                    }.sorted {
                        $0.1 == $1.1 ? $0.0.localizedStandardCompare($1.0) == .orderedAscending : $0.1 > $1.1
                    }
                    if !worlds.isEmpty {
                        let formatter = RelativeDateTimeFormatter()
                        formatter.locale = Locale(identifier: "zh_CN")
                        let now = Date()
                        message += "\n\n这会删除以下存档：\n" + worlds.map {
                            "· \($0.0)（上次修改：\(formatter.localizedString(for: $0.1, relativeTo: now))）"
                        }.joined(separator: "\n")
                    }
                }
            }
            let alert = NSAlert()
            alert.messageText = "版本删除确认"
            alert.informativeText = message
            alert.addButton(withTitle: "删除"); alert.addButton(withTitle: "取消")
            guard alert.runModal() == .alertFirstButtonReturn else { return }
            NSWorkspace.shared.recycle([folder]) { _, error in
                Task { @MainActor in
                    if let error { self.report(error); return }
                    guard !FileManager.default.fileExists(atPath: folder.path) else {
                        self.report(self.settingsError("版本未能移入废纸篓，请重试。"))
                        return
                    }
                    guard self.gameDirectory == sourceGameDirectory else { return }
                    self.installedVersions.removeAll { $0.id == version.id }
                    self.versionProfileCache.removeValue(forKey: version.id)
                    self.originalVersionIDs.removeValue(forKey: version.id)
                    if self.versionSettingsTargetID == version.id {
                        self.versionSettingsTargetID = nil
                        if self.page == .versionSettings { self.page = .library }
                    }
                    self.statusMessage = "版本 \(version.id) 已删除！"
                    await self.refreshLocal()
                }
            }
        } catch { report(error) }
    }

    private func prompt(title: String, message: String, value: String) -> String? {
        let alert = NSAlert()
        alert.messageText = title; alert.informativeText = message
        let field = NSTextField(string: value)
        field.frame = NSRect(x: 0, y: 0, width: 360, height: 24)
        alert.accessoryView = field
        alert.addButton(withTitle: "确定"); alert.addButton(withTitle: "取消")
        alert.window.initialFirstResponder = field
        return alert.runModal() == .alertFirstButtonReturn ? field.stringValue : nil
    }

    private func settingsError(_ text: String) -> NSError {
        NSError(domain: "PCL.VersionSettings", code: 1, userInfo: [NSLocalizedDescriptionKey: text])
    }
}

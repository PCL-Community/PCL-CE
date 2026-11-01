import CryptoKit
import Foundation

public struct ExtraComponentSelection: Hashable, Sendable {
    public var optiFine: OptiFineVersion?
    public var liteLoader: LiteLoaderVersion?
    public var optiFabric: CommunityVersion?
    public var keepOriginalOptiFabric: Bool
    public init(optiFine: OptiFineVersion? = nil, liteLoader: LiteLoaderVersion? = nil,
                optiFabric: CommunityVersion? = nil, keepOriginalOptiFabric: Bool = false) {
        self.optiFine = optiFine; self.liteLoader = liteLoader
        self.optiFabric = optiFabric; self.keepOriginalOptiFabric = keepOriginalOptiFabric
    }
}

public struct ExtraComponentSnapshot: Hashable, Sendable {
    public let optiFine: OptiFineVersion?
    public let liteLoader: LiteLoaderVersion?
    public let optiFabric: LocalModFile?
    public let optiFabricFiles: [LocalModFile]
    public let fingerprint: String
    let mods: ExtraModSnapshot
    public var selection: ExtraComponentSelection {
        .init(optiFine: optiFine, liteLoader: liteLoader, keepOriginalOptiFabric: true)
    }
    public func matchesOptiFabric(_ version: CommunityVersion) -> Bool {
        guard version.source == .curseForge, version.projectID == "322385", optiFabricFiles.count == 1,
              let file = version.primaryFile else { return false }
        return mods.matches(file: optiFabricFiles[0].url, resource: file)
    }
}

/// Compose independent components in a private profile before exposing a version.
/// Existing runtime files participate in reversible transactions until metadata commits.
public actor ExtraComponentsInstaller {
    private let vanilla: MinecraftInstaller
    private let loaders: ModLoaderInstaller
    private let optiFine: OptiFineInstaller
    private let liteLoader: LiteLoaderInstaller
    private let fabricAPI: FabricAPIComponent
    private let resources: CommunityResourceClient
    private let downloader: CommunityResourceInstaller
    private var installing = false
    public init(session: URLSession = .shared) {
        let vanilla = MinecraftInstaller(session: session)
        self.vanilla = vanilla; loaders = ModLoaderInstaller(session: session, vanillaInstaller: vanilla)
        optiFine = OptiFineInstaller(session: session); liteLoader = LiteLoaderInstaller(session: session)
        fabricAPI = FabricAPIComponent(session: session)
        let client = CommunityResourceClient(session: session)
        resources = client; downloader = CommunityResourceInstaller(client: client, session: session)
    }

    public func fetchOptiFineVersions(minecraftVersion: String) async throws -> [OptiFineVersion] {
        try await optiFine.fetchVersions(minecraftVersion: minecraftVersion)
    }
    public func fetchLiteLoaderVersions(minecraftVersion: String) async throws -> [LiteLoaderVersion] {
        try await liteLoader.fetchVersions(minecraftVersion: minecraftVersion)
    }
    public func fetchOptiFabricVersions(minecraftVersion: String) async throws -> [CommunityVersion] {
        let project = try await resources.project(id: "322385", source: .curseForge)
        guard project.projectID == "322385", project.kind == .mod else { throw Self.error("OptiFabric 项目身份不匹配") }
        return try await resources.versions(for: project, minecraftVersion: minecraftVersion, loader: .fabric).filter {
            $0.projectID == "322385" && $0.source == .curseForge && $0.gameVersions.contains(minecraftVersion)
                && ($0.loaders.isEmpty || $0.loaders.contains("fabric"))
        }.map { version in
            CommunityVersion(id: version.id, source: version.source, projectID: version.projectID,
                name: version.name, versionNumber: version.versionNumber, gameVersions: version.gameVersions,
                loaders: ["fabric"], published: version.published, releaseType: version.releaseType,
                files: version.files, dependencies: version.dependencies)
        }
    }

    public func inspect(version: InstalledVersion, gameDirectory: URL, isolate: Bool) async throws -> ExtraComponentSnapshot {
        let metadata = try MinecraftVersionMetadata.resolve(versionID: version.id, gameDirectory: gameDirectory)
        let folder = try LocalModManager.directory(gameDirectory: gameDirectory, version: version, isolate: isolate)
        let snapshot = try ExtraModSnapshot.capture(folder: folder)
        let opti = try OptiFineInstaller.detect(metadata: metadata, modsDirectory: folder)
        let lite = try LiteLoaderInstaller.detect(metadata: metadata)
        let optiFabric = snapshot.files.filter { $0.kind == .optiFabric }.compactMap(\.local)
        return ExtraComponentSnapshot(optiFine: opti, liteLoader: lite, optiFabric: optiFabric.first,
            optiFabricFiles: optiFabric, fingerprint: snapshot.fingerprint, mods: snapshot)
    }

    public nonisolated static func compatibilityError(minecraftVersion: String, loader: ModLoaderVersion?, selection: ExtraComponentSelection) -> String? {
        if let loader, loader.minecraftVersion != minecraftVersion { return "加载器与 Minecraft 版本不匹配。" }
        if let lite = selection.liteLoader {
            if lite.minecraftVersion != minecraftVersion { return "LiteLoader 与 Minecraft 版本不匹配。" }
            if let kind = loader?.kind, kind != .forge { return "LiteLoader 不能与 \(kind.displayName) 同时安装。" }
        }
        if let opti = selection.optiFine {
            if opti.minecraftVersion != minecraftVersion { return "OptiFine 与 Minecraft 版本不匹配。" }
            switch loader?.kind {
            case .neoForge, .quilt: return "OptiFine 不兼容当前加载器。"
            case .forge:
                if minecraftVersion.compare("1.13", options: .numeric) != .orderedAscending && minecraftVersion.compare("1.14.3", options: .numeric) != .orderedDescending {
                    return "此 Minecraft 版本的 Forge 无法与 OptiFine 组合安装。"
                }
                guard let required = opti.requiredForgeVersion else { return "此 OptiFine 版本不兼容 Forge。" }
                if !required.isEmpty, let actual = loader?.version {
                    let matches = required.contains(".") ? (actual == required || actual.hasSuffix("-" + required)) : actual.split(separator: ".").last.map(String.init) == required
                    if !matches { return "此 OptiFine 需要 Forge \(required)，请重新选择兼容版本。" }
                }
            case .fabric:
                if minecraftVersion.compare("1.20.4", options: .numeric) == .orderedDescending { return "此 Minecraft 版本无法通过 OptiFabric 使用 OptiFine。" }
                if selection.optiFabric == nil && !selection.keepOriginalOptiFabric { return "Fabric 与 OptiFine 同时使用时需要 OptiFabric。" }
            case nil: break
            }
        }
        if let bridge = selection.optiFabric {
            guard loader?.kind == .fabric, selection.optiFine != nil else { return "OptiFabric 需要 Fabric 和 OptiFine。" }
            guard bridge.source == .curseForge, bridge.projectID == "322385", bridge.gameVersions.contains(minecraftVersion), bridge.loaders.contains("fabric") else { return "OptiFabric 与 Minecraft 版本不匹配。" }
        }
        if selection.optiFabric != nil || (loader?.kind == .fabric && selection.optiFine != nil) {
            if minecraftVersion.hasPrefix("1.14") || minecraftVersion.hasPrefix("1.15") { return "此旧版 Fabric 需要 OptiFabric Origins，原项目不支持自动安装。" }
        }
        return nil
    }

    public func install(minecraft: RemoteVersion, loader: ModLoaderVersion?, selection: ExtraComponentSelection,
                        fabricAPI api: CommunityVersion?, gameDirectory: URL, profileID: String,
                        javaExecutable: URL, progress: @escaping @Sendable (InstallProgress) -> Void) async throws {
        guard !installing else { throw MinecraftInstallError.alreadyInstalling }
        installing = true; defer { installing = false }
        try Self.requireCompatible(minecraft: minecraft.id, loader: loader, selection: selection, api: api)
        if loader?.kind == .fabric, selection.optiFine != nil, selection.optiFabric == nil { throw Self.error("Fabric 与 OptiFine 同时使用时需要选择 OptiFabric") }
        let root = gameDirectory.standardizedFileURL
        let final = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(profileID)", under: root)
        try VersionManager.validateName(profileID)
        guard !FileManager.default.fileExists(atPath: final.path) else { throw VersionManagementError.destinationExists }
        let temporaryID = "PCL-Components-" + UUID().uuidString
        let temporary = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(temporaryID)", under: root)
        defer { try? FileManager.default.removeItem(at: temporary) }
        let relay = ExtraComponentProgress(progress: progress)
        try await build(minecraft: minecraft, loader: loader, selection: selection, profileID: temporaryID,
                        root: root, java: javaExecutable, relay: relay)
        let local = try Self.local(temporaryID, root: root)
        try await stageOptiFabric(selection, original: nil, folder: temporary.appendingPathComponent("mods"), root: root, relay: relay)
        try Self.bridgeMarker(selection, original: nil, profileID: temporaryID, root: root)
        if let api {
            let file = try await fabricAPI.prepare(version: api, gameDirectory: root, reuseFiles: try reusableAPIs(root), progress: { relay.update($0, stage: 3) })
            let before = try FabricAPIComponent.installed(gameDirectory: root, version: local, isolate: true)
            let txn = try FabricAPIComponent.apply(preparedFile: file, gameDirectory: root, version: local, isolate: true, expectedState: before)
            txn.finish()
        }
        try Task.checkCancellation(); relay.begin(4, message: "应用版本组件")
        _ = try VersionManager.rename(version: local, to: profileID, gameDirectory: root)
        relay.finish()
    }

    public func modify(version: InstalledVersion, minecraft: RemoteVersion?, minecraftID: String,
                       loader: ModLoaderVersion?, selection: ExtraComponentSelection, fabricAPI api: CommunityVersion?,
                       keepOriginalFabricAPI: Bool, originalFabricAPI: FabricAPIState, originalExtras: ExtraComponentSnapshot,
                       newName: String, isolate: Bool, expectedFingerprint: String, gameDirectory: URL, javaExecutable: URL,
                       progress: @escaping @Sendable (InstallProgress) -> Void) async throws {
        guard !installing else { throw MinecraftInstallError.alreadyInstalling }
        installing = true; defer { installing = false }
        try Self.requireCompatible(minecraft: minecraftID, loader: loader, selection: selection, api: api)
        let root = gameDirectory.standardizedFileURL
        let oldMetadata = try MinecraftVersionMetadata.resolve(versionID: version.id, gameDirectory: root)
        let oldMC = oldMetadata["pclOriginalVersion"] as? String ?? version.inheritsFrom ?? version.id
        if loader?.kind == .fabric, selection.optiFine != nil, selection.optiFabric == nil, originalExtras.optiFabricFiles.isEmpty { throw Self.error("Fabric 与 OptiFine 同时使用时需要选择 OptiFabric") }
        if keepOriginalFabricAPI, !originalFabricAPI.files.isEmpty, oldMC != minecraftID || (MinecraftVersionMetadata.modLoader(in: oldMetadata)?.kind == .fabric && loader?.kind != .fabric) { throw Self.error("请重新选择兼容的 Fabric API，或明确移除它") }
        let sameOpti = originalExtras.optiFine == selection.optiFine
        let sameLite = originalExtras.liteLoader == selection.liteLoader
        let rebuild = oldMC != minecraftID || MinecraftVersionMetadata.modLoader(in: oldMetadata) != loader || !sameOpti || !sameLite
        let wasManagedBridge = MinecraftVersionMetadata.modLoader(in: oldMetadata)?.kind == .fabric && originalExtras.optiFine != nil
        let needsBridge = loader?.kind == .fabric && selection.optiFine != nil
        if selection.keepOriginalOptiFabric, !originalExtras.optiFabricFiles.isEmpty {
            if wasManagedBridge && !needsBridge { throw Self.error("请先清除不再兼容的 OptiFabric") }
            if (wasManagedBridge || needsBridge) && oldMC != minecraftID { throw Self.error("Minecraft 版本已改变，请重新选择兼容的 OptiFabric") }
        }
        let temporaryID = "PCL-Components-" + UUID().uuidString
        let temporary = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(temporaryID)", under: root)
        defer { try? FileManager.default.removeItem(at: temporary) }
        let relay = ExtraComponentProgress(progress: progress)
        if rebuild {
            guard let minecraft, minecraft.id == minecraftID else { throw Self.error("未找到目标 Minecraft 版本") }
            try await build(minecraft: minecraft, loader: loader, selection: selection, profileID: temporaryID, root: root, java: javaExecutable, relay: relay)
        } else {
            try FileManager.default.createDirectory(at: temporary, withIntermediateDirectories: false)
            let destination = temporary.appendingPathComponent("mods")
            for file in originalExtras.mods.files where file.kind == .optiFine {
                try FileManager.default.createDirectory(at: destination, withIntermediateDirectories: true)
                try ModpackImporter.cloneOrCopy(file.url, to: destination.appendingPathComponent(file.url.lastPathComponent))
            }
        }
        try await stageOptiFabric(selection, original: originalExtras, folder: temporary.appendingPathComponent("mods"), root: root, relay: relay)
        if rebuild { try Self.bridgeMarker(selection, original: oldMetadata["pclOptiFabric"], profileID: temporaryID, root: root) }
        let preparedAPI: URL?
        let changeAPI = !keepOriginalFabricAPI && (api.map { !originalFabricAPI.matches($0) } ?? !originalFabricAPI.files.isEmpty)
        if changeAPI, let api {
            preparedAPI = try await fabricAPI.prepare(version: api, gameDirectory: root, reuseFiles: try reusableAPIs(root), progress: { relay.update($0, stage: 3) })
        } else { preparedAPI = nil }
        try Task.checkCancellation()
        guard try VersionComponentManager.fingerprint(version: version, gameDirectory: root) == expectedFingerprint,
              try VersionSettingsRepository.load(version: version).isolateGameDirectory == isolate else { throw Self.error("原版本或隔离设置已改变，请刷新后重试") }
        let desired = try ExtraModSnapshot.capture(folder: temporary.appendingPathComponent("mods"))
        var extraTxn: ExtraModTransaction?, apiTxn: FabricAPITransaction?
        let jsonURL = version.directory.appendingPathComponent(version.id + ".json")
        let originalJSON = try Data(contentsOf: jsonURL)
        var changedJSON = false
        do {
            extraTxn = try ExtraModTransaction.apply(expected: originalExtras.mods, desired: desired, gameDirectory: root)
            if changeAPI { apiTxn = try FabricAPIComponent.apply(preparedFile: preparedAPI, gameDirectory: root, version: version, isolate: isolate, expectedState: originalFabricAPI) }
            try Task.checkCancellation(); relay.begin(4, message: "应用版本组件")
            if rebuild {
                _ = try VersionComponentManager.applyPreparedProfile(version: version, preparedID: temporaryID, gameDirectory: root, newName: newName, expectedFingerprint: expectedFingerprint)
            } else {
                // OptiFabric-only changes update its provenance without reinstalling Minecraft.
                if !selection.keepOriginalOptiFabric {
                    var metadata = try MinecraftVersionMetadata.load(versionID: version.id, gameDirectory: root)
                    metadata["pclOptiFabric"] = try selection.optiFabric.map { try Self.json($0) }
                    try JSONSerialization.data(withJSONObject: metadata, options: [.prettyPrinted, .sortedKeys]).write(to: jsonURL, options: .atomic)
                    changedJSON = true
                }
                if newName != version.id { _ = try VersionManager.rename(version: version, to: newName, gameDirectory: root) }
            }
            apiTxn?.finish(); extraTxn?.finish(); relay.finish()
        } catch {
            let failure = error
            var rollbackFailure: Error?
            if changedJSON { do { try originalJSON.write(to: jsonURL, options: .atomic) } catch { rollbackFailure = error } }
            do { try apiTxn?.rollback() } catch { if rollbackFailure == nil { rollbackFailure = error } }
            do { try extraTxn?.rollback() } catch { if rollbackFailure == nil { rollbackFailure = error } }
            if let rollbackFailure { throw rollbackFailure }
            throw failure
        }
    }

    private func build(minecraft: RemoteVersion, loader: ModLoaderVersion?, selection: ExtraComponentSelection,
                       profileID: String, root: URL, java: URL, relay: ExtraComponentProgress) async throws {
        relay.begin(0, message: "准备 Minecraft 和主加载器")
        if let loader {
            try await loaders.install(version: loader, minecraftVersion: minecraft, gameDirectory: root, profileID: profileID,
                                      javaExecutable: java, progress: { relay.update($0, stage: 0) })
        } else { try await vanilla.install(version: minecraft, gameDirectory: root, profileID: profileID, progress: { relay.update($0, stage: 0) }) }
        let local = try Self.local(profileID, root: root)
        var settings = try VersionSettingsRepository.load(version: local)
        settings.isolateGameDirectory = true; settings.category = loader != nil || selection.liteLoader != nil ? "modded" : "auto"
        if loader == nil { settings.iconName = selection.liteLoader == nil ? "GrassPath" : "Egg" }
        try VersionSettingsRepository.save(settings, for: local)
        relay.begin(1, message: "安装 LiteLoader")
        if let lite = selection.liteLoader { try await liteLoader.apply(version: lite, toProfile: profileID, gameDirectory: root, javaExecutable: java, progress: { relay.update($0, stage: 1) }) }
        relay.begin(2, message: "安装 OptiFine")
        if let opti = selection.optiFine { try await optiFine.apply(version: opti, toProfile: profileID, gameDirectory: root, javaExecutable: java, asMod: loader != nil, progress: { relay.update($0, stage: 2) }) }
        relay.begin(3, message: "安装附加组件")
    }

    private func stageOptiFabric(_ selection: ExtraComponentSelection, original: ExtraComponentSnapshot?, folder: URL,
                                 root: URL, relay: ExtraComponentProgress) async throws {
        if selection.keepOriginalOptiFabric, let original {
            if !original.optiFabricFiles.isEmpty { try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true) }
            for file in original.optiFabricFiles { try ModpackImporter.cloneOrCopy(file.url, to: folder.appendingPathComponent(file.filename)) }
        } else if let bridge = selection.optiFabric {
            guard bridge.source == .curseForge, bridge.projectID == "322385", let file = bridge.primaryFile else { throw Self.error("OptiFabric 项目身份不匹配") }
            let name = try ModpackZIPArchive.validatedPath(file.filename)
            guard !name.contains("/"), name.lowercased().hasSuffix(".jar") else { throw Self.error("OptiFabric 文件名无效") }
            let cache = try MinecraftVersionMetadata.safeFile(relativePath: "PCL/download-cache/optifabric/\(bridge.id)", under: root)
            try FileManager.default.createDirectory(at: cache, withIntermediateDirectories: true)
            let destination = cache.appendingPathComponent(name)
            if FileManager.default.fileExists(atPath: destination.path), !(try CommunityResourceInstaller.fileMatches(at: destination, file: file)) {
                throw Self.error("OptiFabric 缓存校验失败，请删除损坏的缓存后重试")
            }
            if let existing = original?.optiFabricFiles.first(where: { (try? CommunityResourceInstaller.fileMatches(at: $0.url, file: file)) == true }),
               !FileManager.default.fileExists(atPath: destination.path) { try ModpackImporter.cloneOrCopy(existing.url, to: destination) }
            let verified = try await downloader.download(version: bridge, to: destination, progress: { relay.update($0, stage: 3) })
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            try ModpackImporter.cloneOrCopy(verified, to: folder.appendingPathComponent(name))
            let mods = try LocalModManager.scan(directory: folder)
            guard mods.contains(where: { $0.filename == name && $0.modID == "optifabric" && $0.error == nil }) else { throw Self.error("下载的 JAR 不是 OptiFabric") }
        }
    }

    private func reusableAPIs(_ root: URL) throws -> [URL] {
        try LocalVersionScanner.scan(gameDirectory: root).flatMap { version in
            let settings = (try? VersionSettingsRepository.load(version: version)) ?? VersionProfileSettings()
            return (try? FabricAPIComponent.installed(gameDirectory: root, version: version, isolate: settings.isolateGameDirectory).files.map(\.url)) ?? []
        }
    }
    private static func local(_ id: String, root: URL) throws -> InstalledVersion {
        let metadata = try MinecraftVersionMetadata.resolve(versionID: id, gameDirectory: root)
        return InstalledVersion(id: id, type: metadata["type"] as? String ?? "release", requiredJava: MinecraftVersionMetadata.requiredJava(metadata),
            directory: try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(id)", under: root), inheritsFrom: metadata["inheritsFrom"] as? String,
            modLoader: MinecraftVersionMetadata.modLoader(in: metadata))
    }
    private static func requireCompatible(minecraft: String, loader: ModLoaderVersion?, selection: ExtraComponentSelection, api: CommunityVersion?) throws {
        if let error = compatibilityError(minecraftVersion: minecraft, loader: loader, selection: selection) { throw Self.error(error) }
        if let api, loader?.kind != .fabric || !api.gameVersions.contains(minecraft) { throw Self.error("Fabric API 与目标版本不兼容") }
    }
    private static func bridgeMarker(_ selection: ExtraComponentSelection, original: Any?, profileID: String, root: URL) throws {
        let url = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(profileID)/\(profileID).json", under: root)
        var metadata = try MinecraftVersionMetadata.load(versionID: profileID, gameDirectory: root)
        metadata["pclOptiFabric"] = selection.keepOriginalOptiFabric ? original : try selection.optiFabric.map { try json($0) }
        try JSONSerialization.data(withJSONObject: metadata, options: [.prettyPrinted,.sortedKeys]).write(to: url, options: .atomic)
    }
    private static func json<T: Encodable>(_ value: T) throws -> Any { try JSONSerialization.jsonObject(with: JSONEncoder().encode(value)) }
    private static func error(_ value: String) -> MinecraftInstallError { .invalidMetadata(value) }
}

private final class ExtraComponentProgress: @unchecked Sendable {
    private let lock = NSLock()
    private var current = 0, revision: Int64 = 0
    private var fraction = 0.0
    private let output: @Sendable (InstallProgress) -> Void
    private let titles = ["准备 Minecraft 和加载器", "安装 LiteLoader", "安装 OptiFine", "安装附加组件", "应用版本组件"]
    init(progress: @escaping @Sendable (InstallProgress) -> Void) { output = progress }
    func begin(_ stage: Int, message: String) {
        lock.lock(); current = stage; lock.unlock()
        update(.init(message: message, completed: 0, total: 1), stage: stage)
    }
    func update(_ value: InstallProgress, stage: Int) {
        lock.lock(); defer { lock.unlock() }
        guard stage == current else { return }
        revision += 1; fraction = max(fraction, min(0.99, Double(stage) * 0.2 + value.fraction * 0.19))
        let stages = titles.enumerated().map { index, title in InstallStageProgress(id: "components-\(index)", title: title,
            state: index < stage ? .completed : index == stage ? .running : .waiting, fraction: index < stage ? 1 : index == stage ? value.fraction : 0) }
        let snapshot = InstallTaskSnapshot(title: "安装版本组件", stages: stages, totalFraction: fraction,
            bytesPerSecond: value.snapshot?.bytesPerSecond ?? 0, remainingFiles: value.snapshot?.remainingFiles ?? 0,
            downloadedBytes: value.snapshot?.downloadedBytes ?? 0, revision: revision)
        output(.init(message: value.message, completed: Int(fraction * 10000), total: 10000, snapshot: snapshot))
    }
    func finish() {
        lock.lock(); defer { lock.unlock() }; current = 5; revision += 1
        let snapshot = InstallTaskSnapshot(title: "安装版本组件", stages: titles.enumerated().map { .init(id: "components-\($0.offset)", title: $0.element, state: .completed) }, totalFraction: 1, revision: revision)
        output(.init(message: "版本组件安装完成", completed: 1, total: 1, snapshot: snapshot))
    }
}

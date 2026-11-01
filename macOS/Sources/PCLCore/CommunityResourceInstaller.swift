import Foundation
import Darwin

public struct CommunityInstallResult: Sendable {
    public let files: [URL]
    public let reusedFiles: Int
    public let downloadedFiles: Int
    public init(files: [URL], reusedFiles: Int, downloadedFiles: Int) {
        self.files = files; self.reusedFiles = reusedFiles; self.downloadedFiles = downloadedFiles
    }
}

public enum CommunityInstallError: LocalizedError {
    case incompatible(String), collision(String), invalidFile(String), unsafePath(String), missingDependency(String), alreadyInstalling
    public var errorDescription: String? {
        switch self {
        case .incompatible(let value): return "资源与所选游戏版本不兼容：\(value)"
        case .collision(let value): return "目标已有不同内容的文件，原文件未被覆盖：\(value)"
        case .invalidFile(let value): return "社区资源文件校验失败：\(value)"
        case .unsafePath(let value): return "无法使用该资源安装路径：\(value)"
        case .missingDependency(let value): return "未找到兼容的必需前置：\(value)"
        case .alreadyInstalling: return "已有社区资源正在安装，请等待完成。"
        }
    }
}

/// Plans every required dependency first, verifies all bytes in private staging,
/// then publishes new runtime files. Existing runtime files are never replaced.
public actor CommunityResourceInstaller {
    private let client: CommunityResourceClient
    private let session: URLSession
    private var installing = false
    private static let maximumFileBytes: Int64 = 2 * 1024 * 1024 * 1024

    public init(client: CommunityResourceClient? = nil, session: URLSession = .shared) {
        self.session = session; self.client = client ?? CommunityResourceClient(session: session)
    }

    public func install(project: CommunityProject, version: CommunityVersion, gameDirectory: URL,
                        targetVersion: InstalledVersion, worldDirectory: URL? = nil, includeDependencies: Bool = true,
                        progress: @escaping @Sendable (InstallProgress) -> Void) async throws -> CommunityInstallResult {
        guard !installing else { throw CommunityInstallError.alreadyInstalling }
        installing = true
        defer { installing = false }
        guard project.kind != .modpack else { throw CommunityInstallError.incompatible("整合包需要作为独立游戏版本导入") }
        guard version.source == project.source, version.projectID == project.projectID else {
            throw CommunityInstallError.invalidFile("文件版本不属于所选项目")
        }
        let monitor = InstallTaskMonitor(title: "\(project.kind.displayName) 安装： \(project.name)", stages: [
            .init(id: "resolveDependencies", title: "获取资源下载信息", weight: 1),
            .init(id: "resourceFiles", title: "下载\(project.kind.displayName)", weight: 10),
            .init(id: "final", title: "安装\(project.kind.displayName)", weight: 1)
        ], progress: progress)
        do {
            return try await InstallProgressContext.$current.withValue(monitor) {
                monitor.begin("resolveDependencies")
                let root = try Self.canonicalRoot(gameDirectory)
                let profile = try Self.descendant("versions/\(targetVersion.id)", under: root)
                guard try Self.canonicalRoot(targetVersion.directory) == profile else { throw CommunityInstallError.unsafePath(targetVersion.directory.path) }
                let metadata = try MinecraftVersionMetadata.resolve(versionID: targetVersion.id, gameDirectory: root)
                let minecraft = metadata["pclOriginalVersion"] as? String ?? targetVersion.inheritsFrom ?? targetVersion.id
                let loader = MinecraftVersionMetadata.modLoader(in: metadata)?.kind
                try Self.requireCompatible(version, kind: project.kind, minecraft: minecraft, loader: loader)
                let settings = try VersionSettingsRepository.load(version: targetVersion)
                let runtime = settings.isolateGameDirectory ? profile : root
                let folder: URL
                switch project.kind {
                case .mod: folder = try Self.descendant("mods", under: runtime)
                case .resourcepack: folder = try Self.descendant("resourcepacks", under: runtime)
                case .shader: folder = try Self.descendant("shaderpacks", under: runtime)
                case .datapack:
                    guard let worldDirectory else { throw CommunityInstallError.unsafePath("请先选择要安装数据包的存档") }
                    let world = try Self.world(worldDirectory, under: runtime)
                    folder = try Self.descendant("datapacks", under: world)
                case .modpack: throw CommunityInstallError.incompatible("整合包需要独立导入")
                }
                var versions = [version]
                if project.kind == .mod && includeDependencies {
                    versions = try await resolveDependencies(version, minecraft: minecraft, loader: loader)
                }
                var names = Set<String>()
                for item in versions {
                    guard let file = item.primaryFile else { throw CommunityInstallError.invalidFile(item.name + " 没有可下载文件") }
                    try Self.validate(file)
                    let expectedExtension = project.kind == .mod ? "jar" : "zip"
                    guard (file.filename as NSString).pathExtension.lowercased() == expectedExtension else {
                        throw CommunityInstallError.invalidFile("\(project.kind.displayName) 文件应为 .\(expectedExtension)：\(file.filename)")
                    }
                    guard names.insert(ModpackZIPArchive.pathKey(file.filename)).inserted else {
                        throw CommunityInstallError.collision("多个资源使用同一文件名：\(file.filename)")
                    }
                }
                monitor.complete("resolveDependencies")
                monitor.begin("resourceFiles")
                let cache = try Self.descendant("PCL/download-cache/community-files", under: root)
                try FileManager.default.createDirectory(at: cache, withIntermediateDirectories: true)
                try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
                let staging = try Self.descendant(".pcl-community-\(UUID().uuidString)", under: root)
                try FileManager.default.createDirectory(at: staging, withIntermediateDirectories: true)
                defer { try? FileManager.default.removeItem(at: staging) }
                let runtimeCandidates = try Self.regularFiles(in: folder)
                var tasks: [PreparedFile] = []
                monitor.setRemainingFiles(versions.count, for: "community-files")
                for (index, item) in versions.enumerated() {
                    try Task.checkCancellation()
                    let file = item.primaryFile!
                    let destination = try Self.descendant(file.filename, under: folder)
                    if FileManager.default.fileExists(atPath: destination.path), !(try Self.matches(destination, file: file)) {
                        throw CommunityInstallError.collision(destination.lastPathComponent)
                    }
                    let renamed = try runtimeCandidates.first { candidate in
                        guard candidate.pathExtension.lowercased() == destination.pathExtension.lowercased() else { return false }
                        return try Self.matches(candidate, file: file)
                    }
                    if let existing = renamed {
                        tasks.append(PreparedFile(version: item, file: file, staged: nil, destination: existing, reused: true))
                    } else {
                        let temporary = staging.appendingPathComponent("\(index)-" + file.filename)
                        let cached = try Self.cachedFile(file, under: cache)
                        var reused = false
                        if try Self.matches(cached, file: file) {
                            try ModpackImporter.cloneOrCopy(cached, to: temporary); reused = true
                        } else {
                            try await InstallProgressContext.$stageID.withValue("resourceFiles") {
                                try await Self.fetch(file, to: temporary, session: session)
                            }
                            try Self.publishCache(temporary, file: file, destination: cached)
                        }
                        tasks.append(PreparedFile(version: item, file: file, staged: temporary, destination: destination, reused: reused))
                    }
                    monitor.setRemainingFiles(versions.count - index - 1, for: "community-files")
                    monitor.update("resourceFiles", fraction: Double(index + 1) / Double(versions.count))
                }
                // A different filename must not introduce a second copy of an
                // already installed Fabric/Quilt/Forge Mod ID.
                if project.kind == .mod { try Self.rejectDuplicateModIDs(tasks, existing: runtimeCandidates) }
                let indexFile = try Self.descendant("PCL/community-resources.json", under: runtime)
                var index = try Self.readIndex(indexFile)
                for task in tasks {
                    let key = String(folder.path.dropFirst(runtime.path.count + 1)) + ":" + task.version.source.rawValue + ":" + task.version.projectID
                    if let previous = index[key], previous.versionID != task.version.id {
                        let prior = try Self.descendant(previous.relativePath, under: runtime)
                        if FileManager.default.fileExists(atPath: prior.path), prior != task.destination {
                            throw CommunityInstallError.collision("\(task.version.name) 已安装其他版本：\(prior.lastPathComponent)")
                        }
                    }
                    index[key] = ResourceIndexEntry(versionID: task.version.id, relativePath: String(task.destination.path.dropFirst(runtime.path.count + 1)))
                }
                monitor.complete("resourceFiles")
                monitor.begin("final")
                var committed: [URL] = []
                do {
                    for task in tasks {
                        try Task.checkCancellation()
                        guard let staged = task.staged else { continue }
                        let destination = try Self.descendant(task.destination.lastPathComponent, under: folder)
                        if FileManager.default.fileExists(atPath: destination.path) {
                            guard try Self.matches(destination, file: task.file) else { throw CommunityInstallError.collision(destination.lastPathComponent) }
                        } else {
                            try FileManager.default.moveItem(at: staged, to: destination)
                            committed.append(destination)
                        }
                    }
                    try Task.checkCancellation()
                    try FileManager.default.createDirectory(at: indexFile.deletingLastPathComponent(), withIntermediateDirectories: true)
                    let encoded = try JSONEncoder().encode(index)
                    try encoded.write(to: indexFile, options: .atomic)
                } catch {
                    for file in committed { try? FileManager.default.removeItem(at: file) }
                    throw error
                }
                monitor.complete("final", message: "\(project.name) 安装完成")
                return CommunityInstallResult(files: tasks.map(\.destination), reusedFiles: tasks.filter(\.reused).count,
                    downloadedFiles: tasks.filter { !$0.reused }.count)
            }
        } catch {
            if !Task.isCancelled {
                let active = monitor.snapshot.stages.first { $0.state == .running }?.id ?? "resolveDependencies"
                monitor.fail(active, message: error.localizedDescription)
            }
            monitor.cancel()
            if Task.isCancelled { throw CancellationError() }
            throw error
        }
    }

    /// Save the selected client file (or modpack archive) without changing a
    /// game version. This still requires its declared size and complete hashes.
    @discardableResult public func download(version: CommunityVersion, to destination: URL,
        progress: @escaping @Sendable (InstallProgress) -> Void) async throws -> URL {
        guard !installing else { throw CommunityInstallError.alreadyInstalling }
        installing = true
        defer { installing = false }
        guard let file = version.primaryFile else { throw CommunityInstallError.invalidFile("没有可下载文件") }
        try Self.validate(file)
        let monitor = InstallTaskMonitor(title: "资源下载： " + version.name, stages: [
            .init(id: "resourceFiles", title: "下载文件", weight: 10), .init(id: "final", title: "校验文件", weight: 1)
        ], progress: progress)
        do {
            return try await InstallProgressContext.$current.withValue(monitor) {
                monitor.begin("resourceFiles")
                let parent = try Self.canonicalRoot(destination.deletingLastPathComponent())
                let target = try Self.descendant(destination.lastPathComponent, under: parent)
                if FileManager.default.fileExists(atPath: target.path) {
                    guard try Self.matches(target, file: file) else { throw CommunityInstallError.collision(target.lastPathComponent) }
                    monitor.complete("resourceFiles"); monitor.begin("final"); monitor.complete("final")
                    return target
                }
                monitor.setRemainingFiles(1, for: "community-files")
                let temporary = parent.appendingPathComponent(".pcl-community-\(UUID().uuidString).partial")
                defer { try? FileManager.default.removeItem(at: temporary) }
                try await InstallProgressContext.$stageID.withValue("resourceFiles") {
                    try await Self.fetch(file, to: temporary, session: session)
                }
                monitor.setRemainingFiles(0, for: "community-files")
                monitor.complete("resourceFiles"); monitor.begin("final")
                try Task.checkCancellation()
                guard !FileManager.default.fileExists(atPath: target.path) else { throw CommunityInstallError.collision(target.lastPathComponent) }
                try FileManager.default.moveItem(at: temporary, to: target)
                monitor.complete("final", message: version.name + " 下载完成")
                return target
            }
        } catch {
            if !Task.isCancelled {
                monitor.fail(monitor.snapshot.stages.first { $0.state == .running }?.id ?? "resourceFiles", message: error.localizedDescription)
            }
            monitor.cancel()
            if Task.isCancelled { throw CancellationError() }
            throw error
        }
    }

    private func resolveDependencies(_ root: CommunityVersion, minecraft: String, loader: ModLoaderKind?) async throws -> [CommunityVersion] {
        var result: [CommunityVersion] = [], selected: [String: String] = [:]
        func visit(_ version: CommunityVersion) async throws {
            try Task.checkCancellation()
            let key = version.source.rawValue + ":" + version.projectID
            if let previous = selected[key] {
                guard previous == version.id else { throw CommunityInstallError.missingDependency("同一前置要求不同版本：\(version.name)") }
                return
            }
            guard selected.count < 128 else { throw CommunityInstallError.missingDependency("前置层数或数量超过限制") }
            selected[key] = version.id
            try Self.requireCompatible(version, kind: .mod, minecraft: minecraft, loader: loader)
            for dependency in version.dependencies where dependency.required {
                let child: CommunityVersion
                if let versionID = dependency.versionID {
                    child = try await client.version(id: versionID, source: version.source)
                    if let projectID = dependency.projectID, child.projectID != projectID { throw CommunityInstallError.missingDependency(projectID) }
                } else if let projectID = dependency.projectID {
                    if selected[version.source.rawValue + ":" + projectID] != nil { continue }
                    let depProject = try await client.project(id: projectID, source: version.source, kind: .mod)
                    let choices = try await client.versions(for: depProject, minecraftVersion: minecraft, loader: loader)
                    guard let choice = choices.first(where: { Self.compatible($0, kind: .mod, minecraft: minecraft, loader: loader) }) else {
                        throw CommunityInstallError.missingDependency(depProject.name)
                    }
                    child = choice
                } else { throw CommunityInstallError.missingDependency(version.name + " 含有无法识别的必需前置") }
                try await visit(child)
            }
            result.append(version)
        }
        try await visit(root)
        return result
    }

    private struct PreparedFile {
        let version: CommunityVersion
        let file: CommunityResourceFile
        let staged: URL?
        let destination: URL
        let reused: Bool
    }
    private struct ResourceIndexEntry: Codable { let versionID: String; let relativePath: String }

    private static func compatible(_ version: CommunityVersion, kind: CommunityResourceKind, minecraft: String, loader: ModLoaderKind?) -> Bool {
        if !version.gameVersions.isEmpty && !version.gameVersions.contains(minecraft) { return false }
        guard kind == .mod else { return true }
        let supported = Set(version.loaders.map { $0.lowercased() })
        if !supported.isEmpty {
            guard let loader, supported.contains(loader.rawValue.lowercased()) else { return false }
        } else if loader == nil { return false }
        return true
    }
    private static func requireCompatible(_ version: CommunityVersion, kind: CommunityResourceKind, minecraft: String, loader: ModLoaderKind?) throws {
        guard compatible(version, kind: kind, minecraft: minecraft, loader: loader) else {
            throw CommunityInstallError.incompatible("\(version.name)（Minecraft \(minecraft)\(loader.map { " · " + $0.displayName } ?? "")）")
        }
    }
    private static func validate(_ file: CommunityResourceFile) throws {
        let validated = try ModpackZIPArchive.validatedPath(file.filename)
        guard !validated.contains("/"), file.size >= 0, file.size <= maximumFileBytes,
              file.sha1 != nil || file.sha512 != nil || file.md5 != nil, !file.urls.isEmpty,
              file.urls.allSatisfy({ $0.scheme == "https" && $0.user == nil && $0.password == nil }) else {
            throw CommunityInstallError.invalidFile(file.filename)
        }
        for (hash, length) in [(file.sha1, 40), (file.sha512, 128), (file.md5, 32)] {
            if let hash, hash.utf8.count != length || hash.range(of: "^[0-9a-fA-F]+$", options: .regularExpression) == nil {
                throw CommunityInstallError.invalidFile(file.filename)
            }
        }
    }
    private static func matches(_ path: URL, file: CommunityResourceFile) throws -> Bool {
        try ModpackFileReuse.matches(path, identity: ModpackFileIdentity(size: file.size, sha1: file.sha1, sha512: file.sha512, md5: file.md5), maximumFileBytes: maximumFileBytes)
    }
    /// Allows the launcher to verify its own archive cache before reuse. User
    /// destinations still use download(), which refuses to replace other bytes.
    public nonisolated static func fileMatches(at url: URL, file: CommunityResourceFile) throws -> Bool {
        try validate(file)
        return try matches(url, file: file)
    }
    private static func cachedFile(_ file: CommunityResourceFile, under root: URL) throws -> URL {
        let hash = file.sha512 ?? file.sha1 ?? file.md5!
        return try descendant(hash.lowercased() + "." + ((file.filename as NSString).pathExtension.isEmpty ? "bin" : (file.filename as NSString).pathExtension), under: root)
    }
    private static func publishCache(_ source: URL, file: CommunityResourceFile, destination: URL) throws {
        if try matches(destination, file: file) { return }
        // A damaged cache may be repaired, while runtime files may never be replaced.
        let temporary = destination.deletingLastPathComponent().appendingPathComponent(".pcl-cache-\(UUID().uuidString)")
        defer { try? FileManager.default.removeItem(at: temporary) }
        try ModpackImporter.cloneOrCopy(source, to: temporary)
        try MinecraftInstaller.commit(temporary, to: destination)
    }
    private static func fetch(_ file: CommunityResourceFile, to destination: URL, session: URLSession) async throws {
        var last: Error = CommunityInstallError.invalidFile(file.filename)
        for url in file.urls {
            do {
                try Task.checkCancellation()
                var request = URLRequest(url: url, cachePolicy: .reloadIgnoringLocalCacheData, timeoutInterval: 90)
                request.setValue(CurseForgeResolver.userAgent, forHTTPHeaderField: "User-Agent")
                let (temporary, response) = try await PCLHTTPS.download(for: request, session: session)
                defer { try? FileManager.default.removeItem(at: temporary) }
                guard let http = response as? HTTPURLResponse, (200..<300).contains(http.statusCode), response.url?.scheme == "https",
                      try matches(temporary, file: file) else { throw CommunityInstallError.invalidFile(file.filename) }
                try Task.checkCancellation()
                try ModpackImporter.cloneOrCopy(temporary, to: destination)
                return
            } catch {
                if Task.isCancelled || (error as? URLError)?.code == .cancelled { throw CancellationError() }
                last = error
            }
        }
        throw last
    }
    private static func canonicalRoot(_ value: URL) throws -> URL {
        guard let path = realpath(value.path, nil) else { throw CommunityInstallError.unsafePath(value.path) }
        defer { free(path) }
        let result = URL(fileURLWithPath: String(cString: path), isDirectory: true)
        guard try result.resourceValues(forKeys: [.isDirectoryKey]).isDirectory == true else { throw CommunityInstallError.unsafePath(value.path) }
        return result
    }
    private static func descendant(_ relative: String, under root: URL) throws -> URL {
        let valid = try ModpackZIPArchive.validatedPath(relative)
        var path = root
        for component in valid.split(separator: "/") {
            path.appendPathComponent(String(component))
            if (try? FileManager.default.destinationOfSymbolicLink(atPath: path.path)) != nil { throw CommunityInstallError.unsafePath(path.path) }
        }
        return try MinecraftInstaller.safeDestination(root: root, relative: valid)
    }
    private static func regularFiles(in root: URL) throws -> [URL] {
        try FileManager.default.contentsOfDirectory(at: root, includingPropertiesForKeys: [.isRegularFileKey, .isSymbolicLinkKey], options: [.skipsHiddenFiles]).filter {
            let values = try $0.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey])
            return values.isRegularFile == true && values.isSymbolicLink != true
        }
    }
    private static func world(_ value: URL, under runtime: URL) throws -> URL {
        let root = try canonicalRoot(value)
        let saves = try descendant("saves", under: runtime)
        guard root.path.hasPrefix(saves.path + "/"), FileManager.default.fileExists(atPath: root.appendingPathComponent("level.dat").path) else {
            throw CommunityInstallError.unsafePath("请选择所选游戏目录 saves 中已有的 Minecraft 存档")
        }
        var cursor = value.standardizedFileURL
        while try canonicalRoot(cursor) != runtime {
            if (try? FileManager.default.destinationOfSymbolicLink(atPath: cursor.path)) != nil { throw CommunityInstallError.unsafePath(value.path) }
            let parent = cursor.deletingLastPathComponent()
            guard parent != cursor else { throw CommunityInstallError.unsafePath(value.path) }
            cursor = parent
        }
        return root
    }
    private static func readIndex(_ file: URL) throws -> [String: ResourceIndexEntry] {
        guard FileManager.default.fileExists(atPath: file.path) else { return [:] }
        return try JSONDecoder().decode([String: ResourceIndexEntry].self, from: Data(contentsOf: file))
    }
    private static func rejectDuplicateModIDs(_ tasks: [PreparedFile], existing: [URL]) throws {
        var ids: [String: URL] = [:]
        for file in existing where file.pathExtension.lowercased() == "jar" {
            for id in modIDs(file) { ids[id] = file }
        }
        for task in tasks {
            let source = task.staged ?? task.destination
            for id in modIDs(source) {
                if let previous = ids[id], previous != task.destination {
                    throw CommunityInstallError.collision("Mod \(id) 已存在于 \(previous.lastPathComponent)")
                }
                ids[id] = task.destination
            }
        }
    }
    private static func modIDs(_ file: URL) -> Set<String> {
        guard let archive = try? ModpackZIPArchive(url: file) else { return [] }
        for name in ["fabric.mod.json", "quilt.mod.json"] {
            if let entry = archive.member(name), let data = try? archive.data(for: entry),
               let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
                if let id = json["id"] as? String { return [id] }
                if let loader = json["quilt_loader"] as? [String: Any], let id = loader["id"] as? String { return [id] }
            }
        }
        for name in ["META-INF/mods.toml", "META-INF/neoforge.mods.toml"] {
            if let entry = archive.member(name), let data = try? archive.data(for: entry), let text = String(data: data, encoding: .utf8),
               let regex = try? NSRegularExpression(pattern: #"(?m)^\s*modId\s*=\s*["']([^"']+)["']"#) {
                return Set(regex.matches(in: text, range: NSRange(text.startIndex..., in: text)).compactMap {
                    Range($0.range(at: 1), in: text).map { String(text[$0]) }
                })
            }
        }
        return []
    }
}

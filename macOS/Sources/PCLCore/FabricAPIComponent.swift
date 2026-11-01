import CryptoKit
import Darwin
import Foundation

/// The active, top-level Fabric API JARs in the version's actual runtime folder.
/// Disabled JARs and other Mods never participate in a component replacement.
public struct FabricAPIState: Sendable, Hashable {
    public let files: [LocalModFile]
    public let fingerprint: String
    public var displayVersion: String? {
        guard !files.isEmpty else { return nil }
        return files.map { $0.version.isEmpty ? $0.filename : $0.version }.joined(separator: "、")
    }
    fileprivate let folder: URL
    fileprivate let identities: [String: FabricAPIFileIdentity]

    /// Compare whole-file identity, including renamed bundled JARs, without IO.
    public func matches(_ version: CommunityVersion) -> Bool {
        guard files.count == 1, let identity = identities[files[0].filename],
              version.source == .modrinth, version.projectID == FabricAPIComponent.projectID,
              let file = version.primaryFile else { return false }
        return identity.matches(file)
    }
}

/// Fabric API uses the same public resource catalogue and verified downloader as
/// the original launcher; this component never asks for an account or API key.
public actor FabricAPIComponent {
    public static let projectID = "P7dR8mSH"
    private let client: CommunityResourceClient
    private let downloader: CommunityResourceInstaller
    private var preparing = false

    public init(client: CommunityResourceClient? = nil, session: URLSession = .shared) {
        let catalogue = client ?? CommunityResourceClient(session: session)
        self.client = catalogue
        downloader = CommunityResourceInstaller(client: catalogue, session: session)
    }

    public func versions(minecraftVersion: String) async throws -> [CommunityVersion] {
        let project = try await client.project(id: "fabric-api", source: .modrinth)
        guard project.projectID == Self.projectID, project.kind == .mod else {
            throw CommunityResourceError.invalidResponse("Fabric API 项目身份不匹配")
        }
        return try await client.versions(for: project, minecraftVersion: minecraftVersion, loader: .fabric).filter {
            $0.projectID == Self.projectID && $0.source == .modrinth && $0.gameVersions.contains(minecraftVersion)
                && $0.loaders.contains("fabric") && $0.primaryFile?.filename.lowercased().hasSuffix(".jar") == true
        }
    }

    /// Only fully verified bytes are returned. Reuse tries the current/bundled
    /// JARs first, even if the user renamed them, before consulting the cache.
    public func prepare(version: CommunityVersion, gameDirectory: URL, reuseFiles: [URL] = [],
                        progress: @escaping @Sendable (InstallProgress) -> Void) async throws -> URL {
        guard !preparing else { throw CommunityInstallError.alreadyInstalling }
        preparing = true
        defer { preparing = false }
        guard version.source == .modrinth, version.projectID == Self.projectID,
              version.loaders.contains("fabric"), !version.gameVersions.isEmpty,
              let file = version.primaryFile, file.filename.lowercased().hasSuffix(".jar") else {
            throw CommunityInstallError.invalidFile("这不是 Fabric API 的客户端文件")
        }
        let name = try ModpackZIPArchive.validatedPath(file.filename)
        guard !name.contains("/") else { throw CommunityInstallError.unsafePath(file.filename) }
        let root = try FabricAPIPaths.canonicalRoot(gameDirectory)
        let key = file.sha512 ?? file.sha1 ?? file.md5 ?? ""
        guard !key.isEmpty else { throw CommunityInstallError.invalidFile("Fabric API 缺少完整文件校验值") }
        // fileMatches validates every advertised digest, size, HTTPS URL and name.
        _ = try CommunityResourceInstaller.fileMatches(at: root.appendingPathComponent(".pcl-no-such-api-\(UUID().uuidString)"), file: file)
        let cache = try FabricAPIPaths.descendant("PCL/download-cache/fabric-api/\(key.lowercased())", under: root)
        try FileManager.default.createDirectory(at: cache, withIntermediateDirectories: true)
        let destination = try FabricAPIPaths.descendant(name, under: cache)
        for existing in reuseFiles {
            try Task.checkCancellation()
            if try CommunityResourceInstaller.fileMatches(at: existing, file: file) {
                if try CommunityResourceInstaller.fileMatches(at: destination, file: file) { return destination }
                try Self.replaceCache(existing, destination: destination, file: file)
                progress(InstallProgress(message: "复用已有 Fabric API", completed: 1, total: 1))
                return destination
            }
        }
        if try CommunityResourceInstaller.fileMatches(at: destination, file: file) {
            progress(InstallProgress(message: "复用已校验的 Fabric API", completed: 1, total: 1))
            return destination
        }
        // A broken launcher-owned cache may be repaired; runtime files are never
        // passed to the downloader as an overwrite destination.
        if FileManager.default.fileExists(atPath: destination.path) {
            try FabricAPIPaths.regularFile(destination)
            try FileManager.default.removeItem(at: destination)
        }
        return try await downloader.download(version: version, to: destination, progress: progress)
    }

    public nonisolated static func installed(gameDirectory: URL, version: InstalledVersion, isolate: Bool) throws -> FabricAPIState {
        let folder = try LocalModManager.directory(gameDirectory: gameDirectory, version: version, isolate: isolate)
        let files = try LocalModManager.scan(directory: folder).filter { $0.enabled && $0.modID == "fabric-api" }
            .sorted { $0.filename < $1.filename }
        var identities: [String: FabricAPIFileIdentity] = [:]
        var snapshot = Data(folder.path.utf8)
        for file in files {
            let identity = try FabricAPIFileIdentity.read(file.url)
            identities[file.filename] = identity
            snapshot.append(0); snapshot.append(Data(file.filename.utf8)); snapshot.append(0)
            snapshot.append(Data(identity.sha256.utf8))
        }
        let fingerprint = SHA256.hash(data: snapshot).map { String(format: "%02x", $0) }.joined()
        return FabricAPIState(files: files, fingerprint: fingerprint, folder: folder, identities: identities)
    }

    /// Stage the entire replacement before moving any active JAR. The caller can
    /// roll this back if the companion client/metadata transaction fails.
    public nonisolated static func apply(preparedFile: URL?, gameDirectory: URL, version: InstalledVersion,
                                         isolate: Bool, expectedState: FabricAPIState) throws -> FabricAPITransaction {
        try Task.checkCancellation()
        let root = try FabricAPIPaths.canonicalRoot(gameDirectory)
        let current = try installed(gameDirectory: root, version: version, isolate: isolate)
        guard current.folder == expectedState.folder, current.fingerprint == expectedState.fingerprint else {
            throw CommunityInstallError.invalidFile("已安装的 Fabric API 已改变，请刷新修改页面后重试")
        }
        let staging = try FabricAPIPaths.descendant(".pcl-fabric-api-\(UUID().uuidString)", under: root)
        try FileManager.default.createDirectory(at: staging, withIntermediateDirectories: false)
        let transaction = FabricAPITransaction(root: root, folder: current.folder, staging: staging)
        do {
            var incoming: URL?, destination: URL?, incomingIdentity: FabricAPIFileIdentity?
            if let preparedFile {
                try FabricAPIPaths.regularFile(preparedFile)
                let name = try ModpackZIPArchive.validatedPath(preparedFile.lastPathComponent)
                guard !name.contains("/"), name.lowercased().hasSuffix(".jar") else { throw CommunityInstallError.invalidFile(name) }
                let staged = staging.appendingPathComponent("incoming.jar")
                try ModpackImporter.cloneOrCopy(preparedFile, to: staged)
                let mods = try LocalModManager.scan(directory: staging)
                guard mods.count == 1, mods[0].error == nil, mods[0].modID == "fabric-api" else {
                    throw CommunityInstallError.invalidFile("下载的 JAR 不是 Fabric API")
                }
                let identity = try FabricAPIFileIdentity.read(staged)
                if current.files.count == 1, current.identities[current.files[0].filename] == identity {
                    transaction.finish()
                    return transaction
                }
                incoming = staged; incomingIdentity = identity
                destination = try FabricAPIPaths.descendant(name, under: current.folder)
                if FileManager.default.fileExists(atPath: current.folder.path) {
                    let replaceable = Set(current.files.map { FabricAPIPaths.key($0.filename) })
                    for existing in try FileManager.default.contentsOfDirectory(atPath: current.folder.path)
                        where FabricAPIPaths.key(existing) == FabricAPIPaths.key(name) && !replaceable.contains(FabricAPIPaths.key(existing)) {
                        throw CommunityInstallError.collision(existing)
                    }
                }
            }
            try Task.checkCancellation()
            guard try installed(gameDirectory: root, version: version, isolate: isolate).fingerprint == expectedState.fingerprint else {
                throw CommunityInstallError.invalidFile("已安装的 Fabric API 已改变，请重试")
            }
            if incoming != nil { try FileManager.default.createDirectory(at: current.folder, withIntermediateDirectories: true) }
            for (index, file) in current.files.enumerated() {
                let backup = staging.appendingPathComponent("original-\(index).jar")
                try FileManager.default.moveItem(at: file.url, to: backup)
                transaction.backups.append((file.url, backup, current.identities[file.filename]!))
            }
            if let incoming, let destination, let incomingIdentity {
                // moveItem refuses an unexpected destination instead of replacing
                // a file created since the preflight above.
                try FileManager.default.moveItem(at: incoming, to: destination)
                transaction.published = (destination, incomingIdentity)
            }
            return transaction
        } catch {
            let failure = error
            try transaction.rollback()
            throw failure
        }
    }

    private nonisolated static func replaceCache(_ source: URL, destination: URL, file: CommunityResourceFile) throws {
        let temporary = destination.deletingLastPathComponent().appendingPathComponent(".pcl-api-cache-\(UUID().uuidString)")
        defer { try? FileManager.default.removeItem(at: temporary) }
        try ModpackImporter.cloneOrCopy(source, to: temporary)
        guard try CommunityResourceInstaller.fileMatches(at: temporary, file: file) else { throw CommunityInstallError.invalidFile(file.filename) }
        if FileManager.default.fileExists(atPath: destination.path) { try FabricAPIPaths.regularFile(destination) }
        try MinecraftInstaller.commit(temporary, to: destination)
    }
}

/// The backup sits outside the version directory, so finalizing after a profile
/// rename is safe. Rollback must precede a successful rename; finish is idempotent.
public final class FabricAPITransaction: @unchecked Sendable {
    private let root: URL
    private let folder: URL
    private let staging: URL
    fileprivate var backups: [(original: URL, backup: URL, identity: FabricAPIFileIdentity)] = []
    fileprivate var published: (url: URL, identity: FabricAPIFileIdentity)?
    private var completed = false
    private let lock = NSLock()

    fileprivate init(root: URL, folder: URL, staging: URL) {
        self.root = root; self.folder = folder; self.staging = staging
    }

    public func rollback() throws {
        lock.lock(); defer { lock.unlock() }
        guard !completed else { return }
        if published != nil || !backups.isEmpty { try FabricAPIPaths.unchangedFolder(folder, under: root) }
        if let published, FileManager.default.fileExists(atPath: published.url.path) {
            guard try FabricAPIFileIdentity.read(published.url) == published.identity else {
                throw LocalModError.rollback(staging.path)
            }
            try FileManager.default.removeItem(at: published.url)
            self.published = nil
        }
        for item in backups.reversed() {
            guard !FileManager.default.fileExists(atPath: item.original.path),
                  try FabricAPIFileIdentity.read(item.backup) == item.identity else { throw LocalModError.rollback(staging.path) }
            try FileManager.default.moveItem(at: item.backup, to: item.original)
        }
        backups.removeAll()
        completed = true
        try? FileManager.default.removeItem(at: staging)
    }

    public func finish() {
        lock.lock(); defer { lock.unlock() }
        guard !completed else { return }
        completed = true
        try? FileManager.default.removeItem(at: staging)
    }
}

fileprivate struct FabricAPIFileIdentity: Hashable, Sendable {
    let size: Int64
    let sha1: String
    let sha256: String
    let sha512: String
    let md5: String

    static func read(_ file: URL) throws -> FabricAPIFileIdentity {
        try FabricAPIPaths.regularFile(file)
        let descriptor = open(file.path, O_RDONLY | O_NOFOLLOW)
        guard descriptor >= 0 else { throw LocalModError.unsafePath(file.path) }
        let handle = FileHandle(fileDescriptor: descriptor, closeOnDealloc: true)
        defer { try? handle.close() }
        var status = stat()
        guard fstat(descriptor, &status) == 0, status.st_mode & S_IFMT == S_IFREG, status.st_nlink == 1,
              status.st_size >= 0, status.st_size <= 2 * 1024 * 1024 * 1024 else { throw LocalModError.unsafePath(file.path) }
        var sha1 = Insecure.SHA1(), sha256 = SHA256(), sha512 = SHA512(), md5 = Insecure.MD5()
        while let chunk = try handle.read(upToCount: 1024 * 1024), !chunk.isEmpty {
            sha1.update(data: chunk); sha256.update(data: chunk); sha512.update(data: chunk); md5.update(data: chunk)
        }
        return FabricAPIFileIdentity(size: status.st_size, sha1: hex(sha1.finalize()), sha256: hex(sha256.finalize()), sha512: hex(sha512.finalize()), md5: hex(md5.finalize()))
    }
    func matches(_ file: CommunityResourceFile) -> Bool {
        guard size == file.size, file.sha1 != nil || file.sha512 != nil || file.md5 != nil else { return false }
        return (file.sha1 == nil || file.sha1?.lowercased() == sha1)
            && (file.sha512 == nil || file.sha512?.lowercased() == sha512)
            && (file.md5 == nil || file.md5?.lowercased() == md5)
    }
    private static func hex<D: Sequence>(_ bytes: D) -> String where D.Element == UInt8 { bytes.map { String(format: "%02x", $0) }.joined() }
}

fileprivate enum FabricAPIPaths {
    static func canonicalRoot(_ root: URL) throws -> URL {
        try MinecraftVersionMetadata.safeFile(relativePath: ".pcl-root-probe-\(UUID().uuidString)", under: root).deletingLastPathComponent()
    }
    static func descendant(_ path: String, under root: URL) throws -> URL {
        let raw = root.appendingPathComponent(path)
        let safe = try MinecraftVersionMetadata.safeFile(relativePath: path, under: root)
        guard raw == safe else { throw LocalModError.unsafePath(raw.path) }
        return safe
    }
    static func key(_ name: String) -> String { name.precomposedStringWithCanonicalMapping.lowercased() }
    static func regularFile(_ file: URL) throws {
        let values = try file.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey])
        guard values.isRegularFile == true, values.isSymbolicLink != true,
              ((try FileManager.default.attributesOfItem(atPath: file.path)[.referenceCount]) as? NSNumber)?.intValue == 1 else {
            throw LocalModError.unsafePath(file.path)
        }
    }
    static func unchangedFolder(_ folder: URL, under root: URL) throws {
        guard folder.path.hasPrefix(root.path + "/"),
              (try? FileManager.default.destinationOfSymbolicLink(atPath: folder.path)) == nil,
              try MinecraftVersionMetadata.safeFile(relativePath: String(folder.path.dropFirst(root.path.count + 1)), under: root) == folder else {
            throw LocalModError.unsafePath(folder.path)
        }
    }
}

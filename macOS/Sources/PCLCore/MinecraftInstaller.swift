import Foundation
import CryptoKit

public struct RemoteVersion: Codable, Identifiable, Sendable {
    public let id: String
    public let type: String
    public let url: URL
    public let time: String?
    public let releaseTime: String?
    public let sha1: String?

    public init(id: String, type: String, url: URL, time: String? = nil, releaseTime: String? = nil, sha1: String? = nil) {
        self.id = id; self.type = type; self.url = url
        self.time = time; self.releaseTime = releaseTime; self.sha1 = sha1
    }
}

public struct LatestVersion: Decodable, Sendable {
    public let release: String
    public let snapshot: String
}

public struct VersionManifest: Decodable, Sendable {
    public let latest: LatestVersion
    public let versions: [RemoteVersion]
}

public struct InstallProgress: Sendable {
    public let message: String
    public let completed: Int
    public let total: Int
    public let snapshot: InstallTaskSnapshot?
    public var fraction: Double { total > 0 ? min(1, Double(completed) / Double(total)) : 0 }

    public init(message: String, completed: Int, total: Int, snapshot: InstallTaskSnapshot? = nil) {
        self.message = message; self.completed = completed; self.total = total
        self.snapshot = snapshot
    }
}

public enum MinecraftInstallError: LocalizedError {
    case invalidMetadata(String)
    case http(Int, String)
    case integrity(String)
    case unsafePath(String)
    case alreadyInstalling

    public var errorDescription: String? {
        switch self {
        case .invalidMetadata(let reason): return "版本信息无效：\(reason)"
        case .http(let code, let file): return "下载失败（HTTP \(code)）：\(file)"
        case .integrity(let file): return "文件校验失败，请重试：\(file)"
        case .unsafePath(let path): return "拒绝不安全的文件路径：\(path)"
        case .alreadyInstalling: return "已有版本正在安装，请等待完成。"
        }
    }
}

struct InstallArtifact: Sendable {
    let url: URL
    let destination: URL
    let sha1: String
    let size: Int64?
}

private struct InstallAssetIndex: Decodable {
    struct Object: Decodable { let hash: String; let size: Int64 }
    let objects: [String: Object]
    let virtual: Bool?
    let map_to_resources: Bool?
}

/// Installs official vanilla versions. The profile JSON is committed only after every dependency
/// passes its hash check, so interrupted downloads never appear as completed local profiles.
public actor MinecraftInstaller {
    private let session: URLSession
    private let manifestURL: URL
    private let maxConcurrentDownloads: Int
    private var isInstalling = false
    public static let officialManifestURL = URL(string: "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json")!
    static let officialManifestFallbackURL = URL(string: "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json")!

    public init(session: URLSession = .shared, manifestURL: URL = MinecraftInstaller.officialManifestURL,
                maxConcurrentDownloads: Int = 12) {
        self.session = session; self.manifestURL = manifestURL
        self.maxConcurrentDownloads = min(24, max(1, maxConcurrentDownloads))
    }

    public func fetchManifest() async throws -> VersionManifest {
        do {
            let data = try await Self.fetchData(url: manifestURL, session: session)
            return try JSONDecoder().decode(VersionManifest.self, from: data)
        } catch {
            try Task.checkCancellation()
            guard manifestURL == Self.officialManifestURL else { throw error }
            if let failure = error as? URLError {
                guard [.secureConnectionFailed, .networkConnectionLost, .timedOut,
                       .cannotFindHost, .cannotConnectToHost, .dnsLookupFailed].contains(failure.code) else { throw error }
            }
            // Both addresses are Mojang endpoints for the same official manifest.
            // Keep caller-provided sources isolated instead of silently replacing them.
            let data = try await Self.fetchData(url: Self.officialManifestFallbackURL, session: session)
            return try JSONDecoder().decode(VersionManifest.self, from: data)
        }
    }

    public func install(version: RemoteVersion, gameDirectory: URL, profileID: String? = nil,
                        progress: @escaping @Sendable (InstallProgress) -> Void) async throws {
        guard !isInstalling else { throw MinecraftInstallError.alreadyInstalling }
        isInstalling = true
        defer { isInstalling = false }
        try Task.checkCancellation()
        try Self.validateComponent(version.id)
        let installedID = profileID ?? version.id
        try Self.validateProfileID(installedID)
        let root = gameDirectory.standardizedFileURL
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        let monitor = InstallProgressContext.current
        monitor?.begin("vanillaJson")
        var currentStage = "vanillaJson"
        do {
            progress(InstallProgress(message: "获取 \(version.id) 的版本信息", completed: 0, total: 1))
            let metadataData = try await Self.fetchData(url: version.url, session: session)
            if let expected = version.sha1, Self.hash(metadataData) != expected.lowercased() {
                throw MinecraftInstallError.integrity("\(version.id).json")
            }
            guard let metadata = try JSONSerialization.jsonObject(with: metadataData) as? [String: Any],
                  metadata["id"] as? String == version.id,
                  let mainClass = metadata["mainClass"] as? String, !mainClass.isEmpty,
                  let downloads = metadata["downloads"] as? [String: Any],
                  let client = downloads["client"] as? [String: Any],
                  let libraries = metadata["libraries"] as? [[String: Any]],
                  let indexInfo = metadata["assetIndex"] as? [String: Any],
                  let indexID = indexInfo["id"] as? String else {
                throw MinecraftInstallError.invalidMetadata("缺少客户端、依赖库或资源索引")
            }
            try Self.validateComponent(indexID)
            let profileDirectory = try Self.safeDestination(root: root, relative: "versions/\(installedID)")
            let profileJSON = try Self.safeDestination(root: root, relative: "versions/\(installedID)/\(installedID).json")
            if FileManager.default.fileExists(atPath: profileJSON.path) {
                guard let existing = try JSONSerialization.jsonObject(with: Data(contentsOf: profileJSON)) as? [String: Any],
                      existing["inheritsFrom"] == nil,
                      existing["pclLoaderKind"] == nil,
                      existing["mainClass"] as? String == metadata["mainClass"] as? String,
                      (existing["pclOriginalVersion"] as? String ?? existing["id"] as? String) == version.id else {
                    throw MinecraftInstallError.invalidMetadata("目标版本已存在，且不属于 Minecraft \(version.id)，不能覆盖")
                }
            }
            monitor?.complete("vanillaJson")
            currentStage = "vanillaLibraries"
            monitor?.begin("vanillaLibraries")
            let clientDestination = try Self.safeDestination(root: root, relative: "versions/\(installedID)/\(installedID).jar")
            var artifacts = [try Self.artifact(client, destination: clientDestination)]
            let librariesDirectory = try Self.safeDestination(root: root, relative: "libraries")
            for library in libraries where InstallPlatform.libraryAllowed(library) {
                guard let information = library["downloads"] as? [String: Any] else {
                    throw MinecraftInstallError.invalidMetadata("依赖库没有下载信息")
                }
                if let artifact = information["artifact"] as? [String: Any] {
                    guard let path = artifact["path"] as? String else { throw MinecraftInstallError.invalidMetadata("依赖库没有路径") }
                    artifacts.append(try Self.artifact(artifact, destination: Self.safeDestination(root: librariesDirectory, relative: path)))
                }
                if let classifier = InstallPlatform.nativeClassifier(for: library) {
                    guard let classifiers = information["classifiers"] as? [String: Any],
                          let native = classifiers[classifier] as? [String: Any], let path = native["path"] as? String else {
                        throw MinecraftInstallError.invalidMetadata("缺少 macOS 本地依赖：\(classifier)")
                    }
                    artifacts.append(try Self.artifact(native, destination: Self.safeDestination(root: librariesDirectory, relative: path)))
                }
            }
            if let logging = metadata["logging"] as? [String: Any],
               let loggingClient = logging["client"] as? [String: Any],
               let loggingFile = loggingClient["file"] as? [String: Any],
               let loggingID = loggingFile["id"] as? String {
                try Self.validateComponent(loggingID)
                let destination = try Self.safeDestination(root: root, relative: "assets/log_configs/\(loggingID)")
                artifacts.append(try Self.artifact(loggingFile, destination: destination))
            }
            monitor?.begin("vanillaAssets")
            currentStage = "vanillaAssets"
            monitor?.setRemainingFiles(1, for: "base-assets")
            let indexURL = try Self.safeDestination(root: root, relative: "assets/indexes/\(indexID).json")
            let indexArtifact = try Self.artifact(indexInfo, destination: indexURL)
            try await InstallProgressContext.$stageID.withValue("vanillaAssets") {
                try await Self.download(indexArtifact, session: session)
            }
            let index = try JSONDecoder().decode(InstallAssetIndex.self, from: Data(contentsOf: indexURL))
            let supportPaths = Set(artifacts.map { $0.destination.path })
            var seenObjects = Set<String>()
            for object in index.objects.values {
                guard Self.validSHA1(object.hash), object.size >= 0 else { throw MinecraftInstallError.invalidMetadata("资源哈希或大小无效") }
                guard seenObjects.insert(object.hash).inserted else { continue }
                let prefix = String(object.hash.prefix(2))
                let destination = try Self.safeDestination(root: root, relative: "assets/objects/\(prefix)/\(object.hash)")
                artifacts.append(InstallArtifact(url: URL(string: "https://resources.download.minecraft.net/\(prefix)/\(object.hash)")!,
                                                 destination: destination, sha1: object.hash, size: object.size))
            }
            // Deduplicate destinations before parallel writes (some historical metadata repeats jars).
            var seenPaths = Set<String>()
            artifacts = artifacts.filter { seenPaths.insert($0.destination.path).inserted }
            let total = artifacts.count + 2
            var completed = 1
            progress(InstallProgress(message: "校验并下载游戏文件", completed: completed, total: total))
            let downloadSession = session
            let supportCount = artifacts.filter { supportPaths.contains($0.destination.path) }.count
            let assetsCount = artifacts.count - supportCount
            var supportCompleted = 0, assetsCompleted = 0
            monitor?.setRemainingFiles(supportCount, for: "base-support")
            monitor?.setRemainingFiles(assetsCount, for: "base-assets")
            if supportCount == 0 { monitor?.complete("vanillaLibraries") }
            // Resource indexing is one real unit of work; local verification and
            // cache reuse count exactly like a completed download for file progress.
            monitor?.update("vanillaAssets", fraction: 1 / Double(assetsCount + 1))
            try await withThrowingTaskGroup(of: (String, Bool).self) { group in
                var iterator = artifacts.makeIterator()
                func add(_ item: InstallArtifact) {
                    let support = supportPaths.contains(item.destination.path)
                    let stage = support ? "vanillaLibraries" : "vanillaAssets"
                    group.addTask {
                        try await InstallProgressContext.$stageID.withValue(stage) {
                            do {
                                try await Self.download(item, session: downloadSession)
                                return (item.destination.lastPathComponent, support)
                            } catch {
                                if !Task.isCancelled { monitor?.fail(stage, message: error.localizedDescription) }
                                throw error
                            }
                        }
                    }
                }
                for _ in 0..<min(maxConcurrentDownloads, artifacts.count) {
                    if let item = iterator.next() { add(item) }
                }
                while let (filename, support) = try await group.next() {
                    try Task.checkCancellation()
                    completed += 1
                    if support {
                        supportCompleted += 1
                        monitor?.setRemainingFiles(supportCount - supportCompleted, for: "base-support")
                        monitor?.update("vanillaLibraries", fraction: Double(supportCompleted) / Double(max(1, supportCount)))
                        if supportCompleted == supportCount { monitor?.complete("vanillaLibraries") }
                    } else {
                        assetsCompleted += 1
                        monitor?.setRemainingFiles(assetsCount - assetsCompleted, for: "base-assets")
                        monitor?.update("vanillaAssets", fraction: Double(assetsCompleted + 1) / Double(assetsCount + 1))
                    }
                    progress(InstallProgress(message: "校验文件 · \(filename)", completed: completed, total: total))
                    if let item = iterator.next() { add(item) }
                }
            }
            try Task.checkCancellation()
            var legacyLayouts: [String] = []
            if index.virtual == true { legacyLayouts.append("assets/virtual/\(indexID)") }
            if index.map_to_resources == true { legacyLayouts.append("resources") }
            for relativeDirectory in legacyLayouts {
                let virtualDirectory = try Self.safeDestination(root: root, relative: relativeDirectory)
                for (name, object) in index.objects {
                    try Task.checkCancellation()
                    let target = try Self.safeDestination(root: virtualDirectory, relative: name)
                    let source = root.appendingPathComponent("assets/objects/\(object.hash.prefix(2))/\(object.hash)")
                    if try Self.matches(target, sha1: object.hash, size: object.size) { continue }
                    try FileManager.default.createDirectory(at: target.deletingLastPathComponent(), withIntermediateDirectories: true)
                    // Atomic copies also repair a stale virtual resource from an older install.
                    let temporary = target.deletingLastPathComponent().appendingPathComponent(".\(UUID().uuidString).partial")
                    defer { try? FileManager.default.removeItem(at: temporary) }
                    try FileManager.default.copyItem(at: source, to: temporary)
                    try Self.commit(temporary, to: target)
                }
            }
            try Task.checkCancellation()
            try FileManager.default.createDirectory(at: profileDirectory, withIntermediateDirectories: true)
            var installedMetadata = metadata
            installedMetadata["id"] = installedID
            installedMetadata["pclOriginalVersion"] = version.id
            if installedMetadata["jar"] != nil { installedMetadata["jar"] = installedID }
            let installedData = try JSONSerialization.data(withJSONObject: installedMetadata, options: [.prettyPrinted, .sortedKeys])
            try installedData.write(to: profileJSON, options: .atomic)
            monitor?.complete("vanillaAssets")
            progress(InstallProgress(message: "\(installedID) 安装完成", completed: total, total: total))
        } catch {
            if !Task.isCancelled, monitor?.snapshot.stages.contains(where: { $0.state == .failed }) != true {
                monitor?.fail(currentStage, message: error.localizedDescription)
            }
            throw error
        }
    }

    /// Allows descriptive Unicode profile names while retaining PCL's folder-name restrictions.
    public static func validateProfileID(_ value: String) throws {
        let forbidden = CharacterSet(charactersIn: "/\\:*?\"<>|!;").union(.controlCharacters)
        guard !value.isEmpty, value != ".", value != "..", !value.hasPrefix("."),
              value == value.trimmingCharacters(in: .whitespacesAndNewlines), !value.hasSuffix("."),
              value.utf8.count <= 240, value.rangeOfCharacter(from: forbidden) == nil else {
            throw MinecraftInstallError.unsafePath("版本名不能为空，不能包含路径符号、控制字符、感叹号或分号，也不能以点或空格结尾")
        }
    }

    static func validateComponent(_ value: String) throws {
        guard !value.isEmpty, value != ".", value != "..",
              value == value.trimmingCharacters(in: .whitespacesAndNewlines),
              value.range(of: #"^[A-Za-z0-9._+ \-]+$"#, options: .regularExpression) != nil else {
            throw MinecraftInstallError.unsafePath(value)
        }
    }

    static func safeDestination(root: URL, relative: String) throws -> URL {
        let components = relative.split(separator: "/", omittingEmptySubsequences: false)
        guard !relative.isEmpty, !relative.hasPrefix("/"), !relative.contains("\\"),
              !components.contains(where: { $0.isEmpty || $0 == "." || $0 == ".." }) else {
            throw MinecraftInstallError.unsafePath(relative)
        }
        // Share the launcher's component-by-component resolver. Foundation's convenience
        // resolver can leave a symlink parent unresolved when the final file does not exist.
        do {
            return try MinecraftVersionMetadata.safeFile(relativePath: relative, under: root)
        } catch {
            throw MinecraftInstallError.unsafePath(relative)
        }
    }

    static func artifact(_ info: [String: Any], destination: URL) throws -> InstallArtifact {
        guard let rawURL = info["url"] as? String, let url = URL(string: rawURL), url.scheme == "https",
              let sha1 = info["sha1"] as? String, validSHA1(sha1) else {
            throw MinecraftInstallError.invalidMetadata("下载地址或 SHA-1 无效")
        }
        let size = (info["size"] as? NSNumber)?.int64Value
        if let size, size < 0 { throw MinecraftInstallError.invalidMetadata("文件大小无效") }
        return InstallArtifact(url: url, destination: destination, sha1: sha1.lowercased(), size: size)
    }

    static func validSHA1(_ value: String) -> Bool {
        value.range(of: #"^[0-9a-fA-F]{40}$"#, options: .regularExpression) != nil
    }

    static func hash(_ data: Data) -> String { Insecure.SHA1.hash(data: data).map { String(format: "%02x", $0) }.joined() }

    static func matches(_ url: URL, sha1: String, size: Int64?) throws -> Bool {
        guard FileManager.default.fileExists(atPath: url.path) else { return false }
        let attributes = try FileManager.default.attributesOfItem(atPath: url.path)
        guard attributes[.type] as? FileAttributeType == .typeRegular else { return false }
        if let size, (attributes[.size] as? NSNumber)?.int64Value != size { return false }
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }
        var hasher = Insecure.SHA1()
        while let chunk = try handle.read(upToCount: 1024 * 1024), !chunk.isEmpty {
            try Task.checkCancellation()
            hasher.update(data: chunk)
        }
        let digest = hasher.finalize().map { String(format: "%02x", $0) }.joined()
        return digest == sha1.lowercased()
    }

    public static func fetchData(url: URL, session: URLSession = .shared) async throws -> Data {
        guard url.scheme == "https" else { throw MinecraftInstallError.invalidMetadata("下载必须使用 HTTPS") }
        var request = URLRequest(url: url)
        request.timeoutInterval = 60
        var lastError: Error?
        for attempt in 0..<3 {
            do {
                try Task.checkCancellation()
                let (data, response) = try await PCLHTTPS.data(for: request, session: session)
                guard let http = response as? HTTPURLResponse, (200..<300).contains(http.statusCode) else {
                    throw MinecraftInstallError.http((response as? HTTPURLResponse)?.statusCode ?? 0, url.lastPathComponent)
                }
                try Task.checkCancellation()
                return data
            } catch {
                try Task.checkCancellation()
                guard let failure = error as? URLError,
                      [.secureConnectionFailed, .networkConnectionLost, .timedOut].contains(failure.code) else { throw error }
                lastError = error
                if attempt < 2 { try await Task.sleep(nanoseconds: UInt64(attempt + 1) * 400_000_000) }
            }
        }
        throw lastError ?? URLError(.unknown)
    }

    static func download(_ item: InstallArtifact, session: URLSession) async throws {
        try Task.checkCancellation()
        if try matches(item.destination, sha1: item.sha1, size: item.size) { return }
        try FileManager.default.createDirectory(at: item.destination.deletingLastPathComponent(), withIntermediateDirectories: true)
        var lastError: Error?
        for attempt in 0..<3 {
            do {
                var request = URLRequest(url: item.url)
                request.timeoutInterval = 90
                let (temporary, response) = try await PCLHTTPS.download(for: request, session: session)
                defer { try? FileManager.default.removeItem(at: temporary) }
                guard let http = response as? HTTPURLResponse, (200..<300).contains(http.statusCode) else {
                    throw MinecraftInstallError.http((response as? HTTPURLResponse)?.statusCode ?? 0, item.destination.lastPathComponent)
                }
                guard try matches(temporary, sha1: item.sha1, size: item.size) else {
                    throw MinecraftInstallError.integrity(item.destination.lastPathComponent)
                }
                try Task.checkCancellation()
                // Place the staging file beside the destination for same-volume atomic replacement.
                let staging = item.destination.deletingLastPathComponent().appendingPathComponent(".\(UUID().uuidString).partial")
                defer { try? FileManager.default.removeItem(at: staging) }
                try FileManager.default.copyItem(at: temporary, to: staging)
                try commit(staging, to: item.destination)
                return
            } catch {
                try Task.checkCancellation()
                if let urlError = error as? URLError, urlError.code == .cancelled { throw CancellationError() }
                lastError = error
                if attempt < 2 { try await Task.sleep(nanoseconds: UInt64(attempt + 1) * 400_000_000) }
            }
        }
        throw lastError ?? MinecraftInstallError.integrity(item.destination.lastPathComponent)
    }

    static func commit(_ source: URL, to destination: URL) throws {
        if FileManager.default.fileExists(atPath: destination.path) {
            _ = try FileManager.default.replaceItemAt(destination, withItemAt: source)
        } else {
            try FileManager.default.moveItem(at: source, to: destination)
        }
    }
}

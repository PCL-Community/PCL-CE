import Foundation
import CryptoKit
import Compression
import Darwin
import zlib

public enum ModpackFormat: String, Codable, CaseIterable, Sendable {
    case modrinth, curseForge, multiMC, mcbbs, completeVersion
    public var displayName: String {
        switch self {
        case .modrinth: return "Modrinth"
        case .curseForge: return "CurseForge"
        case .multiMC: return "MultiMC / Prism Launcher"
        case .mcbbs: return "MCBBS"
        case .completeVersion: return "PCL / 完整版本 ZIP"
        }
    }
}

public struct ModpackDescriptor: Identifiable, Sendable {
    public var id: String { name + ":" + minecraftVersion + ":" + format.rawValue }
    public let name: String
    public let format: ModpackFormat
    public let minecraftVersion: String
    public let loaderKind: ModLoaderKind?
    public let loaderVersion: String?
    public let fileCount: Int
    public let summary: String
    public let warnings: [String]
    public let needsAPIKey: Bool
}

public struct ModpackImportLimits: Sendable {
    public var maximumEntries: Int
    public var maximumEntryBytes: Int64
    public var maximumExpandedBytes: Int64
    public var maximumCompressionRatio: Int64
    public init(maximumEntries: Int = 100_000, maximumEntryBytes: Int64 = 512 * 1024 * 1024,
                maximumExpandedBytes: Int64 = 12 * 1024 * 1024 * 1024, maximumCompressionRatio: Int64 = 2_000) {
        self.maximumEntries = maximumEntries; self.maximumEntryBytes = maximumEntryBytes
        self.maximumExpandedBytes = maximumExpandedBytes; self.maximumCompressionRatio = maximumCompressionRatio
    }
}

public enum ModpackImportError: LocalizedError {
    case invalidArchive(String), invalidManifest(String), unsupported(String), unsafePath(String)
    case destinationExists, unavailableFile(String), integrity(String), sharedConflict(String), alreadyImporting
    public var errorDescription: String? {
        switch self {
        case .invalidArchive(let message): return "整合包压缩文件无效：\(message)"
        case .invalidManifest(let message): return "整合包安装信息无效：\(message)"
        case .unsupported(let message): return "暂时无法导入此整合包：\(message)"
        case .unsafePath(let path): return "整合包含有不安全的路径或符号链接：\(path)"
        case .destinationExists: return "已有同名版本，请使用其他名称。原版本未被覆盖。"
        case .unavailableFile(let name): return "整合包所需文件无法下载：\(name)。导入未完成，请在来源平台确认该文件仍可分发。"
        case .integrity(let name): return "整合包文件校验失败：\(name)"
        case .sharedConflict(let path): return "共享游戏文件与整合包不一致，未覆盖现有文件：\(path)"
        case .alreadyImporting: return "已有整合包正在导入，请等待完成。"
        }
    }
}

/// ZIP parsing and extraction share one validated central directory. Members are decoded as
/// bytes; archive names never become extraction-tool arguments and symlinks are never created.
struct ModpackZIPArchive: Sendable {
    struct Entry: Sendable {
        let path: String
        let rawName: Data
        let method: UInt16
        let flags: UInt16
        let crc32: UInt32
        let compressedSize: Int
        let size: Int
        let offset: UInt64
        let isDirectory: Bool
    }
    let url: URL
    let entries: [Entry]
    let centralDirectoryOffset: UInt64
    let limits: ModpackImportLimits

    init(url: URL, limits: ModpackImportLimits = .init()) throws {
        self.url = url; self.limits = limits
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }
        let fileSize = try handle.seekToEnd()
        guard fileSize >= 22 else { throw ModpackImportError.invalidArchive("不是 ZIP 文件") }
        let tailSize = Int(min(fileSize, 65_557))
        try handle.seek(toOffset: fileSize - UInt64(tailSize))
        let tail = try Self.readExactly(handle, count: tailSize)
        var end: Int?
        for index in stride(from: tail.count - 22, through: 0, by: -1) {
            if try tail.le32(index) == 0x06054b50, index + 22 + Int(try tail.le16(index + 20)) == tail.count { end = index; break }
        }
        guard let end else { throw ModpackImportError.invalidArchive("缺少 ZIP 目录结束记录") }
        let count = Int(try tail.le16(end + 10))
        let directorySize = UInt64(try tail.le32(end + 12))
        let directoryOffset = UInt64(try tail.le32(end + 16))
        guard try tail.le16(end + 4) == 0, try tail.le16(end + 6) == 0,
              try tail.le16(end + 8) == UInt16(count) else { throw ModpackImportError.unsupported("不支持分卷 ZIP") }
        guard count != 65_535, directoryOffset != UInt64(UInt32.max), directorySize != UInt64(UInt32.max) else {
            throw ModpackImportError.unsupported("请将 ZIP64 大包拆成普通 ZIP，或使用 Modrinth 格式导出")
        }
        guard count > 0, count <= limits.maximumEntries, directorySize <= 64 * 1024 * 1024,
              directoryOffset + directorySize <= fileSize - UInt64(tailSize) + UInt64(end) else {
            throw ModpackImportError.invalidArchive("文件数量、目录大小或偏移超出限制")
        }
        self.centralDirectoryOffset = directoryOffset
        try handle.seek(toOffset: directoryOffset)
        let directory = try Self.readExactly(handle, count: Int(directorySize))
        var result: [Entry] = [], seen = Set<String>(), files = Set<String>(), total: Int64 = 0, cursor = 0
        for _ in 0..<count {
            try Task.checkCancellation()
            guard try directory.le32(cursor) == 0x02014b50, cursor + 46 <= directory.count else {
                throw ModpackImportError.invalidArchive("中央目录损坏")
            }
            let flags = try directory.le16(cursor + 8), method = try directory.le16(cursor + 10)
            let compressed = Int(try directory.le32(cursor + 20)), expanded = Int(try directory.le32(cursor + 24))
            let nameLength = Int(try directory.le16(cursor + 28)), extraLength = Int(try directory.le16(cursor + 30))
            let commentLength = Int(try directory.le16(cursor + 32)), attributes = try directory.le32(cursor + 38)
            let next = cursor + 46 + nameLength + extraLength + commentLength
            guard nameLength > 0, next <= directory.count, try directory.le16(cursor + 34) == 0,
                  flags & 1 == 0, [UInt16(0), 8].contains(method) else {
                throw ModpackImportError.unsupported("不支持加密、分卷或此压缩算法的 ZIP")
            }
            let rawName = directory.subdata(in: cursor + 46..<cursor + 46 + nameLength)
            guard let rawPath = String(data: rawName, encoding: .utf8) else { throw ModpackImportError.unsupported("文件名需要采用 UTF-8 编码") }
            let path = try Self.validatedPath(rawPath)
            let isDirectory = rawPath.hasSuffix("/") || attributes & 0x10 != 0
            let unixType = (attributes >> 16) & 0o170000
            guard unixType == 0 || unixType == 0o100000 || unixType == 0o040000,
                  unixType != 0o040000 || isDirectory else { throw ModpackImportError.unsafePath(rawPath) }
            let key = Self.pathKey(path)
            guard seen.insert(key).inserted else { throw ModpackImportError.invalidArchive("存在重复或大小写冲突的路径：\(path)") }
            if !isDirectory { files.insert(key) }
            guard Int64(expanded) <= limits.maximumEntryBytes, Int64(compressed) <= limits.maximumEntryBytes,
                  !isDirectory || expanded == 0 else { throw ModpackImportError.invalidArchive("单个文件过大") }
            total += Int64(expanded)
            guard total <= limits.maximumExpandedBytes,
                  expanded <= 4 * 1024 * 1024 || Int64(expanded) <= max(1, Int64(compressed)) * limits.maximumCompressionRatio else {
                throw ModpackImportError.invalidArchive("解压大小或压缩率超出限制")
            }
            result.append(Entry(path: path, rawName: rawName, method: method, flags: flags,
                                crc32: try directory.le32(cursor + 16), compressedSize: compressed, size: expanded,
                                offset: UInt64(try directory.le32(cursor + 42)), isDirectory: isDirectory))
            cursor = next
        }
        guard cursor == directory.count else { throw ModpackImportError.invalidArchive("ZIP 目录包含额外数据") }
        for entry in result {
            let components = Self.pathKey(entry.path).split(separator: "/")
            for length in 1..<components.count {
                if files.contains(components.prefix(length).joined(separator: "/")) { throw ModpackImportError.unsafePath(entry.path) }
            }
        }
        self.entries = result
    }

    static func validatedPath(_ raw: String) throws -> String {
        let value = raw.replacingOccurrences(of: "\\", with: "/")
        let normalized = value.hasSuffix("/") ? String(value.dropLast()) : value
        let components = normalized.split(separator: "/", omittingEmptySubsequences: false)
        guard !normalized.isEmpty, normalized.utf8.count <= 4096, !value.hasPrefix("/"),
              normalized.rangeOfCharacter(from: .controlCharacters) == nil,
              !components.contains(where: { $0.isEmpty || $0 == "." || $0 == ".." || $0.contains(":") || $0.utf8.count > 255 }) else {
            throw ModpackImportError.unsafePath(raw)
        }
        return normalized.precomposedStringWithCanonicalMapping
    }
    static func pathKey(_ value: String) -> String { value.precomposedStringWithCanonicalMapping.lowercased() }

    func member(_ path: String) -> Entry? { entries.first { $0.path == path && !$0.isDirectory } }
    func data(for entry: Entry, maximumBytes: Int = 8 * 1024 * 1024) throws -> Data {
        guard !entry.isDirectory, entry.size <= maximumBytes else { throw ModpackImportError.invalidArchive("元数据文件过大") }
        return try decoded(entry)
    }
    func write(_ entry: Entry, to destination: URL) throws {
        try Task.checkCancellation()
        if entry.isDirectory { try FileManager.default.createDirectory(at: destination, withIntermediateDirectories: true); return }
        let data = try decoded(entry)
        try FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
        try data.write(to: destination, options: .atomic)
    }
    private func decoded(_ entry: Entry) throws -> Data {
        try Task.checkCancellation()
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }
        guard entry.offset + 30 <= centralDirectoryOffset else { throw ModpackImportError.invalidArchive("ZIP 成员偏移无效") }
        try handle.seek(toOffset: entry.offset)
        let header = try Self.readExactly(handle, count: 30)
        guard try header.le32(0) == 0x04034b50, try header.le16(6) == entry.flags,
              try header.le16(8) == entry.method else { throw ModpackImportError.invalidArchive("本地 ZIP 头不一致") }
        let nameLength = Int(try header.le16(26)), extraLength = Int(try header.le16(28))
        let name = try Self.readExactly(handle, count: nameLength)
        guard name == entry.rawName else { throw ModpackImportError.invalidArchive("本地文件名与 ZIP 目录不一致") }
        let start = entry.offset + UInt64(30 + nameLength + extraLength)
        guard start + UInt64(entry.compressedSize) <= centralDirectoryOffset else { throw ModpackImportError.invalidArchive("ZIP 成员越界") }
        try handle.seek(toOffset: start)
        let compressed = try Self.readExactly(handle, count: entry.compressedSize)
        let output: Data
        if entry.method == 0 {
            guard entry.compressedSize == entry.size else { throw ModpackImportError.invalidArchive("未压缩文件大小不一致") }
            output = compressed
        } else {
            var decoded = Data(count: max(1, entry.size))
            let written = decoded.withUnsafeMutableBytes { destination in
                compressed.withUnsafeBytes { source -> Int in
                    guard let source = source.baseAddress?.assumingMemoryBound(to: UInt8.self),
                          let destination = destination.baseAddress?.assumingMemoryBound(to: UInt8.self) else { return 0 }
                    return compression_decode_buffer(destination, max(1, entry.size), source, compressed.count, nil, COMPRESSION_ZLIB)
                }
            }
            guard written == entry.size else { throw ModpackImportError.invalidArchive("DEFLATE 文件损坏：\(entry.path)") }
            output = entry.size == 0 ? Data() : decoded
        }
        guard try ModpackCRC32.checksum(output) == entry.crc32 else { throw ModpackImportError.integrity(entry.path) }
        return output
    }
    private static func readExactly(_ handle: FileHandle, count: Int) throws -> Data {
        guard count >= 0 else { throw ModpackImportError.invalidArchive("负数文件长度") }
        let value = try handle.read(upToCount: count) ?? Data()
        guard value.count == count else { throw ModpackImportError.invalidArchive("ZIP 文件被截断") }
        return value
    }
}

private extension Data {
    func le16(_ index: Int) throws -> UInt16 {
        guard index >= 0, index + 2 <= count else { throw ModpackImportError.invalidArchive("ZIP 头被截断") }
        return UInt16(self[index]) | UInt16(self[index + 1]) << 8
    }
    func le32(_ index: Int) throws -> UInt32 {
        guard index >= 0, index + 4 <= count else { throw ModpackImportError.invalidArchive("ZIP 头被截断") }
        return UInt32(self[index]) | UInt32(self[index + 1]) << 8 | UInt32(self[index + 2]) << 16 | UInt32(self[index + 3]) << 24
    }
}

private enum ModpackCRC32 {
    static func checksum(_ data: Data) throws -> UInt32 {
        try data.withUnsafeBytes { buffer in
            let bytes = buffer.bindMemory(to: Bytef.self)
            var result: uLong = 0
            for offset in stride(from: 0, to: data.count, by: 4 * 1024 * 1024) {
                try Task.checkCancellation()
                let count = min(4 * 1024 * 1024, data.count - offset)
                result = zlib.crc32(result, bytes.baseAddress!.advanced(by: offset), uInt(count))
            }
            return UInt32(truncatingIfNeeded: result)
        }
    }
}

private struct ModpackDownload: Sendable {
    let path: String
    let urls: [URL]
    let sha1: String?
    let sha512: String?
    let md5: String?
    let size: Int64
}

private struct ModpackPlan: @unchecked Sendable {
    let descriptor: ModpackDescriptor
    let prefix: String
    let manifest: [String: Any]
    let overridePrefixes: [String]
    let completeProfileID: String?
    let completeRootPrefix: String?
    let instanceSettings: [String: String]
}

public actor ModpackImporter {
    private let session: URLSession
    private let installer: MinecraftInstaller
    private let loaderInstaller: ModLoaderInstaller
    private let curseForgeResolver: CurseForgeResolver
    private let limits: ModpackImportLimits
    private let configuredCurseForgeKey: String?
    private var importing = false

    public init(session: URLSession = .shared, installer: MinecraftInstaller? = nil,
                loaderInstaller: ModLoaderInstaller? = nil, curseForgeAPIKey: String? = nil,
                limits: ModpackImportLimits = .init()) {
        let vanilla = installer ?? MinecraftInstaller(session: session)
        self.session = session; self.installer = vanilla
        self.loaderInstaller = loaderInstaller ?? ModLoaderInstaller(session: session, vanillaInstaller: vanilla)
        self.curseForgeResolver = CurseForgeResolver(session: session)
        self.configuredCurseForgeKey = curseForgeAPIKey
        self.limits = limits
    }

    public func inspect(archive: URL) async throws -> ModpackDescriptor {
        try Self.plan(ModpackZIPArchive(url: archive, limits: limits)).descriptor
    }

    public func importPack(archive: URL, gameDirectory: URL, profileID: String, javaExecutable: URL,
                           progress: @escaping @Sendable (InstallProgress) -> Void,
                           curseForgeAPIKey: String? = nil) async throws {
        guard !importing else { throw ModpackImportError.alreadyImporting }
        importing = true
        defer { importing = false }
        try MinecraftInstaller.validateProfileID(profileID)
        try Task.checkCancellation()
        let zip = try ModpackZIPArchive(url: archive, limits: limits)
        let plan = try Self.plan(zip)
        let key = curseForgeAPIKey ?? configuredCurseForgeKey ?? ProcessInfo.processInfo.environment["PCL_CURSEFORGE_API_KEY"]
        let fm = FileManager.default
        try fm.createDirectory(at: gameDirectory, withIntermediateDirectories: true)
        // Directory enumerators return canonical paths (/private/var on macOS). Use
        // the same spelling before computing relative cache paths.
        guard let resolvedRoot = realpath(gameDirectory.path, nil) else { throw CocoaError(.fileReadUnknown) }
        let root = URL(fileURLWithPath: String(cString: resolvedRoot), isDirectory: true)
        free(resolvedRoot)
        let versions = try MinecraftInstaller.safeDestination(root: root, relative: "versions")
        try fm.createDirectory(at: versions, withIntermediateDirectories: true)
        let target = try MinecraftInstaller.safeDestination(root: root, relative: "versions/\(profileID)")
        let existingNames = try fm.contentsOfDirectory(atPath: versions.path)
        if fm.fileExists(atPath: target.path) || existingNames.contains(where: { $0.caseInsensitiveCompare(profileID) == .orderedSame }) {
            throw ModpackImportError.destinationExists
        }
        let staging = try MinecraftInstaller.safeDestination(root: root, relative: ".pcl-import-\(UUID().uuidString)")
        try fm.createDirectory(at: staging, withIntermediateDirectories: true)
        defer { try? fm.removeItem(at: staging) }
        let stagedProfile = staging.appendingPathComponent("profile-output")
        let bundleRoot = staging.appendingPathComponent("bundle-output")
        try fm.createDirectory(at: stagedProfile, withIntermediateDirectories: true)
        try fm.createDirectory(at: bundleRoot, withIntermediateDirectories: true)
        let payloads = try Self.payloads(in: zip, plan: plan, targetID: profileID)
        let monitor = InstallTaskMonitor(title: "\(plan.descriptor.format.displayName) 整合包安装： \(profileID)",
            stages: Self.importStages(plan: plan, zip: zip), progress: progress)
        let downloadSession = session, vanillaInstaller = installer, modLoaderInstaller = loaderInstaller
        let resolver = curseForgeResolver, importLimits = limits
        var sourceProfileID = "", bundledFiles = 0, bundledMods = 0
        var downloadResult = (reused: 0, downloaded: 0), resolvedFiles = 0
        do {
            try await InstallProgressContext.$current.withValue(monitor) {
                try await withThrowingTaskGroup(of: ImportBranchResult.self) { group in
                    group.addTask {
                        let counts = try await Self.detachedWork {
                            try InstallProgressContext.$stageID.withValue("extract") {
                                try InstallProgressContext.$current.withValue(monitor) {
                                    monitor.begin("extract")
                                    do {
                                        let total = max(1, payloads.reduce(Int64(0)) { $0 + Int64($1.entry.size) })
                                        var done: Int64 = 0, mods = 0
                                        for payload in payloads {
                                            try Task.checkCancellation()
                                            try Self.writePayload(payload.entry, relative: payload.path, zip: zip, profile: bundleRoot, targetID: profileID)
                                            done += Int64(payload.entry.size)
                                            if payload.path.hasPrefix("mods/"), payload.path.lowercased().hasSuffix(".jar"), payload.path.split(separator: "/").count == 2 { mods += 1 }
                                            monitor.update("extract", fraction: Double(done) / Double(total))
                                        }
                                        monitor.complete("extract")
                                        return (payloads.count, mods)
                                    } catch { monitor.fail("extract", message: error.localizedDescription); throw error }
                                }
                            }
                        }
                        return .bundle(counts.0, counts.1)
                    }
                    group.addTask {
                        do {
                            monitor.begin("resolve")
                            let downloads: [ModpackDownload]
                            if plan.descriptor.format == .modrinth { downloads = try Self.modrinthDownloads(plan.manifest, limits: importLimits) }
                            else if plan.descriptor.format == .curseForge {
                                let refs = try Self.curseForgeReferences(plan.manifest).map { CurseForgeFileReference(projectID: $0.0, fileID: $0.1) }
                                let values = try await resolver.resolve(refs, apiKey: key) { done, total in
                                    monitor.update("resolve", fraction: Double(done) / Double(max(1, total)))
                                }
                                downloads = try values.map { file in
                                    guard file.size <= importLimits.maximumEntryBytes else { throw ModpackImportError.invalidManifest("模组文件过大：\(file.path)") }
                                    return ModpackDownload(path: file.path, urls: file.urls, sha1: file.sha1, sha512: nil, md5: file.md5, size: file.size)
                                }
                            } else { downloads = [] }
                            try Self.validateDownloads(downloads, profileID: profileID, limits: importLimits)
                            monitor.complete("resolve")
                            monitor.begin("modFiles")
                            let cache = try Self.downloadCachePath(relative: "PCL/download-cache/modpack-files", under: root)
                            try fm.createDirectory(at: cache, withIntermediateDirectories: true)
                            let reuse = ModpackFileReuse(roots: try Self.reuseRoots(profile: stagedProfile, game: root, cache: cache), maximumFileBytes: importLimits.maximumEntryBytes)
                            let result = try await Self.downloadFiles(downloads, into: stagedProfile, reuse: reuse, cache: cache,
                                session: downloadSession, zip: zip, payloads: payloads, monitor: monitor)
                            monitor.complete("modFiles")
                            return .downloads(downloads.count, result.reused, result.downloaded)
                        } catch {
                            let stage = monitor.snapshot.stages.first { $0.id == "resolve" }?.state == .completed ? "modFiles" : "resolve"
                            monitor.fail(stage, message: error.localizedDescription); throw error
                        }
                    }
                    group.addTask {
                        let source = try await Self.installBase(plan: plan, zip: zip, root: root, staging: staging,
                            javaExecutable: javaExecutable, installer: vanillaInstaller, loaderInstaller: modLoaderInstaller,
                            session: downloadSession, monitor: monitor)
                        return .base(source)
                    }
                    do {
                        for try await result in group {
                            switch result {
                            case .bundle(let files, let mods): bundledFiles = files; bundledMods = mods
                            case .downloads(let count, let reused, let downloaded): resolvedFiles = count; downloadResult = (reused, downloaded)
                            case .base(let id): sourceProfileID = id
                            }
                        }
                    } catch {
                        group.cancelAll()
                        // The task-group scope joins every branch, including the cancellable
                        // detached extractor, before the staging directory can be removed.
                        throw error
                    }
                }
            }
            monitor.begin("final")
            // Each branch wrote to its own directory. Bundled overrides win only
            // after all three branches have joined, without decompressing twice.
            try Self.overlay(from: bundleRoot, to: stagedProfile)
        } catch {
            monitor.cancel()
            if Task.isCancelled { throw CancellationError() }
            throw error
        }
        do {
            try Task.checkCancellation()
            var metadata = try MinecraftVersionMetadata.resolve(versionID: sourceProfileID, gameDirectory: staging)
            let jarID = metadata["jar"] as? String ?? sourceProfileID
            try MinecraftInstaller.validateProfileID(jarID)
            let clientJar = try MinecraftInstaller.safeDestination(root: staging, relative: "versions/\(jarID)/\(jarID).jar")
            guard fm.fileExists(atPath: clientJar.path) else { throw ModpackImportError.unavailableFile("\(jarID).jar") }
            let targetJar = stagedProfile.appendingPathComponent("\(profileID).jar")
            if clientJar.standardizedFileURL != targetJar.standardizedFileURL {
                if fm.fileExists(atPath: targetJar.path) { try fm.removeItem(at: targetJar) }
                try Self.cloneOrCopy(clientJar, to: targetJar)
            }
            metadata["id"] = profileID
            metadata["jar"] = profileID
            metadata["inheritsFrom"] = nil
            metadata["pclOriginalVersion"] = plan.descriptor.minecraftVersion
            if let kind = plan.descriptor.loaderKind, let version = plan.descriptor.loaderVersion {
                metadata["pclLoaderKind"] = kind.rawValue
                metadata["pclLoaderVersion"] = version
            }
            let data = try JSONSerialization.data(withJSONObject: metadata, options: [.sortedKeys, .prettyPrinted])
            guard !String(decoding: data, as: UTF8.self).contains(staging.path) else { throw ModpackImportError.invalidManifest("启动配置引用了临时安装目录") }
            try data.write(to: stagedProfile.appendingPathComponent("\(profileID).json"), options: .atomic)
            var settings = VersionProfileSettings()
            settings.isolateGameDirectory = true
            settings.category = plan.descriptor.loaderKind == nil ? "auto" : "modded"
            settings.displayDescription = plan.descriptor.summary.isEmpty ? plan.descriptor.name : plan.descriptor.summary
            if let memory = plan.instanceSettings["MaxMemAlloc"].flatMap(Int.init), (256...1_048_576).contains(memory) {
                settings.useGlobalLaunchSettings = false; settings.memoryMB = memory
            }
            if let args = plan.instanceSettings["JvmArgs"], !args.isEmpty {
                _ = try MinecraftArguments.tokenize(args)
                settings.useGlobalLaunchSettings = false; settings.extraJVMArguments = args
            }
            if !settings.useGlobalLaunchSettings { settings.javaPath = javaExecutable.path }
            let installed = InstalledVersion(id: profileID, type: metadata["type"] as? String ?? "custom",
                                             requiredJava: MinecraftVersionMetadata.requiredJava(metadata), directory: stagedProfile)
            try VersionSettingsRepository.save(settings, for: installed)
            let importReport: [String: Any] = ["format": plan.descriptor.format.rawValue,
                "sourceArchive": archive.lastPathComponent, "bundledFiles": bundledFiles,
                "bundledModJars": bundledMods,
                "resolvedFiles": resolvedFiles, "reusedFiles": downloadResult.reused, "downloadedFiles": downloadResult.downloaded]
            let reportFile = try MinecraftInstaller.safeDestination(root: stagedProfile, relative: "PCL/import-report.json")
            try JSONSerialization.data(withJSONObject: importReport, options: [.prettyPrinted, .sortedKeys]).write(to: reportFile, options: .atomic)
            monitor.update("final", fraction: 0.5, message: "合并已校验的共享游戏资源")
            try Self.mergeShared(staging: staging, root: root)
            try Task.checkCancellation()
            guard !fm.fileExists(atPath: target.path) else { throw ModpackImportError.destinationExists }
            try fm.moveItem(at: stagedProfile, to: target)
            monitor.complete("final", message: "\(profileID) 导入完成")
        } catch {
            if !Task.isCancelled { monitor.fail("final", message: error.localizedDescription) }
            monitor.cancel()
            if Task.isCancelled { throw CancellationError() }
            throw error
        }
    }

    private struct Payload: Sendable { let entry: ModpackZIPArchive.Entry; let path: String }
    private enum ImportBranchResult: Sendable { case bundle(Int, Int), downloads(Int, Int, Int), base(String) }

    private static func detachedWork<T: Sendable>(_ operation: @escaping @Sendable () throws -> T) async throws -> T {
        let worker = Task.detached(operation: operation)
        return try await withTaskCancellationHandler { try await worker.value } onCancel: { worker.cancel() }
    }

    private static func importStages(plan: ModpackPlan, zip: ModpackZIPArchive) -> [InstallStageProgress] {
        let external = plan.descriptor.format == .curseForge || plan.descriptor.format == .modrinth
        let loader = plan.descriptor.loaderKind?.displayName ?? "加载器"
        let fileCount: Int
        if plan.descriptor.format == .curseForge { fileCount = (try? curseForgeReferences(plan.manifest).count) ?? 0 }
        else if plan.descriptor.format == .modrinth { fileCount = (plan.manifest["files"] as? [[String: Any]])?.count ?? 0 }
        else { fileCount = 0 }
        let count = Double(max(1, fileCount))
        let archiveBytes = (try? zip.url.resourceValues(forKeys: [.fileSizeKey]).fileSize) ?? 0
        return [
            .init(id: "extract", title: "解压整合包文件", weight: max(1, Double(archiveBytes) / 1024 / 1024 / 6)),
            .init(id: "resolve", title: "获取 Mod 下载信息", weight: external ? max(1, count / 10) : 0, isVisible: external),
            .init(id: "modFiles", title: "下载 Mod", weight: external ? count * 1.5 : 0, isVisible: external),
            .init(id: "vanillaJson", title: "下载原版 json 文件", weight: 2),
            .init(id: "vanillaLibraries", title: "下载原版支持库文件", weight: 14),
            .init(id: "vanillaAssets", title: "下载原版资源文件", weight: 18),
            .init(id: "loaderMetadata", title: "获取 \(loader) 主文件下载地址", weight: plan.descriptor.loaderKind == nil ? 0 : 1, isVisible: plan.descriptor.loaderKind != nil),
            .init(id: "loaderFiles", title: "下载 \(loader) 主文件", weight: plan.descriptor.loaderKind == nil ? 0 : (plan.descriptor.loaderKind == .fabric || plan.descriptor.loaderKind == .quilt ? 2.5 : 25), isVisible: plan.descriptor.loaderKind != nil),
            .init(id: "install", title: "安装游戏", weight: 2),
            .init(id: "gameLibraries", title: "下载游戏支持库文件", weight: 8),
            .init(id: "final", title: "完成导入", weight: 0.1, isVisible: false)
        ]
    }

    private static func payloads(in zip: ModpackZIPArchive, plan: ModpackPlan, targetID: String) throws -> [Payload] {
        var effective: [String: Payload] = [:]
        func add(_ entry: ModpackZIPArchive.Entry, _ relative: String) throws {
            let path = try ModpackZIPArchive.validatedPath(relative)
            guard path != targetID + ".json", path != targetID + ".jar", path != "PCL/macos-settings.json" else {
                throw ModpackImportError.invalidManifest("覆盖文件试图替换启动器生成的配置：\(path)")
            }
            guard !["launcher_accounts.json", "launcher_profiles.json", "accounts.json", "launcher_msa_credentials.bin"].contains(path.lowercased()) else { return }
            effective[ModpackZIPArchive.pathKey(path)] = Payload(entry: entry, path: path)
        }
        if let base = plan.completeRootPrefix, let profile = plan.completeProfileID {
            for entry in zip.entries where !entry.isDirectory && entry.path.hasPrefix(base) {
                let relative = String(entry.path.dropFirst(base.count))
                if isRuntimePayload(relative) { try add(entry, relative) }
            }
            let prefix = base + "versions/\(profile)/"
            for entry in zip.entries where !entry.isDirectory && entry.path.hasPrefix(prefix) {
                let relative = String(entry.path.dropFirst(prefix.count))
                if !["\(profile).json", "\(profile).jar", "PCL/macos-settings.json"].contains(relative) { try add(entry, relative) }
            }
        }
        for prefix in plan.overridePrefixes {
            for entry in zip.entries where !entry.isDirectory && entry.path.hasPrefix(prefix) {
                try add(entry, String(entry.path.dropFirst(prefix.count)))
            }
        }
        return effective.values.sorted { $0.path < $1.path }
    }

    private static func validateDownloads(_ files: [ModpackDownload], profileID: String, limits: ModpackImportLimits) throws {
        guard files.reduce(Int64(0), { $0 + $1.size }) <= limits.maximumExpandedBytes else { throw ModpackImportError.invalidManifest("整合包下载总大小超出限制") }
        var paths = Set<String>()
        for file in files {
            guard paths.insert(ModpackZIPArchive.pathKey(file.path)).inserted else { throw ModpackImportError.invalidManifest("下载文件路径冲突：\(file.path)") }
            guard ![profileID + ".json", profileID + ".jar", "PCL/macos-settings.json"].contains(file.path) else {
                throw ModpackImportError.invalidManifest("下载文件试图替换启动配置：\(file.path)")
            }
        }
    }

    private static func installBase(plan: ModpackPlan, zip: ModpackZIPArchive, root: URL, staging: URL,
        javaExecutable: URL, installer: MinecraftInstaller, loaderInstaller: ModLoaderInstaller,
        session: URLSession, monitor: InstallTaskMonitor) async throws -> String {
        do {
            try await detachedWork {
                for directory in ["assets", "libraries"] {
                    try seedCache(from: root.appendingPathComponent(directory), to: staging.appendingPathComponent(directory))
                }
                try seedBaseProfile(versionID: plan.descriptor.minecraftVersion, root: root, staging: staging)
                if let base = plan.completeRootPrefix {
                    for entry in zip.entries where !entry.isDirectory {
                        for folder in ["assets/", "libraries/"] where entry.path.hasPrefix(base + folder) {
                            try Task.checkCancellation()
                            try zip.write(entry, to: MinecraftInstaller.safeDestination(root: staging, relative: String(entry.path.dropFirst(base.count))))
                        }
                    }
                }
            }
            monitor.begin("vanillaJson")
            let manifest = try await installer.fetchManifest()
            guard let minecraft = manifest.versions.first(where: { $0.id == plan.descriptor.minecraftVersion }) else {
                throw ModpackImportError.unsupported("官方版本列表没有 Minecraft \(plan.descriptor.minecraftVersion)")
            }
            let buildID = "pcl-import-build-\(UUID().uuidString)"
            if let kind = plan.descriptor.loaderKind, let version = plan.descriptor.loaderVersion {
                try await loaderInstaller.install(version: ModLoaderVersion(kind: kind, minecraftVersion: minecraft.id, version: version),
                    minecraftVersion: minecraft, gameDirectory: staging, profileID: buildID, javaExecutable: javaExecutable, progress: { _ in })
            } else {
                let cachedJar = staging.appendingPathComponent("versions/\(minecraft.id)/\(minecraft.id).jar")
                if FileManager.default.fileExists(atPath: cachedJar.path) {
                    let target = staging.appendingPathComponent("versions/\(buildID)/\(buildID).jar")
                    try FileManager.default.createDirectory(at: target.deletingLastPathComponent(), withIntermediateDirectories: true)
                    try cloneOrCopy(cachedJar, to: target)
                }
                try await installer.install(version: minecraft, gameDirectory: staging, profileID: buildID, progress: { _ in })
                monitor.complete("loaderMetadata"); monitor.complete("loaderFiles")
                monitor.begin("install"); monitor.complete("install")
            }
            var source = buildID
            if let base = plan.completeRootPrefix, let profile = plan.completeProfileID {
                try await detachedWork {
                    for entry in zip.entries where !entry.isDirectory && entry.path.hasPrefix(base + "versions/") {
                        try Task.checkCancellation()
                        try zip.write(entry, to: MinecraftInstaller.safeDestination(root: staging, relative: String(entry.path.dropFirst(base.count))))
                    }
                }
                source = profile
            }
            let metadata = try MinecraftVersionMetadata.resolve(versionID: source, gameDirectory: staging)
            monitor.begin("gameLibraries")
            try await InstallProgressContext.$stageID.withValue("gameLibraries") {
                try await ensureMetadataLibraries(metadata, in: staging, session: session, monitor: monitor)
            }
            monitor.complete("gameLibraries")
            return source
        } catch {
            let failed = monitor.snapshot.stages.first { $0.state == .running && !["extract", "resolve", "modFiles"].contains($0.id) }?.id ?? "vanillaJson"
            monitor.fail(failed, message: error.localizedDescription)
            throw error
        }
    }

    private static func overlay(from source: URL, to target: URL) throws {
        let fm = FileManager.default
        guard let iterator = fm.enumerator(at: source, includingPropertiesForKeys: [.isRegularFileKey]) else { return }
        for case let file as URL in iterator {
            try Task.checkCancellation()
            guard try file.resourceValues(forKeys: [.isRegularFileKey]).isRegularFile == true else { continue }
            let destination = try MinecraftInstaller.safeDestination(root: target, relative: String(file.path.dropFirst(source.path.count + 1)))
            try fm.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
            if fm.fileExists(atPath: destination.path) { try fm.removeItem(at: destination) }
            try cloneOrCopy(file, to: destination)
        }
    }

    private static func plan(_ zip: ModpackZIPArchive) throws -> ModpackPlan {
        // A copied manifest in a config/override/version directory is game data,
        // not the archive's export format. Accept metadata at the ZIP root or at
        // the common enclosing directory of all its non-Finder payload files.
        let files = zip.entries.filter { entry in
            guard !entry.isDirectory else { return false }
            let parts = entry.path.split(separator: "/")
            return parts.first != "__MACOSX" && parts.last != ".DS_Store"
        }
        var wrapper = files.first.map { Array($0.path.split(separator: "/").dropLast()) } ?? []
        for file in files.dropFirst() {
            let parent = file.path.split(separator: "/").dropLast()
            while !parent.starts(with: wrapper) { wrapper.removeLast() }
        }
        let runtimeDirectories: Set<String> = ["overrides", "client-overrides", "server-overrides", "config",
                                                "versions", "mods", "resourcepacks", "shaderpacks", "saves",
                                                "libraries", "assets", "logs", "screenshots", ".minecraft"]
        let wrapperPrefix = !wrapper.isEmpty && !wrapper.contains(where: { runtimeDirectories.contains($0.lowercased()) })
            ? wrapper.joined(separator: "/") + "/" : nil
        let markers: [(String, ModpackFormat)] = [("modrinth.index.json", .modrinth), ("mcbbs.packmeta", .mcbbs), ("mmc-pack.json", .multiMC), ("manifest.json", .curseForge)]
        for (name, format) in markers {
            var markerPaths = Set([name])
            if let wrapperPrefix { markerPaths.insert(wrapperPrefix + name) }
            let matches = files.filter { markerPaths.contains($0.path) }
            guard matches.count <= 1 else { throw ModpackImportError.invalidManifest("压缩包中包含多个 \(name)") }
            guard let entry = matches.first else { continue }
            let prefix = String(entry.path.dropLast(name.count))
            let json = try object(zip.data(for: entry), name: name)
            var mc = "", loader: (ModLoaderKind, String)?, settings: [String: String] = [:], warnings: [String] = []
            var overrides: [String] = []
            let suggested = (json["name"] as? String).flatMap { $0.isEmpty ? nil : $0 } ?? zip.url.deletingPathExtension().lastPathComponent
            var title = suggested
            switch format {
            case .modrinth:
                guard json["formatVersion"] as? Int == 1, json["game"] as? String == "minecraft",
                      let dependencies = json["dependencies"] as? [String: String], let game = dependencies["minecraft"] else {
                    throw ModpackImportError.invalidManifest("不支持的 Modrinth 格式或缺少 Minecraft 版本")
                }
                mc = game
                let names: [String: ModLoaderKind] = ["forge": .forge, "neoforge": .neoForge, "fabric-loader": .fabric, "quilt-loader": .quilt]
                for (key, value) in dependencies where key != "minecraft" {
                    guard let kind = names[key] else { throw ModpackImportError.unsupported("未知的必需组件 \(key)") }
                    guard loader == nil else { throw ModpackImportError.invalidManifest("同时声明多个模组加载器") }
                    loader = (kind, value)
                }
                _ = try modrinthDownloads(json, limits: zip.limits)
                if (json["files"] as? [[String: Any]])?.contains(where: { ($0["env"] as? [String: String])?["client"] == "optional" }) == true {
                    warnings.append("将一并安装客户端可选文件。")
                }
                overrides = [prefix + "overrides/", prefix + "client-overrides/"]
            case .curseForge:
                guard json["manifestType"] as? String == "minecraftModpack", json["manifestVersion"] as? Int == 1,
                      let minecraft = json["minecraft"] as? [String: Any], let version = minecraft["version"] as? String else {
                    continue // Another application's unrelated manifest.json may be in a complete pack.
                }
                mc = version
                if let memory = minecraft["recommendedRam"] as? Int, (256...65_536).contains(memory) {
                    settings["MaxMemAlloc"] = String(memory)
                }
                let loaders = minecraft["modLoaders"] as? [[String: Any]] ?? []
                if let selected = loaders.first(where: { $0["primary"] as? Bool == true }) ?? loaders.first {
                    guard let id = selected["id"] as? String else { throw ModpackImportError.invalidManifest("加载器 ID 为空") }
                    loader = try parseLoaderID(id, minecraft: mc)
                }
                let references = try curseForgeReferences(json)
                if !references.isEmpty { warnings.append("将自动解析 \(references.count) 项必需文件，复用已包含且校验一致的文件，并补全缺少的内容。") }
                let override = json["overrides"] as? String ?? "overrides"
                overrides = [prefix + (try ModpackZIPArchive.validatedPath(override)) + "/"]
            case .multiMC:
                guard json["formatVersion"] as? Int == 1, let components = json["components"] as? [[String: Any]] else {
                    throw ModpackImportError.invalidManifest("MultiMC 组件列表无效")
                }
                if let config = zip.member(prefix + "instance.cfg"), let text = String(data: try zip.data(for: config), encoding: .utf8) {
                    settings = parseINI(text)
                    if let name = settings["name"], !name.isEmpty { title = name }
                }
                if settings["OverrideCommands"]?.lowercased() == "true",
                   ["PreLaunchCommand", "WrapperCommand", "PostExitCommand"].contains(where: { !(settings[$0] ?? "").isEmpty }) {
                    throw ModpackImportError.unsupported("此实例需要外部启动命令，请先转换为自包含的 Modrinth 或完整版本包")
                }
                let supported: [String: ModLoaderKind] = ["net.minecraftforge": .forge, "net.neoforged": .neoForge,
                                                         "net.fabricmc.fabric-loader": .fabric, "org.quiltmc.quilt-loader": .quilt]
                for component in components where component["disabled"] as? Bool != true {
                    guard let uid = component["uid"] as? String, let version = component["version"] as? String ?? component["cachedVersion"] as? String else {
                        throw ModpackImportError.invalidManifest("组件缺少 uid 或 version")
                    }
                    if uid == "net.minecraft" { mc = version }
                    else if let kind = supported[uid] {
                        guard loader == nil else { throw ModpackImportError.invalidManifest("同时声明多个加载器") }
                        loader = (kind, version)
                    } else if !["org.lwjgl", "org.lwjgl3", "org.multimc.java"].contains(uid) {
                        throw ModpackImportError.unsupported("MultiMC 自定义组件 \(uid)")
                    }
                }
                if zip.entries.contains(where: { !$0.isDirectory && $0.path.hasPrefix(prefix + "patches/") }) {
                    throw ModpackImportError.unsupported("此 MultiMC 实例包含自定义版本补丁，请使用完整版本 ZIP 导出")
                }
                let folder = zip.entries.contains { $0.path.hasPrefix(prefix + "minecraft/") } ? "minecraft/" : ".minecraft/"
                overrides = [prefix + folder]
            case .mcbbs:
                guard let addons = json["addons"] as? [[String: Any]] else { throw ModpackImportError.invalidManifest("MCBBS addons 为空") }
                let supported: [String: ModLoaderKind] = ["forge": .forge, "neoforge": .neoForge, "fabric": .fabric, "quilt": .quilt]
                for addon in addons {
                    guard let id = addon["id"] as? String, let version = addon["version"] as? String else { throw ModpackImportError.invalidManifest("MCBBS 组件信息无效") }
                    if id == "game" { mc = version }
                    else if let kind = supported[id] {
                        guard loader == nil else { throw ModpackImportError.invalidManifest("同时声明多个加载器") }
                        loader = (kind, version)
                    } else { throw ModpackImportError.unsupported("MCBBS 组件 \(id)") }
                }
                overrides = [prefix + "overrides/"]
            case .completeVersion: break
            }
            guard !mc.isEmpty else { throw ModpackImportError.invalidManifest("缺少 Minecraft 版本") }
            try MinecraftInstaller.validateComponent(mc)
            if let loader { try MinecraftInstaller.validateComponent(loader.1) }
            return ModpackPlan(descriptor: ModpackDescriptor(name: title, format: format, minecraftVersion: mc,
                                loaderKind: loader?.0, loaderVersion: loader?.1, fileCount: zip.entries.filter { !$0.isDirectory }.count,
                                summary: json["summary"] as? String ?? json["description"] as? String ?? "", warnings: warnings, needsAPIKey: false),
                               prefix: prefix, manifest: json, overridePrefixes: overrides, completeProfileID: nil,
                               completeRootPrefix: nil, instanceSettings: settings)
        }
        return try completePlan(zip)
    }

    private static func completePlan(_ zip: ModpackZIPArchive) throws -> ModpackPlan {
        var profiles: [(entry: ModpackZIPArchive.Entry, id: String, root: String, json: [String: Any])] = []
        for entry in zip.entries where !entry.isDirectory && entry.path.hasSuffix(".json") {
            let parts = entry.path.split(separator: "/").map(String.init)
            guard parts.count >= 3, parts[parts.count - 3] == "versions", parts.last == parts[parts.count - 2] + ".json" else { continue }
            let json = try object(zip.data(for: entry), name: entry.path)
            guard json["mainClass"] != nil || json["inheritsFrom"] != nil else { continue }
            let id = parts[parts.count - 2]
            try MinecraftInstaller.validateProfileID(id)
            guard json["id"] as? String == id else { throw ModpackImportError.invalidManifest("版本文件夹与配置 ID 不一致") }
            let root = parts.dropLast(3).joined(separator: "/")
            profiles.append((entry, id, root.isEmpty ? "" : root + "/", json))
        }
        guard !profiles.isEmpty else { throw ModpackImportError.unsupported("没有找到 Modrinth、CurseForge、MultiMC、MCBBS 或完整 versions 目录") }
        let parentIDs = Set(profiles.compactMap { $0.json["inheritsFrom"] as? String })
        let candidates = profiles.filter { !parentIDs.contains($0.id) }
        guard candidates.count == 1, let selected = candidates.first,
              profiles.allSatisfy({ $0.root == selected.root }) else {
            throw ModpackImportError.unsupported("压缩包包含多个独立版本，请单独导出需要导入的版本")
        }
        var current = selected, visited = Set<String>(), mc = selected.json["pclOriginalVersion"] as? String
        while let parent = current.json["inheritsFrom"] as? String {
            guard visited.insert(current.id).inserted else { throw ModpackImportError.invalidManifest("版本继承循环") }
            if let next = profiles.first(where: { $0.id == parent }) { current = next }
            else { mc = mc ?? parent; break }
        }
        mc = mc ?? current.json["pclOriginalVersion"] as? String ?? current.json["id"] as? String
        guard let mc else { throw ModpackImportError.invalidManifest("无法识别基础游戏版本") }
        try MinecraftInstaller.validateComponent(mc)
        var loader: (ModLoaderKind, String)?
        if let marker = selected.json["pclLoaderKind"] as? String, let kind = ModLoaderKind(rawValue: marker), let version = selected.json["pclLoaderVersion"] as? String {
            loader = (kind, version)
        } else {
            for library in selected.json["libraries"] as? [[String: Any]] ?? [] {
                let name = (library["name"] as? String ?? "").split(separator: ":").map(String.init)
                guard name.count >= 3 else { continue }
                if name[0] == "net.fabricmc", name[1] == "fabric-loader" { loader = (.fabric, name[2]) }
                else if name[0] == "org.quiltmc", name[1] == "quilt-loader" { loader = (.quilt, name[2]) }
                else if name[0] == "net.neoforged", name[1] == "neoforge" { loader = (.neoForge, name[2]) }
                else if name[0] == "net.minecraftforge", name[1] == "forge" {
                    loader = (.forge, name[2].hasPrefix(mc + "-") ? String(name[2].dropFirst(mc.count + 1)) : name[2])
                }
            }
        }
        return ModpackPlan(descriptor: ModpackDescriptor(name: selected.id, format: .completeVersion, minecraftVersion: mc,
                            loaderKind: loader?.0, loaderVersion: loader?.1, fileCount: zip.entries.filter { !$0.isDirectory }.count,
                            summary: "完整版本导入", warnings: [], needsAPIKey: false), prefix: selected.root, manifest: selected.json,
                           overridePrefixes: [], completeProfileID: selected.id, completeRootPrefix: selected.root, instanceSettings: [:])
    }

    private static func object(_ data: Data, name: String) throws -> [String: Any] {
        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else { throw ModpackImportError.invalidManifest(name) }
        return json
    }
    private static func parseLoaderID(_ id: String, minecraft: String) throws -> (ModLoaderKind, String) {
        for (prefix, kind) in [("neoforge-", ModLoaderKind.neoForge), ("forge-", .forge), ("fabric-", .fabric), ("quilt-", .quilt)] where id.lowercased().hasPrefix(prefix) {
            var version = String(id.dropFirst(prefix.count))
            if version.hasPrefix(minecraft + "-") { version = String(version.dropFirst(minecraft.count + 1)) }
            guard !version.isEmpty else { throw ModpackImportError.invalidManifest("加载器版本为空") }
            return (kind, version)
        }
        throw ModpackImportError.unsupported("CurseForge 加载器 \(id)")
    }
    private static func parseINI(_ text: String) -> [String: String] {
        var result: [String: String] = [:]
        for raw in text.split(whereSeparator: \.isNewline) {
            let line = raw.trimmingCharacters(in: .whitespaces)
            guard !line.hasPrefix("#"), let equal = line.firstIndex(of: "=") else { continue }
            result[String(line[..<equal])] = String(line[line.index(after: equal)...]).replacingOccurrences(of: "\\\"", with: "\"")
        }
        return result
    }

    private static func modrinthDownloads(_ manifest: [String: Any], limits: ModpackImportLimits) throws -> [ModpackDownload] {
        guard let files = manifest["files"] as? [[String: Any]], files.count <= limits.maximumEntries else {
            throw ModpackImportError.invalidManifest("Modrinth files 列表无效")
        }
        var result: [ModpackDownload] = [], paths = Set<String>(), total: Int64 = 0
        for file in files {
            if file["env"] != nil {
                guard let env = file["env"] as? [String: String], Set(env.keys).isSubset(of: ["client", "server"]) else {
                    throw ModpackImportError.invalidManifest("客户端/服务器环境信息无效")
                }
                guard env.values.allSatisfy({ ["required", "optional", "unsupported"].contains($0) }) else { throw ModpackImportError.invalidManifest("未知的客户端/服务器环境标记") }
                if env["client"] == "unsupported" { continue }
            }
            guard let path = file["path"] as? String, let hashes = file["hashes"] as? [String: String],
                  let sha1 = hashes["sha1"], isHex(sha1, count: 40),
                  let sha512 = hashes["sha512"], isHex(sha512, count: 128),
                  let rawURLs = file["downloads"] as? [String], !rawURLs.isEmpty,
                  let size = (file["fileSize"] as? NSNumber)?.int64Value, size >= 0, size <= limits.maximumEntryBytes else {
                throw ModpackImportError.invalidManifest("Modrinth 文件缺少下载地址、大小或 SHA-1/SHA-512")
            }
            let relative = try ModpackZIPArchive.validatedPath(path)
            guard paths.insert(ModpackZIPArchive.pathKey(relative)).inserted else { throw ModpackImportError.invalidManifest("重复下载路径：\(relative)") }
            total += size
            guard total <= limits.maximumExpandedBytes else { throw ModpackImportError.invalidManifest("下载文件总大小超出限制") }
            let urls = try rawURLs.flatMap { value -> [URL] in
                guard let url = URL(string: value), allowedDownload(url, hosts: ["cdn.modrinth.com", "github.com", "raw.githubusercontent.com", "gitlab.com"]) else {
                    throw ModpackImportError.invalidManifest("不支持的 Modrinth 文件下载域名")
                }
                // The original PCL CDN mirror is also needed for files inside
                // an online .mrpack, after its archive has been downloaded.
                if url.host?.lowercased() == "cdn.modrinth.com" {
                    return try CommunityResourceClient.modrinthDownloadURLs(url)
                }
                return [url]
            }
            result.append(ModpackDownload(path: relative, urls: urls, sha1: sha1.lowercased(), sha512: sha512.lowercased(), md5: nil, size: size))
        }
        return result
    }

    private static func curseForgeReferences(_ manifest: [String: Any]) throws -> [(Int, Int)] {
        guard let files = manifest["files"] as? [[String: Any]] else { throw ModpackImportError.invalidManifest("CurseForge files 列表无效") }
        var result: [(Int, Int)] = [], seen = Set<Int>()
        for file in files where file["required"] as? Bool != false {
            guard let project = file["projectID"] as? Int, let id = file["fileID"] as? Int, project > 0, id > 0,
                  seen.insert(id).inserted else { throw ModpackImportError.invalidManifest("CurseForge 模组文件 ID 无效或重复") }
            result.append((project, id))
        }
        return result
    }

    private static func reuseRoots(profile: URL, game: URL, cache: URL) throws -> [URL] {
        let types = ["mods", "resourcepacks", "shaderpacks", "saves"]
        var roots = types.map { profile.appendingPathComponent($0) } + [cache]
        roots += try types.map { try MinecraftInstaller.safeDestination(root: game, relative: $0) }
        let versions = try MinecraftInstaller.safeDestination(root: game, relative: "versions")
        if let children = try? FileManager.default.contentsOfDirectory(at: versions, includingPropertiesForKeys: [.isDirectoryKey, .isSymbolicLinkKey], options: [.skipsHiddenFiles]) {
            for directory in children {
                let values = try directory.resourceValues(forKeys: [.isDirectoryKey, .isSymbolicLinkKey])
                guard values.isDirectory == true, values.isSymbolicLink != true else { continue }
                for type in types {
                    roots.append(try MinecraftInstaller.safeDestination(root: game, relative: "versions/\(directory.lastPathComponent)/\(type)"))
                }
            }
        }
        return roots
    }

    /// Cache replacement must never follow even an in-root link into an existing
    /// version. Inspect lexical components before safeDestination canonicalizes.
    private static func downloadCachePath(relative: String, under root: URL) throws -> URL {
        var component = URL(fileURLWithPath: "/", isDirectory: true)
        for part in root.pathComponents.dropFirst() + relative.split(separator: "/").map(String.init) {
            component.appendPathComponent(part)
            if (try? FileManager.default.destinationOfSymbolicLink(atPath: component.path)) != nil {
                throw ModpackImportError.unsafePath("下载缓存不能使用符号链接：\(part)")
            }
        }
        return try MinecraftInstaller.safeDestination(root: root, relative: relative)
    }

    private static func downloadFiles(_ files: [ModpackDownload], into root: URL, reuse: ModpackFileReuse, cache: URL,
        session: URLSession, zip: ModpackZIPArchive, payloads: [Payload], monitor: InstallTaskMonitor) async throws -> (reused: Int, downloaded: Int) {
        monitor.setRemainingFiles(files.count, for: "modFiles")
        return try await InstallProgressContext.$stageID.withValue("modFiles") {
            try await withThrowingTaskGroup(of: Bool.self) { group in
                var iterator = files.makeIterator(), completed = 0, reusedCount = 0
                func add(_ file: ModpackDownload) {
                    group.addTask {
                        // Read immutable archive bytes directly: the bulk extractor may
                        // still be busy with another file, or may not have started this Mod.
                        let directory = (file.path as NSString).deletingLastPathComponent
                        let canRename = ["mods", "resourcepacks", "shaderpacks", "saves"].contains(directory.lowercased())
                        let candidates = payloads.filter {
                            guard Int64($0.entry.size) == file.size else { return false }
                            if ModpackZIPArchive.pathKey($0.path) == ModpackZIPArchive.pathKey(file.path) { return true }
                            return canRename && ModpackZIPArchive.pathKey(($0.path as NSString).deletingLastPathComponent) == ModpackZIPArchive.pathKey(directory)
                                && ($0.path as NSString).pathExtension.lowercased() == (file.path as NSString).pathExtension.lowercased()
                        }
                        for candidate in candidates {
                            try Task.checkCancellation()
                            let bytes = try zip.data(for: candidate.entry, maximumBytes: candidate.entry.size)
                            if (file.sha1 == nil || digest(Insecure.SHA1.hash(data: bytes)) == file.sha1?.lowercased()),
                               (file.sha512 == nil || digest(SHA512.hash(data: bytes)) == file.sha512?.lowercased()),
                               (file.md5 == nil || digest(Insecure.MD5.hash(data: bytes)) == file.md5?.lowercased()) { return true }
                        }
                        return try await download(file, into: root, reuse: reuse, cache: cache, session: session)
                    }
                }
                for _ in 0..<min(6, files.count) { if let file = iterator.next() { add(file) } }
                while let reused = try await group.next() {
                    try Task.checkCancellation()
                    completed += 1; if reused { reusedCount += 1 }
                    monitor.setRemainingFiles(files.count - completed, for: "modFiles")
                    monitor.update("modFiles", fraction: Double(completed) / Double(max(1, files.count)))
                    if let file = iterator.next() { add(file) }
                }
                return (reusedCount, completed - reusedCount)
            }
        }
    }

    private static func download(_ file: ModpackDownload, into root: URL, reuse: ModpackFileReuse, cache: URL,
                                 session: URLSession) async throws -> Bool {
        let target = try MinecraftInstaller.safeDestination(root: root, relative: file.path)
        let identity = ModpackFileIdentity(size: file.size, sha1: file.sha1, sha512: file.sha512, md5: file.md5)
        let hash = file.sha1 ?? file.sha512 ?? file.md5!
        let cached = try downloadCachePath(relative: "\(hash).\(target.pathExtension.isEmpty ? "bin" : target.pathExtension)", under: cache)
        if let source = try await reuse.matchingFile(for: identity), source.deletingLastPathComponent() == target.deletingLastPathComponent() {
            // A renamed, bundled Mod already in this exact runtime directory must
            // not be copied again under its canonical CDN name (duplicate Mod ID).
            return true
        }
        if try await reuse.reuse(identity, to: target) { return true }
        var lastError: Error = ModpackImportError.unavailableFile(file.path)
        for url in file.urls {
            for attempt in 0..<2 {
                do {
                    try Task.checkCancellation()
                    var request = URLRequest(url: url, cachePolicy: .reloadIgnoringLocalCacheData, timeoutInterval: 90)
                    request.setValue(CurseForgeResolver.userAgent, forHTTPHeaderField: "User-Agent")
                    let (temporary, response) = try await PCLHTTPS.download(for: request, session: session)
                    defer { try? FileManager.default.removeItem(at: temporary) }
                    guard let http = response as? HTTPURLResponse, (200..<300).contains(http.statusCode), response.url?.scheme == "https" else {
                        throw ModpackImportError.unavailableFile(file.path)
                    }
                    guard try ModpackFileReuse.matches(temporary, identity: identity, maximumFileBytes: max(file.size, 1)) else {
                        throw ModpackImportError.integrity(file.path)
                    }
                    try Task.checkCancellation()
                    try FileManager.default.createDirectory(at: target.deletingLastPathComponent(), withIntermediateDirectories: true)
                    let staged = target.deletingLastPathComponent().appendingPathComponent(".pcl-download-\(UUID().uuidString)")
                    defer { try? FileManager.default.removeItem(at: staged) }
                    try FileManager.default.copyItem(at: temporary, to: staged)
                    try MinecraftInstaller.commit(staged, to: target)
                    if !(try ModpackFileReuse.matches(cached, identity: identity, maximumFileBytes: max(file.size, 1))) {
                        let cacheStage = cache.appendingPathComponent(".pcl-cache-\(UUID().uuidString)")
                        defer { try? FileManager.default.removeItem(at: cacheStage) }
                        try cloneOrCopy(temporary, to: cacheStage)
                        try MinecraftInstaller.commit(cacheStage, to: cached)
                    }
                    try await reuse.refresh()
                    return false
                } catch {
                    try Task.checkCancellation()
                    if let failure = error as? URLError, failure.code == .cancelled { throw CancellationError() }
                    lastError = error
                    if attempt == 0 { try await Task.sleep(nanoseconds: 400_000_000) }
                }
            }
        }
        throw lastError
    }

    private static func ensureMetadataLibraries(_ metadata: [String: Any], in root: URL, session: URLSession, monitor: InstallTaskMonitor) async throws {
        let libraries = metadata["libraries"] as? [[String: Any]] ?? []
        let allowed = libraries.filter { InstallPlatform.libraryAllowed($0) }
        monitor.setRemainingFiles(allowed.count, for: "gameLibraries")
        for (index, library) in allowed.enumerated() {
            try Task.checkCancellation()
            defer {
                monitor.setRemainingFiles(allowed.count - index - 1, for: "gameLibraries")
                monitor.update("gameLibraries", fraction: Double(index + 1) / Double(max(1, allowed.count)))
            }
            let downloads = library["downloads"] as? [String: Any]
            if downloads == nil || downloads?["artifact"] != nil, let path = MinecraftVersionMetadata.libraryPath(library) {
                let target = try MinecraftInstaller.safeDestination(root: root, relative: "libraries/" + path)
                if let artifact = downloads?["artifact"] as? [String: Any], let hash = artifact["sha1"] as? String, MinecraftInstaller.validSHA1(hash) {
                    try await MinecraftInstaller.download(MinecraftInstaller.artifact(artifact, destination: target), session: session)
                } else if !FileManager.default.fileExists(atPath: target.path) {
                    throw ModpackImportError.unavailableFile("依赖库 \(path)，请先在来源启动器补全后重新导出")
                }
            }
            if let classifier = InstallPlatform.nativeClassifier(for: library),
               let classifiers = downloads?["classifiers"] as? [String: Any], let info = classifiers[classifier] as? [String: Any], let path = info["path"] as? String {
                let target = try MinecraftInstaller.safeDestination(root: root, relative: "libraries/" + path)
                try await MinecraftInstaller.download(MinecraftInstaller.artifact(info, destination: target), session: session)
            }
        }
    }

    private static func allowedDownload(_ url: URL, hosts: Set<String>) -> Bool {
        url.scheme == "https" && url.user == nil && url.password == nil && url.port == nil && hosts.contains(url.host?.lowercased() ?? "")
    }
    private static func isHex(_ text: String, count: Int) -> Bool {
        text.utf8.count == count && text.utf8.allSatisfy { (48...57).contains($0) || (65...70).contains($0) || (97...102).contains($0) }
    }
    private static func digest<D: Sequence>(_ digest: D) -> String where D.Element == UInt8 { digest.map { String(format: "%02x", $0) }.joined() }

    private static func writePayload(_ entry: ModpackZIPArchive.Entry, relative: String, zip: ModpackZIPArchive, profile: URL, targetID: String) throws {
        let path = try ModpackZIPArchive.validatedPath(relative)
        guard path != targetID + ".json", path != targetID + ".jar", path != "PCL/macos-settings.json" else {
            throw ModpackImportError.invalidManifest("覆盖文件试图替换启动器生成的配置：\(path)")
        }
        if ["launcher_accounts.json", "launcher_profiles.json", "accounts.json", "launcher_msa_credentials.bin"].contains(path.lowercased()) { return }
        try zip.write(entry, to: MinecraftInstaller.safeDestination(root: profile, relative: path))
    }
    private static func isRuntimePayload(_ path: String) -> Bool {
        let first = path.split(separator: "/").first.map(String.init)?.lowercased() ?? ""
        return !["assets", "libraries", "versions", "runtime", "java", "launcher_accounts.json", "launcher_profiles.json", "accounts.json", "launcher_msa_credentials.bin", "__macosx", ".ds_store"].contains(first)
    }

    private static func seedBaseProfile(versionID: String, root: URL, staging: URL) throws {
        let versions = try MinecraftInstaller.safeDestination(root: root, relative: "versions")
        let names = (try? FileManager.default.contentsOfDirectory(atPath: versions.path)) ?? []
        // Flattened imported profiles also contain an official client JAR. Seed
        // only that JAR; the installer still fetches and verifies official JSON
        // and its declared client size/SHA-1 before trusting the cached bytes.
        let candidates = [versionID] + names.filter { $0 != versionID }.sorted()
        for name in candidates {
            try Task.checkCancellation()
            let folder = try MinecraftInstaller.safeDestination(root: root, relative: "versions/\(name)")
            guard (try? FileManager.default.destinationOfSymbolicLink(atPath: folder.path)) == nil,
                  let data = try? Data(contentsOf: folder.appendingPathComponent("\(name).json")),
                  let metadata = try? object(data, name: name), metadata["inheritsFrom"] == nil,
                  (metadata["pclOriginalVersion"] as? String ?? metadata["id"] as? String) == versionID else { continue }
            let jarID = metadata["jar"] as? String ?? name
            guard (try? MinecraftInstaller.validateProfileID(jarID)) != nil else { continue }
            let source = try MinecraftInstaller.safeDestination(root: root, relative: "versions/\(jarID)/\(jarID).jar")
            guard FileManager.default.fileExists(atPath: source.path) else { continue }
            let target = try MinecraftInstaller.safeDestination(root: staging, relative: "versions/\(versionID)/\(versionID).jar")
            try FileManager.default.createDirectory(at: target.deletingLastPathComponent(), withIntermediateDirectories: true)
            try cloneOrCopy(source, to: target)
            return
        }
    }
    private static func seedCache(from source: URL, to destination: URL) throws {
        let fm = FileManager.default
        guard fm.fileExists(atPath: source.path) else { return }
        guard (try? fm.destinationOfSymbolicLink(atPath: source.path)) == nil else { return }
        guard let enumerator = fm.enumerator(at: source, includingPropertiesForKeys: [.isDirectoryKey, .isRegularFileKey, .isSymbolicLinkKey], options: [.skipsHiddenFiles]) else { return }
        for case let file as URL in enumerator {
            try Task.checkCancellation()
            let values = try file.resourceValues(forKeys: [.isDirectoryKey, .isRegularFileKey, .isSymbolicLinkKey])
            if values.isSymbolicLink == true { enumerator.skipDescendants(); continue }
            let relative = String(file.path.dropFirst(source.path.count + 1))
            let target = try MinecraftInstaller.safeDestination(root: destination, relative: relative)
            if values.isDirectory == true { try fm.createDirectory(at: target, withIntermediateDirectories: true) }
            else if values.isRegularFile == true {
                try fm.createDirectory(at: target.deletingLastPathComponent(), withIntermediateDirectories: true)
                try cloneOrCopy(file, to: target)
            }
        }
    }
    static func cloneOrCopy(_ source: URL, to destination: URL) throws {
        guard (try? FileManager.default.destinationOfSymbolicLink(atPath: source.path)) == nil else { throw ModpackImportError.unsafePath(source.lastPathComponent) }
        let result = source.path.withCString { from in destination.path.withCString { to in clonefile(from, to, 0) } }
        if result != 0 { try FileManager.default.copyItem(at: source, to: destination) }
    }

    private static func mergeShared(staging: URL, root: URL) throws {
        let fm = FileManager.default
        var newFiles: [(URL, String)] = []
        for directory in ["assets", "libraries"] {
            let source = staging.appendingPathComponent(directory)
            guard let enumerator = fm.enumerator(at: source, includingPropertiesForKeys: [.isRegularFileKey, .isSymbolicLinkKey]) else { continue }
            for case let file as URL in enumerator {
                try Task.checkCancellation()
                let values = try file.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey])
                guard values.isSymbolicLink != true else { throw ModpackImportError.unsafePath(file.lastPathComponent) }
                guard values.isRegularFile == true else { continue }
                let relative = directory + "/" + String(file.path.dropFirst(source.path.count + 1))
                let target = try MinecraftInstaller.safeDestination(root: root, relative: relative)
                if fm.fileExists(atPath: target.path) {
                    let a = try fm.attributesOfItem(atPath: file.path), b = try fm.attributesOfItem(atPath: target.path)
                    guard (a[.size] as? NSNumber) == (b[.size] as? NSNumber),
                          try fileDigest(file) == fileDigest(target) else { throw ModpackImportError.sharedConflict(relative) }
                } else { newFiles.append((file, relative)) }
            }
        }
        // Check every existing shared file before committing any new dependency.
        for (source, relative) in newFiles {
            try Task.checkCancellation()
            let target = try MinecraftInstaller.safeDestination(root: root, relative: relative)
            try fm.createDirectory(at: target.deletingLastPathComponent(), withIntermediateDirectories: true)
            guard !fm.fileExists(atPath: target.path) else { throw ModpackImportError.sharedConflict(relative) }
            try fm.moveItem(at: source, to: target)
        }
    }
    private static func fileDigest(_ url: URL) throws -> String {
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }
        var hasher = SHA256()
        while let data = try handle.read(upToCount: 1024 * 1024), !data.isEmpty {
            try Task.checkCancellation(); hasher.update(data: data)
        }
        return digest(hasher.finalize())
    }
}

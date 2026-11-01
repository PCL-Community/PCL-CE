import Foundation
import CryptoKit
import Darwin
import zlib

public enum VersionPackExportFormat: String, CaseIterable, Codable, Sendable {
    case completeZip, modrinth
    public var displayName: String { self == .completeZip ? "完整版本 ZIP" : "Modrinth 整合包" }
    public var fileExtension: String { self == .completeZip ? "zip" : "mrpack" }
}

public struct VersionPackExportOptions: Codable, Sendable, Equatable {
    public var name = ""
    public var version = "1.0.0"
    public var format: VersionPackExportFormat = .completeZip
    public var includeOptions = true
    public var includeMods = true
    public var includeDisabledMods = false
    public var includeConfig = true
    public var includePackData = true
    public var includeResourcePacks = true
    public var selectedResourcePacks: [String]?
    public var includeShaderPacks = true
    public var selectedShaderPacks: [String]?
    private var shaderPackSettings: Bool? = true
    public var includeShaderPackSettings: Bool {
        get { shaderPackSettings ?? true }
        set { shaderPackSettings = newValue }
    }
    public var includeScreenshots = false
    public var includeStructures = false
    public var includeReplays = false
    public var includeSaves = false
    public var selectedSaves: [String]?
    public var includeServers = false
    public var includeLicences = true
    public init() {}

    private enum CodingKeys: String, CodingKey {
        case name, version, format, includeOptions, includeMods, includeDisabledMods, includeConfig, includePackData
        case includeResourcePacks, selectedResourcePacks, includeShaderPacks, selectedShaderPacks
        case shaderPackSettings = "includeShaderPackSettings"
        case includeScreenshots, includeStructures, includeReplays, includeSaves, selectedSaves, includeServers, includeLicences
    }
}

public struct VersionPackExportItem: Identifiable, Sendable {
    public var id: String { relativePath }
    public let relativePath: String
    public let name: String
}

public struct VersionPackExportProgress: Sendable {
    public let fraction: Double
    public let completedBytes: Int64
    public let totalBytes: Int64
    public let completedFiles: Int
    public let totalFiles: Int
    public let currentFile: String
}

public struct VersionPackExportResult: Sendable {
    public let url: URL
    public let fileCount: Int
    public let bytes: Int64
    public let format: VersionPackExportFormat
}

public enum VersionPackExportError: LocalizedError {
    case unsafePath(String), missingFile(String), changedFile(String), invalidOptions(String), archiveTooLarge, destinationExists, alreadyExporting
    public var errorDescription: String? {
        switch self {
        case .unsafePath(let name): return "导出路径不安全或含有符号链接：\(name)"
        case .missingFile(let name): return "导出需要的游戏文件缺失，请先补全文件：\(name)"
        case .changedFile(let name): return "游戏文件大小或哈希校验失败，原文件未修改：\(name)"
        case .invalidOptions(let name): return "导出配置无效：\(name)"
        case .archiveTooLarge: return "整合包超过普通 ZIP 的大小或文件数量限制，请减少导出内容。"
        case .destinationExists: return "导出位置已有文件，未覆盖原文件。"
        case .alreadyExporting: return "已有整合包正在导出。"
        }
    }
}

/// Creates a single-version portable archive. Source files are read only; a private
/// destination-side temporary ZIP is atomically published after every entry succeeds.
public actor VersionPackExporter {
    private var exporting = false
    public init() {}

    public func export(version: InstalledVersion, gameDirectory: URL, options: VersionPackExportOptions,
                       to destination: URL, replaceExisting: Bool = false,
                       progress: @escaping @Sendable (VersionPackExportProgress) -> Void = { _ in }) async throws -> VersionPackExportResult {
        guard !exporting else { throw VersionPackExportError.alreadyExporting }
        exporting = true
        defer { exporting = false }
        try Task.checkCancellation()
        try Self.validate(options)
        let root = try Self.canonicalExisting(gameDirectory)
        let profile = try Self.checked("versions/\(version.id)", under: root)
        guard profile.path == (try Self.canonicalExisting(version.directory)).path else {
            throw VersionPackExportError.unsafePath(version.directory.path)
        }
        let target = try Self.destinationURL(destination)
        guard target.isFileURL, !target.path.hasPrefix(root.path + "/"), target != root,
              (try? FileManager.default.destinationOfSymbolicLink(atPath: target.path)) == nil else { throw VersionPackExportError.unsafePath(target.path) }
        let existed = FileManager.default.fileExists(atPath: target.path)
        guard !existed || replaceExisting else { throw VersionPackExportError.destinationExists }
        if existed {
            let values = try target.resourceValues(forKeys: [.isRegularFileKey])
            guard values.isRegularFile == true else { throw VersionPackExportError.unsafePath(target.path) }
        }
        progress(.init(fraction: 0, completedBytes: 0, totalBytes: 0, completedFiles: 0, totalFiles: 0, currentFile: "读取版本配置"))
        let entries = try Self.plan(version: version, root: root, profile: profile, options: options)
        guard entries.count < 65_535 else { throw VersionPackExportError.archiveTooLarge }
        let total = entries.reduce(Int64(0)) { $0 + $1.size }
        guard total <= Int64(UInt32.max) - Int64(entries.count * 200), total <= 12 * 1024 * 1024 * 1024 else {
            throw VersionPackExportError.archiveTooLarge
        }
        let temporary = target.deletingLastPathComponent().appendingPathComponent(".pcl-export-\(UUID().uuidString).tmp")
        guard FileManager.default.createFile(atPath: temporary.path, contents: nil) else { throw CocoaError(.fileWriteUnknown) }
        defer { try? FileManager.default.removeItem(at: temporary) }
        let handle = try FileHandle(forWritingTo: temporary)
        defer { try? handle.close() }
        var central = Data(), finished: Int64 = 0
        for (index, entry) in entries.enumerated() {
            try Task.checkCancellation()
            let offset = try handle.offset()
            guard offset <= UInt64(UInt32.max) else { throw VersionPackExportError.archiveTooLarge }
            let name = Data(entry.path.utf8)
            guard name.count <= Int(UInt16.max), entry.size <= 512 * 1024 * 1024 else { throw VersionPackExportError.archiveTooLarge }
            var header = Data()
            header.pack32(0x04034b50); header.pack16(20); header.pack16(0x0808); header.pack16(0)
            header.pack16(0); header.pack16(0x21); header.pack32(0); header.pack32(0); header.pack32(0)
            header.pack16(UInt16(name.count)); header.pack16(0); header.append(name)
            try handle.write(contentsOf: header)
            var checksum: uLong = 0, count: Int64 = 0, digest = Insecure.SHA1()
            func write(_ chunk: Data) throws {
                try Task.checkCancellation()
                checksum = chunk.withUnsafeBytes { bytes in
                    crc32(checksum, bytes.bindMemory(to: UInt8.self).baseAddress, uInt(bytes.count))
                }
                digest.update(data: chunk)
                count += Int64(chunk.count)
                guard count <= entry.size else { throw VersionPackExportError.changedFile(entry.path) }
                try handle.write(contentsOf: chunk)
                finished += Int64(chunk.count)
                progress(.init(fraction: 0.02 + 0.96 * Double(finished) / Double(max(1, total)), completedBytes: finished,
                               totalBytes: total, completedFiles: index, totalFiles: entries.count, currentFile: entry.path))
            }
            if let data = entry.data { try write(data) }
            else if let file = entry.file {
                let checked = try Self.checked(entry.sourceRelative!, under: entry.sourceRoot!)
                guard checked == file else { throw VersionPackExportError.unsafePath(entry.path) }
                let descriptor = Darwin.open(file.path, O_RDONLY | O_NOFOLLOW)
                guard descriptor >= 0 else { throw VersionPackExportError.unsafePath(entry.path) }
                let input = FileHandle(fileDescriptor: descriptor, closeOnDealloc: true)
                defer { try? input.close() }
                var status = stat()
                guard fstat(descriptor, &status) == 0, status.st_mode & S_IFMT == S_IFREG,
                      Int64(status.st_size) == entry.size else { throw VersionPackExportError.changedFile(entry.path) }
                while let chunk = try input.read(upToCount: 1024 * 1024), !chunk.isEmpty { try write(chunk) }
                var after = stat()
                guard fstat(descriptor, &after) == 0, after.st_size == status.st_size,
                      after.st_mtimespec.tv_sec == status.st_mtimespec.tv_sec,
                      after.st_mtimespec.tv_nsec == status.st_mtimespec.tv_nsec,
                      after.st_ctimespec.tv_sec == status.st_ctimespec.tv_sec,
                      after.st_ctimespec.tv_nsec == status.st_ctimespec.tv_nsec else {
                    throw VersionPackExportError.changedFile(entry.path)
                }
            }
            guard count == entry.size else { throw VersionPackExportError.changedFile(entry.path) }
            if let expected = entry.sha1, digest.finalize().map({ String(format: "%02x", $0) }).joined() != expected.lowercased() {
                throw VersionPackExportError.changedFile(entry.path)
            }
            var descriptor = Data(); descriptor.pack32(0x08074b50); descriptor.pack32(UInt32(checksum))
            descriptor.pack32(UInt32(count)); descriptor.pack32(UInt32(count)); try handle.write(contentsOf: descriptor)
            central.pack32(0x02014b50); central.pack16(0x0314); central.pack16(20); central.pack16(0x0808); central.pack16(0)
            central.pack16(0); central.pack16(0x21); central.pack32(UInt32(checksum)); central.pack32(UInt32(count)); central.pack32(UInt32(count))
            central.pack16(UInt16(name.count)); central.pack16(0); central.pack16(0); central.pack16(0); central.pack16(0)
            central.pack32(UInt32(0o100644) << 16); central.pack32(UInt32(offset)); central.append(name)
        }
        try Task.checkCancellation()
        let directoryOffset = try handle.offset()
        guard directoryOffset + UInt64(central.count) + 22 <= UInt64(UInt32.max) else { throw VersionPackExportError.archiveTooLarge }
        try handle.write(contentsOf: central)
        var end = Data(); end.pack32(0x06054b50); end.pack16(0); end.pack16(0)
        end.pack16(UInt16(entries.count)); end.pack16(UInt16(entries.count)); end.pack32(UInt32(central.count)); end.pack32(UInt32(directoryOffset)); end.pack16(0)
        try handle.write(contentsOf: end); try handle.synchronize(); try handle.close()
        try Task.checkCancellation()
        if !replaceExisting && FileManager.default.fileExists(atPath: target.path) { throw VersionPackExportError.destinationExists }
        guard try Self.destinationURL(destination).path == target.path else { throw VersionPackExportError.unsafePath(target.path) }
        if replaceExisting {
            // rename(2) atomically replaces a regular destination on the same volume.
            guard Darwin.rename(temporary.path, target.path) == 0 else { throw CocoaError(.fileWriteUnknown) }
        } else {
            guard Darwin.link(temporary.path, target.path) == 0 else {
                if errno == EEXIST { throw VersionPackExportError.destinationExists }
                throw CocoaError(.fileWriteUnknown)
            }
        }
        let bytes = try target.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
        progress(.init(fraction: 1, completedBytes: total, totalBytes: total, completedFiles: entries.count,
                       totalFiles: entries.count, currentFile: "导出完成"))
        return .init(url: target, fileCount: entries.count, bytes: Int64(bytes), format: options.format)
    }

    public static func validate(_ options: VersionPackExportOptions) throws {
        for value in [options.name, options.version] {
            guard value.count <= 100, value.rangeOfCharacter(from: .controlCharacters) == nil else {
                throw VersionPackExportError.invalidOptions("整合包名称和版本最多 100 字，且不能包含控制字符")
            }
        }
        for (selection, folders) in [(options.selectedResourcePacks, ["resourcepacks", "texturepacks"]),
                                     (options.selectedShaderPacks, ["shaderpacks"]), (options.selectedSaves, ["saves"])] {
            guard let selection else { continue }
            guard selection.count < 65_535 else { throw VersionPackExportError.invalidOptions("选择的项目过多") }
            for name in selection {
                let path = try ModpackZIPArchive.validatedPath(name)
                let parts = path.split(separator: "/").map(String.init)
                guard !name.contains("\\"), (parts.count == 1 || parts.count == 2 && folders.contains(parts[0])),
                      !(parts.last?.hasPrefix(".") ?? true) else { throw VersionPackExportError.invalidOptions("只能选择对应文件夹中的直接子项：\(name)") }
            }
        }
    }

    /// Only direct children that Minecraft can treat as packs are selectable.
    /// Shader sidecar settings are deliberately excluded from this list.
    public static func selectableEntries(in runtime: URL, folders: [String], directoriesOnly: Bool = false) throws -> [VersionPackExportItem] {
        let root = try canonicalExisting(runtime)
        var entries: [VersionPackExportItem] = []
        for folder in folders {
            guard ["resourcepacks", "texturepacks", "shaderpacks", "saves"].contains(folder) else { throw VersionPackExportError.unsafePath(folder) }
            let directory = try checked(folder, under: root)
            guard FileManager.default.fileExists(atPath: directory.path) else { continue }
            let children = try FileManager.default.contentsOfDirectory(at: directory,
                includingPropertiesForKeys: [.isRegularFileKey, .isDirectoryKey, .isSymbolicLinkKey], options: [.skipsHiddenFiles])
            for child in children {
                let values = try child.resourceValues(forKeys: [.isRegularFileKey, .isDirectoryKey, .isSymbolicLinkKey])
                let isPack = values.isDirectory == true || !directoriesOnly && values.isRegularFile == true && child.pathExtension.lowercased() == "zip"
                guard isPack else { continue }
                guard values.isSymbolicLink != true else { throw VersionPackExportError.unsafePath(folder + "/" + child.lastPathComponent) }
                let relative = try ModpackZIPArchive.validatedPath(folder + "/" + child.lastPathComponent)
                let name = folder == "texturepacks" ? child.lastPathComponent + "（texturepacks）" : child.lastPathComponent
                entries.append(.init(relativePath: relative, name: name))
            }
        }
        return entries.sorted { $0.name.localizedStandardCompare($1.name) == .orderedAscending }
    }

    private struct Entry {
        let path: String
        let file: URL?
        let data: Data?
        let size: Int64
        let sha1: String?
        let sourceRoot: URL?
        let sourceRelative: String?
    }

    private static func plan(version: InstalledVersion, root: URL, profile: URL, options: VersionPackExportOptions) throws -> [Entry] {
        let metadata = try MinecraftVersionMetadata.resolve(versionID: version.id, gameDirectory: root)
        let minecraft = metadata["pclOriginalVersion"] as? String ?? version.id
        try MinecraftInstaller.validateComponent(minecraft)
        let settings = try VersionSettingsRepository.load(version: version)
        let runtime = settings.isolateGameDirectory ? profile : root
        var entries: [String: Entry] = [:]
        func addData(_ data: Data, _ path: String) throws {
            let path = try ModpackZIPArchive.validatedPath(path), key = ModpackZIPArchive.pathKey(path)
            guard entries[key] == nil else { throw VersionPackExportError.unsafePath(path) }
            entries[key] = .init(path: path, file: nil, data: data, size: Int64(data.count), sha1: nil, sourceRoot: nil, sourceRelative: nil)
        }
        func addFile(_ relative: String, from source: URL, to path: String, expected: [String: Any]? = nil) throws {
            try Task.checkCancellation()
            let file = try checked(relative, under: source), path = try ModpackZIPArchive.validatedPath(path)
            guard FileManager.default.fileExists(atPath: file.path) else { throw VersionPackExportError.missingFile(relative) }
            let values = try file.resourceValues(forKeys: [.isRegularFileKey, .fileSizeKey])
            guard values.isRegularFile == true else { throw VersionPackExportError.unsafePath(relative) }
            let size = Int64(values.fileSize ?? 0)
            if let expectedSize = (expected?["size"] as? NSNumber)?.int64Value, expectedSize != size { throw VersionPackExportError.changedFile(relative) }
            let key = ModpackZIPArchive.pathKey(path)
            if let old = entries[key] { guard old.file == file else { throw VersionPackExportError.unsafePath(path) }; return }
            entries[key] = .init(path: path, file: file, data: nil, size: size, sha1: expected?["sha1"] as? String, sourceRoot: source, sourceRelative: relative)
        }
        let prefix: String
        if options.format == .completeZip {
            let jarID = metadata["jar"] as? String ?? version.id
            try MinecraftVersionMetadata.validateVersionID(jarID)
            var flattened = metadata
            flattened.removeValue(forKey: "inheritsFrom")
            flattened["id"] = version.id; flattened["jar"] = version.id; flattened["pclOriginalVersion"] = minecraft
            let credentialKeys = Set(["accesstoken", "refreshtoken", "clienttoken", "accounts", "authenticationdatabase", "password", "secret", "credentials"])
            for key in Array(flattened.keys) where credentialKeys.contains(key.lowercased()) { flattened.removeValue(forKey: key) }
            try addData(JSONSerialization.data(withJSONObject: flattened, options: [.sortedKeys, .prettyPrinted]), "versions/\(version.id)/\(version.id).json")
            let client = (metadata["downloads"] as? [String: Any])?["client"] as? [String: Any]
            try addFile("versions/\(jarID)/\(jarID).jar", from: root, to: "versions/\(version.id)/\(version.id).jar", expected: client)
            let safeSettings: [String: Any] = ["isolateGameDirectory": true, "displayDescription": settings.displayDescription,
                                             "iconName": settings.iconName, "category": settings.category == "hidden" ? "auto" : settings.category]
            try addData(JSONSerialization.data(withJSONObject: safeSettings, options: [.sortedKeys, .prettyPrinted]), "versions/\(version.id)/PCL/macos-settings.json")
            for library in metadata["libraries"] as? [[String: Any]] ?? [] where InstallPlatform.libraryAllowed(library) {
                let downloads = library["downloads"] as? [String: Any]
                if downloads == nil || downloads?["artifact"] != nil, let path = MinecraftVersionMetadata.libraryPath(library) {
                    try addFile("libraries/" + path, from: root, to: "libraries/" + path, expected: downloads?["artifact"] as? [String: Any])
                }
                if let classifier = InstallPlatform.nativeClassifier(for: library),
                   let native = (downloads?["classifiers"] as? [String: Any])?[classifier] as? [String: Any], let path = native["path"] as? String {
                    try addFile("libraries/" + path, from: root, to: "libraries/" + path, expected: native)
                }
            }
            if let indexID = (metadata["assetIndex"] as? [String: Any])?["id"] as? String ?? metadata["assets"] as? String {
                try MinecraftVersionMetadata.validateVersionID(indexID)
                let path = "assets/indexes/\(indexID).json"
                try addFile(path, from: root, to: path, expected: metadata["assetIndex"] as? [String: Any])
                let data = try Data(contentsOf: checked(path, under: root))
                guard let index = try JSONSerialization.jsonObject(with: data) as? [String: Any], let objects = index["objects"] as? [String: [String: Any]] else {
                    throw VersionPackExportError.invalidOptions("游戏资源索引无效")
                }
                for value in objects.values {
                    guard let hash = value["hash"] as? String, hash.range(of: "^[a-fA-F0-9]{40}$", options: .regularExpression) != nil else { throw VersionPackExportError.invalidOptions("资源哈希无效") }
                    let objectPath = "assets/objects/\(hash.prefix(2))/\(hash)"
                    try addFile(objectPath, from: root, to: objectPath, expected: ["sha1": hash, "size": value["size"] ?? -1])
                }
            }
            if let logging = (metadata["logging"] as? [String: Any])?["client"] as? [String: Any],
               let file = logging["file"] as? [String: Any], let id = file["id"] as? String {
                try addFile("assets/log_configs/" + id, from: root, to: "assets/log_configs/" + id, expected: file)
            }
            prefix = "versions/\(version.id)/"
        } else {
            var dependencies = ["minecraft": minecraft]
            if let loader = MinecraftVersionMetadata.modLoader(in: metadata) {
                let key: String
                switch loader.kind { case .fabric: key = "fabric-loader"; case .quilt: key = "quilt-loader"; case .forge: key = "forge"; case .neoForge: key = "neoforge" }
                dependencies[key] = loader.version
            }
            let index: [String: Any] = ["formatVersion": 1, "game": "minecraft", "versionId": options.version.isEmpty ? "1.0.0" : options.version,
                                       "name": options.name.isEmpty ? version.id : options.name, "files": [], "dependencies": dependencies]
            try addData(JSONSerialization.data(withJSONObject: index, options: [.sortedKeys, .prettyPrinted]), "modrinth.index.json")
            prefix = "overrides/"
        }
        var roots: [String] = [], requiredSelections = Set<String>()
        func selectedRoots(_ names: [String]?, folders: [String]) throws -> [String] {
            guard let names else { return folders }
            var paths: [String] = []
            for name in names {
                if name.contains("/") {
                    paths.append(name)
                } else {
                    let matches = try folders.map { try checked($0 + "/" + name, under: runtime) }
                        .filter { FileManager.default.fileExists(atPath: $0.path) }
                    guard !matches.isEmpty else { throw VersionPackExportError.missingFile(name) }
                    paths += matches.map { String($0.path.dropFirst(runtime.path.count + 1)) }
                }
            }
            requiredSelections.formUnion(paths)
            return paths
        }
        func selectedPacks(_ names: [String]?, folders: [String]) throws -> [String] {
            let paths: [String]
            if let names { paths = try selectedRoots(names, folders: folders) }
            else { paths = try selectableEntries(in: runtime, folders: folders).map(\.relativePath) }
            for path in paths {
                let file = try checked(path, under: runtime)
                guard FileManager.default.fileExists(atPath: file.path) else { throw VersionPackExportError.missingFile(path) }
                let values = try file.resourceValues(forKeys: [.isRegularFileKey, .isDirectoryKey])
                guard values.isDirectory == true || values.isRegularFile == true && file.pathExtension.lowercased() == "zip" else {
                    throw VersionPackExportError.invalidOptions("请选择 ZIP 或文件夹形式的资源包、光影包：\(path)")
                }
            }
            return paths
        }
        if options.includeOptions { roots += ["options.txt", "optionsof.txt", "optionsshaders.txt", "configureddefaults"] }
        if options.includeMods { roots += ["mods", "coremods", "lib"] }
        if options.includeMods && options.includeConfig { roots += ["config", "defaultconfigs", "journeymap/config"] }
        if options.includeMods && options.includePackData { roots += ["scripts", "kubejs", "resources", "global_packs", "global_resource_packs", "global_data_packs", "datapacks", "openloader", "patchouli_books", "structures", "tacz", "config/tacz/custom", "icon.png", "packmenu", "maps", "template", "resource_assorts.json"] }
        if options.includeResourcePacks { roots += try selectedPacks(options.selectedResourcePacks, folders: ["resourcepacks", "texturepacks"]) }
        if options.includeShaderPacks {
            let packs = try selectedPacks(options.selectedShaderPacks, folders: ["shaderpacks"])
            roots += packs
            if options.includeShaderPackSettings {
                for pack in packs {
                    let companion = pack + ".txt"
                    if FileManager.default.fileExists(atPath: runtime.appendingPathComponent(companion).path) {
                        roots.append(companion)
                        requiredSelections.insert(companion)
                    }
                }
            }
        }
        if options.includeScreenshots { roots += ["screenshots"] }
        if options.includeStructures { roots += ["schematics"] }
        if options.includeReplays { roots += ["replay_recordings", "replay_videos"] }
        if options.includeSaves { roots += try selectedRoots(options.selectedSaves, folders: ["saves"]) }
        if options.includeServers { roots += ["servers.dat"] }
        if options.includeLicences {
            roots += try FileManager.default.contentsOfDirectory(atPath: runtime.path).filter { $0.uppercased().hasPrefix("LICEN") }
        }
        for relative in roots {
            try Task.checkCancellation()
            let source = try checked(relative, under: runtime)
            guard FileManager.default.fileExists(atPath: source.path) else {
                if requiredSelections.contains(relative) { throw VersionPackExportError.missingFile(relative) }
                continue
            }
            let values = try source.resourceValues(forKeys: [.isDirectoryKey, .isSymbolicLinkKey])
            guard values.isSymbolicLink != true else { throw VersionPackExportError.unsafePath(relative) }
            if values.isDirectory == true {
                var enumerationError: Error?
                guard let enumerator = FileManager.default.enumerator(at: source, includingPropertiesForKeys: [.isRegularFileKey, .isSymbolicLinkKey, .isDirectoryKey], options: [.skipsHiddenFiles], errorHandler: { _, error in
                    enumerationError = error
                    return false
                }) else { throw VersionPackExportError.missingFile(relative) }
                while let url = enumerator.nextObject() as? URL {
                    try Task.checkCancellation()
                    let rawValues = try url.resourceValues(forKeys: [.isSymbolicLinkKey])
                    guard rawValues.isSymbolicLink != true else { throw VersionPackExportError.unsafePath(url.lastPathComponent) }
                    let canonical = try canonicalExisting(url)
                    guard canonical.path.hasPrefix(runtime.path + "/") else { throw VersionPackExportError.unsafePath(url.lastPathComponent) }
                    let path = String(canonical.path.dropFirst(runtime.path.count + 1))
                    if excluded(path, options: options) { enumerator.skipDescendants(); continue }
                    let values = try url.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey])
                    guard values.isSymbolicLink != true else { throw VersionPackExportError.unsafePath(path) }
                    if values.isRegularFile == true { try addFile(path, from: runtime, to: prefix + path) }
                }
                if let enumerationError { throw enumerationError }
            } else if !excluded(relative, options: options) { try addFile(relative, from: runtime, to: prefix + relative) }
        }
        return entries.values.sorted { $0.path < $1.path }
    }

    private static func excluded(_ path: String, options: VersionPackExportOptions) -> Bool {
        let lower = path.lowercased(), components = lower.split(separator: "/").map(String.init), name = components.last ?? ""
        if name.hasSuffix(".log") || name.hasSuffix(".dat_old") || name == "session.lock" || name == ".ds_store" { return true }
        if ["accounts.json", "microsoft_accounts.json", "launcher_accounts.json", "authentication_database.json", "usercache.json", "usernamecache.json", "secrets.json", "credentials.json", "tokens.json", "auth.json"].contains(name) { return true }
        if components.contains(where: { ["logs", "crash-reports", "cache", ".cache", "probe", "exported"].contains($0) }) { return true }
        if lower.hasPrefix("config/jei/world/") || lower.hasPrefix("config/worldedit/") || lower.hasPrefix("config/spark/") { return true }
        if lower.hasPrefix("mods/") && !options.includeDisabledMods && (name.hasSuffix(".disabled") || name.hasSuffix(".old")) { return true }
        if options.format == .modrinth && lower.hasPrefix("mods/") && name.contains("optifine") { return true }
        return false
    }

    private static func checked(_ relative: String, under root: URL) throws -> URL {
        let relative = try ModpackZIPArchive.validatedPath(relative)
        var result = root
        for component in relative.split(separator: "/") {
            result.appendPathComponent(String(component))
            if (try? FileManager.default.destinationOfSymbolicLink(atPath: result.path)) != nil { throw VersionPackExportError.unsafePath(relative) }
        }
        guard result.path.hasPrefix(root.path + "/") else { throw VersionPackExportError.unsafePath(relative) }
        return result
    }

    private static func canonicalExisting(_ url: URL) throws -> URL {
        guard let pointer = realpath(url.path, nil) else { throw VersionPackExportError.missingFile(url.lastPathComponent) }
        defer { free(pointer) }
        // Foundation standardization rewrites /private/var to the /var symlink.
        return URL(fileURLWithPath: String(cString: pointer))
    }

    private static func destinationURL(_ url: URL) throws -> URL {
        guard url.isFileURL, !url.lastPathComponent.isEmpty,
              (try? FileManager.default.destinationOfSymbolicLink(atPath: url.path)) == nil else { throw VersionPackExportError.unsafePath(url.path) }
        let parent = try canonicalExisting(url.deletingLastPathComponent())
        return parent.appendingPathComponent(url.lastPathComponent)
    }
}

private extension Data {
    mutating func pack16(_ value: UInt16) { append(UInt8(truncatingIfNeeded: value)); append(UInt8(truncatingIfNeeded: value >> 8)) }
    mutating func pack32(_ value: UInt32) { pack16(UInt16(truncatingIfNeeded: value)); pack16(UInt16(truncatingIfNeeded: value >> 16)) }
}

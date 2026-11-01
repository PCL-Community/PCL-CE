import Foundation
import Compression
import zlib

public struct LocalModFile: Identifiable, Sendable, Hashable {
    public var id: String { url.lastPathComponent }
    public let url: URL
    public let name: String
    public let version: String
    public let modID: String
    public let summary: String
    public let enabled: Bool
    public let error: String?
    public let byteCount: Int64
    public let iconData: Data?
    public var filename: String { url.lastPathComponent }
}

public enum LocalModError: LocalizedError {
    case unsafePath(String), collision(String), invalidFile(String), rollback(String)
    public var errorDescription: String? {
        switch self {
        case .unsafePath(let value): return "Mod 文件路径不可用：\(value)"
        case .collision(let value): return "已有同名 Mod 文件，原文件未被覆盖：\(value)"
        case .invalidFile(let value): return "Mod 文件无效：\(value)"
        case .rollback(let value): return "Mod 文件操作未能恢复，请检查：\(value)"
        }
    }
}

/// Original PageInstanceMod uses the version's actual isolated/common mods directory.
public enum LocalModManager {
    public static func directory(gameDirectory: URL, version: InstalledVersion, isolate: Bool, create: Bool = false) throws -> URL {
        try MinecraftVersionMetadata.validateVersionID(version.id)
        guard (try? FileManager.default.destinationOfSymbolicLink(atPath: version.directory.path)) == nil else { throw LocalModError.unsafePath(version.directory.path) }
        let root = gameDirectory.resolvingSymlinksInPath().standardizedFileURL
        let profile = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(version.id)", under: root)
        guard profile.resolvingSymlinksInPath().standardizedFileURL == version.directory.resolvingSymlinksInPath().standardizedFileURL else {
            throw LocalModError.unsafePath(version.directory.path)
        }
        let runtime = isolate ? profile : root
        let raw = runtime.appendingPathComponent("mods", isDirectory: true)
        guard (try? FileManager.default.destinationOfSymbolicLink(atPath: raw.path)) == nil else { throw LocalModError.unsafePath(raw.path) }
        let result = try MinecraftVersionMetadata.safeFile(relativePath: "mods", under: runtime)
        if create { try FileManager.default.createDirectory(at: result, withIntermediateDirectories: true) }
        return result
    }

    public static func scan(directory: URL) throws -> [LocalModFile] {
        let folder = try validatedDirectory(directory, create: false)
        guard FileManager.default.fileExists(atPath: folder.path) else { return [] }
        let files = try FileManager.default.contentsOfDirectory(at: folder, includingPropertiesForKeys: [.isRegularFileKey, .isSymbolicLinkKey, .fileSizeKey], options: [.skipsHiddenFiles])
        return try files.filter { recognized($0.lastPathComponent) }.map { file in
            let values = try file.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey, .fileSizeKey])
            guard values.isSymbolicLink != true, values.isRegularFile == true else { throw LocalModError.unsafePath(file.lastPathComponent) }
            try rejectHardLink(file)
            var name = enabledName(file.lastPathComponent)
            name = (name as NSString).deletingPathExtension
            var version = "", modID = "", summary = "", issue: String?
            var imageData: Data?
            do {
                let metadata = try readMetadata(file)
                name = metadata.name.isEmpty ? name : metadata.name
                version = metadata.version; modID = metadata.id; summary = metadata.summary; imageData = metadata.iconData
            } catch { issue = error.localizedDescription }
            return LocalModFile(url: file, name: name, version: version, modID: modID, summary: summary,
                                enabled: isEnabled(file.lastPathComponent), error: issue, byteCount: Int64(values.fileSize ?? 0), iconData: imageData)
        }.sorted {
            let result = $0.name.localizedStandardCompare($1.name)
            return result == .orderedSame ? $0.filename.localizedStandardCompare($1.filename) == .orderedAscending : result == .orderedAscending
        }
    }

    public static func validatedURLs(_ files: [LocalModFile], directory: URL) throws -> [URL] {
        let folder = try validatedDirectory(directory, create: false)
        var seen = Set<String>()
        return try files.map { file in
            let url = try member(file.filename, folder: folder, mustExist: true)
            guard url.resolvingSymlinksInPath().standardizedFileURL == file.url.resolvingSymlinksInPath().standardizedFileURL, seen.insert(pathKey(file.filename)).inserted else { throw LocalModError.unsafePath(file.filename) }
            return url
        }
    }

    /// Preflight every rename and roll back completed moves if any rename fails.
    public static func setEnabled(_ enabled: Bool, files: [LocalModFile], directory: URL) throws {
        let folder = try validatedDirectory(directory, create: false)
        let sources = try validatedURLs(files, directory: folder)
        let existing = try existingNames(folder)
        var destinations = Set<String>()
        var moves: [(URL, URL)] = []
        for (file, source) in zip(files, sources) where file.enabled != enabled {
            let name = enabled ? enabledName(file.filename) : file.filename + ".disabled"
            guard isEnabled(name) == enabled, recognized(name) else { throw LocalModError.invalidFile(name) }
            let destination = try member(name, folder: folder, mustExist: false)
            let key = pathKey(name)
            guard !existing.contains(key), destinations.insert(key).inserted else { throw LocalModError.collision(name) }
            moves.append((source, destination))
        }
        var committed: [(URL, URL)] = []
        do {
            for move in moves { try FileManager.default.moveItem(at: move.0, to: move.1); committed.append(move) }
        } catch {
            for move in committed.reversed() {
                do { try FileManager.default.moveItem(at: move.1, to: move.0) }
                catch { throw LocalModError.rollback(move.1.path) }
            }
            throw error
        }
    }

    /// Import is staged; a different existing file is never replaced.
    @discardableResult public static func importFiles(_ sources: [URL], directory: URL) throws -> Int {
        let folder = try validatedDirectory(directory, create: true)
        let names = try existingNames(folder)
        var plannedNames = Set<String>()
        var plans: [(URL, URL)] = []
        for source in sources {
            let values = try source.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey])
            guard values.isSymbolicLink != true, values.isRegularFile == true, source.pathExtension.lowercased() == "jar" else { throw LocalModError.invalidFile(source.lastPathComponent) }
            try rejectHardLink(source)
            _ = try LocalModArchive(url: source)
            let destination = try member(source.lastPathComponent, folder: folder, mustExist: false)
            let key = pathKey(source.lastPathComponent)
            guard !names.contains(key), plannedNames.insert(key).inserted else { throw LocalModError.collision(source.lastPathComponent) }
            plans.append((source, destination))
        }
        let staging = folder.appendingPathComponent(".pcl-import-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: staging, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: staging) }
        for (index, plan) in plans.enumerated() { try FileManager.default.copyItem(at: plan.0, to: staging.appendingPathComponent(String(index))) }
        var committed: [URL] = []
        do {
            for (index, plan) in plans.enumerated() {
                guard !(try existingNames(folder)).contains(pathKey(plan.1.lastPathComponent)) else { throw LocalModError.collision(plan.1.lastPathComponent) }
                try FileManager.default.moveItem(at: staging.appendingPathComponent(String(index)), to: plan.1)
                committed.append(plan.1)
            }
        } catch {
            for file in committed { try FileManager.default.removeItem(at: file) }
            throw error
        }
        return committed.count
    }

    private static func validatedDirectory(_ directory: URL, create: Bool) throws -> URL {
        guard (try? FileManager.default.destinationOfSymbolicLink(atPath: directory.path)) == nil else { throw LocalModError.unsafePath(directory.path) }
        let folder = directory.resolvingSymlinksInPath().standardizedFileURL
        if create { try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true) }
        if FileManager.default.fileExists(atPath: folder.path), (try folder.resourceValues(forKeys: [.isDirectoryKey])).isDirectory != true { throw LocalModError.unsafePath(folder.path) }
        return folder
    }
    private static func member(_ name: String, folder: URL, mustExist: Bool) throws -> URL {
        guard !name.isEmpty, !name.contains("/"), !name.contains("\\"), name != ".", name != ".." else { throw LocalModError.unsafePath(name) }
        let raw = folder.appendingPathComponent(name)
        guard (try? FileManager.default.destinationOfSymbolicLink(atPath: raw.path)) == nil else { throw LocalModError.unsafePath(name) }
        let result = try MinecraftVersionMetadata.safeFile(relativePath: name, under: folder)
        if mustExist {
            let values = try result.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey])
            guard values.isRegularFile == true, values.isSymbolicLink != true else { throw LocalModError.unsafePath(name) }
            try rejectHardLink(result)
        }
        return result
    }
    private static func rejectHardLink(_ file: URL) throws {
        let attributes = try FileManager.default.attributesOfItem(atPath: file.path)
        if let count = attributes[.referenceCount] as? NSNumber, count.intValue > 1 { throw LocalModError.unsafePath(file.lastPathComponent) }
    }
    private static func existingNames(_ folder: URL) throws -> Set<String> {
        Set(try FileManager.default.contentsOfDirectory(atPath: folder.path).map(pathKey))
    }
    private static func pathKey(_ name: String) -> String { name.precomposedStringWithCanonicalMapping.lowercased() }
    private static func recognized(_ filename: String) -> Bool {
        let name = enabledName(filename).lowercased()
        return name.hasSuffix(".jar") || name.hasSuffix(".zip") || name.hasSuffix(".litemod")
    }
    private static func isEnabled(_ filename: String) -> Bool {
        let ext = (filename as NSString).pathExtension.lowercased()
        return ["jar", "zip", "litemod"].contains(ext)
    }
    private static func enabledName(_ filename: String) -> String {
        var name = filename
        while name.lowercased().hasSuffix(".disabled") || name.lowercased().hasSuffix(".old") {
            name = String(name.dropLast(name.lowercased().hasSuffix(".disabled") ? 9 : 4))
        }
        if name != filename, !["jar", "zip", "litemod"].contains((name as NSString).pathExtension.lowercased()) { name += ".jar" }
        return name
    }

    private struct Metadata { var name = ""; var version = ""; var id = ""; var summary = ""; var iconData: Data? }
    private static func readMetadata(_ url: URL) throws -> Metadata {
        let zip = try LocalModArchive(url: url)
        func json(_ name: String) throws -> Any? {
            guard let entry = zip.member(name) else { return nil }
            let data = try zip.data(for: entry, maximumBytes: 1024 * 1024)
            if let object = try? JSONSerialization.jsonObject(with: data) { return object }
            // Gson accepts literal line breaks in descriptive strings; normalize only
            // the in-memory metadata, preserving the original JAR bytes.
            var bytes: [UInt8] = [], quoted = false, escaped = false
            for byte in data {
                if quoted && byte < 0x20 {
                    bytes.append(contentsOf: String(format: "\\u%04x", Int(byte)).utf8)
                } else {
                    bytes.append(byte)
                    if escaped { escaped = false }
                    else if quoted && byte == 0x5c { escaped = true }
                    else if byte == 0x22 { quoted.toggle() }
                }
            }
            return try JSONSerialization.jsonObject(with: Data(bytes))
        }
        func icon(_ value: Any?) -> Data? {
            let path: String?
            if let text = value as? String { path = text }
            else if let sizes = value as? [String: String] {
                path = sizes.sorted { (Int($0.key) ?? 0) > (Int($1.key) ?? 0) }.first?.value
            } else { path = nil }
            guard let path, let entry = zip.member(path) else { return nil }
            return try? zip.data(for: entry, maximumBytes: 512 * 1024)
        }
        if let object = try json("fabric.mod.json") as? [String: Any] {
            return Metadata(name: object["name"] as? String ?? "", version: string(object["version"]), id: string(object["id"]), summary: string(object["description"]), iconData: icon(object["icon"] ?? object["logoFile"]))
        }
        if let object = try json("quilt.mod.json") as? [String: Any], let loader = object["quilt_loader"] as? [String: Any] {
            let metadata = loader["metadata"] as? [String: Any] ?? [:]
            return Metadata(name: string(metadata["name"]), version: string(loader["version"]), id: string(loader["id"]), summary: string(metadata["description"]), iconData: icon(metadata["icon"]))
        }
        for path in ["META-INF/neoforge.mods.toml", "META-INF/mods.toml"] {
            guard let entry = zip.member(path) else { continue }
            let text = String(decoding: try zip.data(for: entry, maximumBytes: 1024 * 1024), as: UTF8.self)
            let block = text.components(separatedBy: "[[mods]]").dropFirst().first ?? text
            func value(_ key: String) -> String {
                let pattern = "(?m)^\\s*" + NSRegularExpression.escapedPattern(for: key) + "\\s*=\\s*[\"']([^\"'\\n]*)[\"']"
                guard let range = block.range(of: pattern, options: .regularExpression), let expression = try? NSRegularExpression(pattern: pattern),
                      let match = expression.firstMatch(in: block, range: NSRange(block.startIndex..., in: block)), let capture = Range(match.range(at: 1), in: block) else { return "" }
                _ = range; return String(block[capture])
            }
            var version = value("version")
            if version.contains("${"), let manifest = zip.member("META-INF/MANIFEST.MF") {
                let text = String(decoding: try zip.data(for: manifest, maximumBytes: 1024 * 1024), as: UTF8.self)
                version = text.components(separatedBy: .newlines).first(where: { $0.hasPrefix("Implementation-Version:") }).map { String($0.dropFirst("Implementation-Version:".count)).trimmingCharacters(in: .whitespaces) } ?? ""
            }
            return Metadata(name: value("displayName"), version: version, id: value("modId"), summary: value("description"), iconData: icon(value("logoFile")))
        }
        if let values = try json("mcmod.info") as? [[String: Any]], let object = values.first {
            return Metadata(name: string(object["name"]), version: string(object["version"]), id: string(object["modid"]), summary: string(object["description"]), iconData: icon(object["icon"] ?? object["logoFile"]))
        }
        return Metadata()
    }
    private static func string(_ value: Any?) -> String { value as? String ?? (value as? NSNumber)?.stringValue ?? "" }
}

/// A JAR is read in place, never extracted. Java resource names are case-sensitive
/// and archives may repeat unrelated directory entries, unlike modpack paths.
private struct LocalModArchive {
    struct Entry { let path: String; let rawName: Data; let flags: Int; let method: Int; let size: Int; let compressedSize: Int; let offset: Int; let crc: UInt32 }
    private let bytes: Data
    private let centralOffset: Int
    private let entries: [String: [Entry]]

    init(url: URL) throws {
        let data = try Data(contentsOf: url, options: .mappedIfSafe)
        guard data.count >= 22, data.count <= 2 * 1024 * 1024 * 1024 else { throw LocalModError.invalidFile(url.lastPathComponent) }
        func u16(_ i: Int) -> Int { Int(data[i]) | Int(data[i+1]) << 8 }
        func u32(_ i: Int) -> UInt32 { UInt32(data[i]) | UInt32(data[i+1]) << 8 | UInt32(data[i+2]) << 16 | UInt32(data[i+3]) << 24 }
        var end: Int?
        for index in stride(from: data.count - 22, through: max(0, data.count - 65_557), by: -1) {
            if u32(index) == 0x06054b50, index + 22 + u16(index + 20) == data.count { end = index; break }
        }
        guard let end, u16(end+4) == 0, u16(end+6) == 0, u16(end+8) == u16(end+10) else { throw LocalModError.invalidFile(url.lastPathComponent) }
        let count = u16(end+10), length = Int(u32(end+12)), start = Int(u32(end+16))
        guard count != 0xffff, length != Int(UInt32.max), start != Int(UInt32.max), start >= 0, start + length <= end else { throw LocalModError.invalidFile("暂不支持此 JAR 的 ZIP64 或分卷结构") }
        var position = start, parsed: [String: [Entry]] = [:]
        for _ in 0..<count {
            guard position + 46 <= start + length, u32(position) == 0x02014b50 else { throw LocalModError.invalidFile(url.lastPathComponent) }
            let nameCount = u16(position+28), extraCount = u16(position+30), commentCount = u16(position+32)
            let next = position + 46 + nameCount + extraCount + commentCount
            guard next <= start + length else { throw LocalModError.invalidFile(url.lastPathComponent) }
            let raw = data.subdata(in: (position+46)..<(position+46+nameCount))
            if let name = String(data: raw, encoding: .utf8) {
                let entry = Entry(path: name, rawName: raw, flags: u16(position+8), method: u16(position+10), size: Int(u32(position+24)), compressedSize: Int(u32(position+20)), offset: Int(u32(position+42)), crc: u32(position+16))
                parsed[name, default: []].append(entry)
            }
            position = next
        }
        guard position == start + length else { throw LocalModError.invalidFile(url.lastPathComponent) }
        self.bytes = data; self.centralOffset = start; self.entries = parsed
    }

    func member(_ path: String) -> Entry? { entries[path]?.first }
    func data(for entry: Entry, maximumBytes: Int) throws -> Data {
        guard entries[entry.path]?.count == 1, entry.flags & 1 == 0,
              entry.size >= 0, entry.size <= maximumBytes, entry.compressedSize >= 0,
              entry.compressedSize <= max(maximumBytes, 65_536) * 4,
              entry.offset >= 0, entry.offset + 30 <= centralOffset else { throw LocalModError.invalidFile(entry.path) }
        func u16(_ i: Int) -> Int { Int(bytes[i]) | Int(bytes[i+1]) << 8 }
        func u32(_ i: Int) -> UInt32 { UInt32(bytes[i]) | UInt32(bytes[i+1]) << 8 | UInt32(bytes[i+2]) << 16 | UInt32(bytes[i+3]) << 24 }
        let offset = entry.offset
        guard u32(offset) == 0x04034b50, u16(offset+6) == entry.flags, u16(offset+8) == entry.method else { throw LocalModError.invalidFile(entry.path) }
        let nameCount = u16(offset+26), extraCount = u16(offset+28)
        let begin = offset + 30 + nameCount + extraCount
        guard begin >= 0, begin + entry.compressedSize <= centralOffset,
              bytes.subdata(in: (offset+30)..<(offset+30+nameCount)) == entry.rawName else { throw LocalModError.invalidFile(entry.path) }
        let compressed = bytes.subdata(in: begin..<(begin+entry.compressedSize))
        let output: Data
        if entry.method == 0 {
            guard entry.compressedSize == entry.size else { throw LocalModError.invalidFile(entry.path) }
            output = compressed
        } else if entry.method == 8 {
            var decoded = Data(count: max(1, entry.size))
            let written = decoded.withUnsafeMutableBytes { destination in
                compressed.withUnsafeBytes { source -> Int in
                    guard let source = source.baseAddress?.assumingMemoryBound(to: UInt8.self), let destination = destination.baseAddress?.assumingMemoryBound(to: UInt8.self) else { return 0 }
                    return compression_decode_buffer(destination, max(1, entry.size), source, compressed.count, nil, COMPRESSION_ZLIB)
                }
            }
            guard written == entry.size else { throw LocalModError.invalidFile(entry.path) }
            output = entry.size == 0 ? Data() : decoded
        } else { throw LocalModError.invalidFile("不支持的 JAR 压缩方式") }
        let crc = output.withUnsafeBytes { buffer -> UInt32 in
            UInt32(truncatingIfNeeded: zlib.crc32(0, buffer.baseAddress?.assumingMemoryBound(to: Bytef.self), uInt(output.count)))
        }
        guard crc == entry.crc else { throw LocalModError.invalidFile("\(entry.path) 校验失败") }
        return output
    }
}

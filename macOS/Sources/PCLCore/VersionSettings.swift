import Foundation

public struct VersionProfileSettings: Codable, Sendable, Equatable {
    public var displayDescription = ""
    public var iconName = ""
    public var category = "auto"
    public var isFavorite = false
    public var useGlobalLaunchSettings = true
    public var memoryMB = 4096
    public var javaPath = ""
    public var extraJVMArguments = ""
    public var isolateGameDirectory = false
    public var width = 1100
    public var height = 700
    // Nil preserves the pre-existing all-or-nothing setting when reading older profiles.
    public var memoryMode: String?
    public var javaMode: String?
    public var jvmArgumentsMode: String?
    public var windowMode: String?
    public var javaVersionRange = ""
    public var extraGameArguments = ""
    public var customInfo = ""
    public var serverAddress = ""
    public var garbageCollector = "global"

    public var memoryConfigurationMode: String { memoryMode ?? (useGlobalLaunchSettings ? "global" : "custom") }
    public var javaConfigurationMode: String { javaMode ?? (useGlobalLaunchSettings ? "global" : (javaPath.isEmpty ? "auto" : "custom")) }
    public var jvmConfigurationMode: String { jvmArgumentsMode ?? (useGlobalLaunchSettings ? "global" : "custom") }
    public var windowConfigurationMode: String { windowMode ?? (useGlobalLaunchSettings ? "global" : "custom") }

    public func resolvedMemoryMB(globalValue: Int, availableMemoryMB: Int, modCount: Int) -> Int {
        switch memoryConfigurationMode {
        case "global": return globalValue
        case "auto":
            let requested = modCount > 200 ? 8192 : modCount > 100 ? 6144 : modCount > 50 ? 4096 : 3072
            let budget = max(512, min(availableMemoryMB - 1024, availableMemoryMB * 3 / 4))
            return max(512, min(requested, budget) / 256 * 256)
        default: return memoryMB
        }
    }

    public init() {}

    private enum CodingKeys: String, CodingKey {
        case displayDescription, iconName, category, isFavorite, useGlobalLaunchSettings
        case memoryMB, javaPath, extraJVMArguments, isolateGameDirectory, width, height
        case memoryMode, javaMode, jvmArgumentsMode, windowMode, javaVersionRange
        case extraGameArguments, customInfo, serverAddress, garbageCollector
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        displayDescription = try values.decodeIfPresent(String.self, forKey: .displayDescription) ?? ""
        iconName = try values.decodeIfPresent(String.self, forKey: .iconName) ?? ""
        category = try values.decodeIfPresent(String.self, forKey: .category) ?? "auto"
        isFavorite = try values.decodeIfPresent(Bool.self, forKey: .isFavorite) ?? false
        useGlobalLaunchSettings = try values.decodeIfPresent(Bool.self, forKey: .useGlobalLaunchSettings) ?? true
        memoryMB = try values.decodeIfPresent(Int.self, forKey: .memoryMB) ?? 4096
        javaPath = try values.decodeIfPresent(String.self, forKey: .javaPath) ?? ""
        extraJVMArguments = try values.decodeIfPresent(String.self, forKey: .extraJVMArguments) ?? ""
        isolateGameDirectory = try values.decodeIfPresent(Bool.self, forKey: .isolateGameDirectory) ?? false
        width = try values.decodeIfPresent(Int.self, forKey: .width) ?? 1100
        height = try values.decodeIfPresent(Int.self, forKey: .height) ?? 700
        memoryMode = try values.decodeIfPresent(String.self, forKey: .memoryMode)
        javaMode = try values.decodeIfPresent(String.self, forKey: .javaMode)
        jvmArgumentsMode = try values.decodeIfPresent(String.self, forKey: .jvmArgumentsMode)
        windowMode = try values.decodeIfPresent(String.self, forKey: .windowMode)
        javaVersionRange = try values.decodeIfPresent(String.self, forKey: .javaVersionRange) ?? ""
        extraGameArguments = try values.decodeIfPresent(String.self, forKey: .extraGameArguments) ?? ""
        customInfo = try values.decodeIfPresent(String.self, forKey: .customInfo) ?? ""
        serverAddress = try values.decodeIfPresent(String.self, forKey: .serverAddress) ?? ""
        garbageCollector = try values.decodeIfPresent(String.self, forKey: .garbageCollector) ?? "global"
    }

    public func validate() throws {
        guard ["auto", "hidden", "modded", "regular", "rare", "fool"].contains(category) else {
            throw VersionManagementError.invalidSettings("未知的版本分类")
        }
        guard memoryMB >= 256 && memoryMB <= 1_048_576 else { throw VersionManagementError.invalidSettings("内存应在 256 MB 到 1 TB 之间") }
        guard (320...16_384).contains(width), (240...16_384).contains(height) else {
            throw VersionManagementError.invalidSettings("游戏窗口尺寸超出允许范围")
        }
        guard ["global", "auto", "custom"].contains(memoryConfigurationMode),
              ["global", "auto", "custom", "folder", "range"].contains(javaConfigurationMode),
              ["global", "custom"].contains(jvmConfigurationMode), ["global", "custom"].contains(windowConfigurationMode),
              ["global", "g1", "g1tuned", "zgc", "generational-zgc", "none"].contains(garbageCollector) else {
            throw VersionManagementError.invalidSettings("未知的启动配置方式")
        }
        guard ![javaPath, extraJVMArguments, extraGameArguments, customInfo, serverAddress, javaVersionRange].contains(where: { $0.contains("\0") }) else {
            throw VersionManagementError.invalidSettings("参数中不能包含空字符")
        }
        _ = try MinecraftArguments.tokenize(extraJVMArguments)
        _ = try MinecraftArguments.tokenize(extraGameArguments)
        if !serverAddress.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { _ = try MinecraftServerAddress(serverAddress) }
        // Allow an incomplete range while editing; validate it when starting Java selection.
    }
}

public struct MinecraftServerAddress: Sendable, Equatable {
    public let address: String
    public let host: String
    public let port: Int

    public init(_ input: String) throws {
        let value = input.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !value.isEmpty, !value.contains("://"), !value.contains(where: { $0.isWhitespace || $0.isNewline || $0 == "/" || $0 == "\0" }) else {
            throw VersionManagementError.invalidSettings("服务器地址应为主机名或 IP，可附带端口")
        }
        let parsedHost: String, portText: String?
        if value.hasPrefix("[") {
            guard let close = value.firstIndex(of: "]") else { throw VersionManagementError.invalidSettings("IPv6 地址缺少右方括号") }
            parsedHost = String(value[value.index(after: value.startIndex)..<close])
            let suffix = String(value[value.index(after: close)...])
            guard suffix.isEmpty || suffix.hasPrefix(":") else { throw VersionManagementError.invalidSettings("服务器端口无效") }
            portText = suffix.isEmpty ? nil : String(suffix.dropFirst())
        } else if value.filter({ $0 == ":" }).count == 1, let colon = value.lastIndex(of: ":") {
            parsedHost = String(value[..<colon]); portText = String(value[value.index(after: colon)...])
        } else { parsedHost = value; portText = nil }
        guard !parsedHost.isEmpty, let parsedPort = portText.map(Int.init) ?? 25565, (1...65535).contains(parsedPort) else {
            throw VersionManagementError.invalidSettings("服务器端口应为 1–65535")
        }
        address = value; host = parsedHost; port = parsedPort
    }
}

public struct VersionJavaRange: Sendable {
    private let lower: [Int]?
    private let upper: [Int]?
    private let lowerInclusive: Bool
    private let upperInclusive: Bool

    public init(_ input: String) throws {
        let text = input.trimmingCharacters(in: .whitespacesAndNewlines)
        guard text.count >= 3, let first = text.first, let last = text.last,
              ["[", "("].contains(first), ["]", ")"].contains(last) else { throw Self.invalid }
        let halves = text.dropFirst().dropLast().split(separator: ",", omittingEmptySubsequences: false)
        guard halves.count == 2 else { throw Self.invalid }
        func boundary(_ value: Substring) throws -> [Int]? {
            let trimmed = value.trimmingCharacters(in: .whitespaces)
            if trimmed.isEmpty { return nil }
            guard let parsed = Self.versionParts(trimmed) else { throw Self.invalid }
            return parsed
        }
        lower = try boundary(halves[0]); upper = try boundary(halves[1])
        lowerInclusive = first == "["; upperInclusive = last == "]"
        if let lower, let upper, Self.compare(lower, upper) == .orderedDescending { throw Self.invalid }
    }

    public func contains(_ runtime: JavaRuntime) -> Bool {
        let version = runtime.displayName.hasPrefix("Java ") ? String(runtime.displayName.dropFirst(5).prefix { !$0.isWhitespace }) : String(runtime.majorVersion)
        return contains(version: version)
    }

    public func contains(version: String) -> Bool {
        guard let parts = Self.versionParts(version) else { return false }
        if let lower {
            let comparison = Self.compare(parts, lower)
            if comparison == .orderedAscending || (comparison == .orderedSame && !lowerInclusive) { return false }
        }
        if let upper {
            let comparison = Self.compare(parts, upper)
            if comparison == .orderedDescending || (comparison == .orderedSame && !upperInclusive) { return false }
        }
        return true
    }

    private static var invalid: VersionManagementError { .invalidSettings("Java 版本范围格式应为 [17.0.1, 25.0)；方括号包含边界，圆括号不包含") }
    private static func versionParts(_ value: String) -> [Int]? {
        let stable = value.split(separator: "-", maxSplits: 1).first.map(String.init) ?? value
        let pieces = stable.replacingOccurrences(of: "_", with: ".").split(separator: ".", omittingEmptySubsequences: false)
        let numbers = pieces.compactMap { Int($0) }
        guard !numbers.isEmpty, numbers.count == pieces.count, numbers.allSatisfy({ $0 >= 0 }) else { return nil }
        if numbers.count > 1 && numbers[0] == 1 { return Array(numbers.dropFirst()) }
        return numbers
    }
    private static func compare(_ lhs: [Int], _ rhs: [Int]) -> ComparisonResult {
        for i in 0..<max(lhs.count, rhs.count) {
            let a = i < lhs.count ? lhs[i] : 0, b = i < rhs.count ? rhs[i] : 0
            if a != b { return a < b ? .orderedAscending : .orderedDescending }
        }
        return .orderedSame
    }
}

public enum VersionManagementError: LocalizedError {
    case invalidSettings(String)
    case invalidName
    case destinationExists
    case dependentVersion(String)
    case unsupportedRename
    case rollbackFailed(String)

    public var errorDescription: String? {
        switch self {
        case .invalidSettings(let reason): return "版本设置无效：\(reason)"
        case .invalidName: return "版本名不能为空，不能包含路径符号、控制字符、感叹号或分号，不能以点开头，也不能以空格或句点结尾。"
        case .destinationExists: return "已有同名版本，请使用其他名称。"
        case .dependentVersion(let name): return "版本 \(name) 依赖当前版本，暂时不能重命名。"
        case .unsupportedRename: return "当前版本目录或配置不适合安全重命名。"
        case .rollbackFailed(let path): return "重命名未完成，原版本文件保留在：\(path)"
        }
    }
}

public enum VersionSettingsRepository {
    public static func settingsURL(version: InstalledVersion) throws -> URL {
        let raw = version.directory.appendingPathComponent("PCL/macos-settings.json")
        guard (try? FileManager.default.destinationOfSymbolicLink(atPath: raw.path)) == nil else {
            throw VersionManagementError.invalidSettings("版本设置文件不能是符号链接")
        }
        return try MinecraftVersionMetadata.safeFile(relativePath: "PCL/macos-settings.json", under: version.directory)
    }

    public static func load(version: InstalledVersion) throws -> VersionProfileSettings {
        let file = try settingsURL(version: version)
        guard FileManager.default.fileExists(atPath: file.path) else { return VersionProfileSettings() }
        let settings = try JSONDecoder().decode(VersionProfileSettings.self, from: Data(contentsOf: file))
        try settings.validate()
        return settings
    }

    public static func save(_ settings: VersionProfileSettings, for version: InstalledVersion) throws {
        try settings.validate()
        let file = try settingsURL(version: version)
        try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true)
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        try encoder.encode(settings).write(to: file, options: .atomic)
    }

    public static func reset(version: InstalledVersion) throws {
        // Only this macOS app's settings are reset; preserve the original PCL.ini and user data.
        let file = try settingsURL(version: version)
        if FileManager.default.fileExists(atPath: file.path) { try FileManager.default.removeItem(at: file) }
    }
}

public enum VersionManager {
    public static func validateName(_ name: String) throws {
        do { try MinecraftInstaller.validateProfileID(name) }
        catch { throw VersionManagementError.invalidName }
    }

    /// Rename the profile, its JSON and its private client JAR together. Shared assets/libraries,
    /// world data, and unrelated profiles are not modified. A hidden staging directory allows
    /// rollback without copying a potentially large isolated saves directory.
    @discardableResult public static func rename(version: InstalledVersion, to newName: String, gameDirectory: URL) throws -> String {
        try validateName(newName)
        try MinecraftVersionMetadata.validateVersionID(version.id)
        if newName == version.id { return newName }
        let fm = FileManager.default
        guard (try? fm.destinationOfSymbolicLink(atPath: version.directory.path)) == nil else {
            throw VersionManagementError.unsupportedRename
        }
        let source = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(version.id)", under: gameDirectory)
        let destination = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(newName)", under: gameDirectory)
        guard source.resolvingSymlinksInPath().path == version.directory.resolvingSymlinksInPath().path,
              (try fm.attributesOfItem(atPath: source.path)[.type] as? FileAttributeType) == .typeDirectory else {
            throw VersionManagementError.unsupportedRename
        }
        let sameCaseInsensitiveName = newName.caseInsensitiveCompare(version.id) == .orderedSame
        if fm.fileExists(atPath: destination.path) {
            guard sameCaseInsensitiveName, try sameFile(source, destination) else { throw VersionManagementError.destinationExists }
        }
        let metadata = try MinecraftVersionMetadata.load(versionID: version.id, gameDirectory: gameDirectory)
        let resolved = try MinecraftVersionMetadata.resolve(versionID: version.id, gameDirectory: gameDirectory)
        let versionsRoot = gameDirectory.appendingPathComponent("versions")
        for sibling in try fm.contentsOfDirectory(at: versionsRoot, includingPropertiesForKeys: nil, options: [.skipsHiddenFiles]) where sibling.lastPathComponent != version.id {
            guard let other = try? MinecraftVersionMetadata.load(versionID: sibling.lastPathComponent, gameDirectory: gameDirectory) else { continue }
            let references = [other["inheritsFrom"] as? String, other["jar"] as? String].compactMap { $0 }
            if references.contains(where: { $0.caseInsensitiveCompare(version.id) == .orderedSame }) {
                throw VersionManagementError.dependentVersion(sibling.lastPathComponent)
            }
        }
        let oldJSON = try MinecraftVersionMetadata.safeFile(relativePath: "\(version.id).json", under: source)
        let oldData = try Data(contentsOf: oldJSON)
        let oldJarName = "\(version.id).jar", newJarName = "\(newName).jar"
        let newJSONName = "\(newName).json"
        if !sameCaseInsensitiveName {
            if fm.fileExists(atPath: source.appendingPathComponent(newJSONName).path) || fm.fileExists(atPath: source.appendingPathComponent(newJarName).path) {
                throw VersionManagementError.destinationExists
            }
        }
        var updated = metadata
        updated["pclOriginalVersion"] = metadata["pclOriginalVersion"] as? String ?? resolved["pclOriginalVersion"] as? String ?? version.id
        updated["id"] = newName
        if metadata["jar"] as? String == version.id { updated["jar"] = newName }
        let updatedData = try JSONSerialization.data(withJSONObject: updated, options: [.prettyPrinted, .sortedKeys])
        let staging = try MinecraftVersionMetadata.safeFile(relativePath: "versions/.pcl-rename-\(UUID().uuidString)", under: gameDirectory)
        try fm.moveItem(at: source, to: staging)
        var movedJar = false
        var removedOldJSON = false
        let jarTemporary = staging.appendingPathComponent(".pcl-client-\(UUID().uuidString).jar")
        do {
            // A second temporary basename also permits a case-only rename on case-insensitive APFS.
            if fm.fileExists(atPath: staging.appendingPathComponent(oldJarName).path) {
                try fm.moveItem(at: staging.appendingPathComponent(oldJarName), to: jarTemporary)
                try fm.moveItem(at: jarTemporary, to: staging.appendingPathComponent(newJarName))
                movedJar = true
            }
            try fm.removeItem(at: staging.appendingPathComponent("\(version.id).json"))
            removedOldJSON = true
            try updatedData.write(to: staging.appendingPathComponent(newJSONName), options: .atomic)
            try fm.moveItem(at: staging, to: destination)
        } catch {
            do {
                if fm.fileExists(atPath: jarTemporary.path) {
                    try fm.moveItem(at: jarTemporary, to: staging.appendingPathComponent(oldJarName))
                } else if movedJar {
                    let temporary = staging.appendingPathComponent(".pcl-rollback-\(UUID().uuidString).jar")
                    try fm.moveItem(at: staging.appendingPathComponent(newJarName), to: temporary)
                    try fm.moveItem(at: temporary, to: staging.appendingPathComponent(oldJarName))
                }
                if removedOldJSON {
                    if fm.fileExists(atPath: staging.appendingPathComponent(newJSONName).path) { try fm.removeItem(at: staging.appendingPathComponent(newJSONName)) }
                    try oldData.write(to: staging.appendingPathComponent("\(version.id).json"), options: .atomic)
                }
                try fm.moveItem(at: staging, to: source)
            } catch { throw VersionManagementError.rollbackFailed(staging.path) }
            throw error
        }
        return newName
    }

    public static func exportLaunchScript(plan: LaunchPlan, to destination: URL) throws {
        let values = [plan.executable.path, plan.workingDirectory.path] + plan.arguments
        guard !values.contains(where: { $0.contains("\0") }) else { throw LauncherError.invalidArguments("脚本参数含有空字符") }
        let command = ([plan.executable.path] + plan.arguments).map(shellQuoted).joined(separator: " ")
        let script = "#!/bin/zsh\n# Plain Craft Launcher (PCL) macOS — offline launch\ncd -- \(shellQuoted(plan.workingDirectory.path)) || exit 1\nexec \(command)\n"
        try Data(script.utf8).write(to: destination, options: .atomic)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: destination.path)
    }

    private static func shellQuoted(_ value: String) -> String { "'" + value.replacingOccurrences(of: "'", with: "'\\''") + "'" }

    private static func sameFile(_ first: URL, _ second: URL) throws -> Bool {
        let a = try FileManager.default.attributesOfItem(atPath: first.path)
        let b = try FileManager.default.attributesOfItem(atPath: second.path)
        return a[.systemFileNumber] as? NSNumber == b[.systemFileNumber] as? NSNumber
            && a[.systemNumber] as? NSNumber == b[.systemNumber] as? NSNumber
    }
}

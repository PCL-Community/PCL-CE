import Foundation

public struct InstalledVersion: Identifiable, Sendable {
    public let id: String
    public let type: String
    public let requiredJava: Int
    public let directory: URL
    public let inheritsFrom: String?
    public let modLoader: ModLoaderVersion?
    public let liteLoaderVersion: String?
    public let optiFineVersion: String?
    public let requiredJavaArchitecture: String?
    public var canInstallMods: Bool { modLoader != nil || liteLoaderVersion != nil }

    public init(id: String, type: String, requiredJava: Int, directory: URL, inheritsFrom: String? = nil, modLoader: ModLoaderVersion? = nil,
                liteLoaderVersion: String? = nil, optiFineVersion: String? = nil, requiredJavaArchitecture: String? = nil) {
        self.id = id
        self.type = type
        self.requiredJava = requiredJava
        self.directory = directory
        self.inheritsFrom = inheritsFrom
        self.modLoader = modLoader
        self.liteLoaderVersion = liteLoaderVersion; self.optiFineVersion = optiFineVersion
        self.requiredJavaArchitecture = requiredJavaArchitecture
    }
}

public struct JavaRuntime: Identifiable, Sendable, Hashable {
    public var id: String { executable.path }
    public let executable: URL
    public let majorVersion: Int
    public let architecture: String
    public let displayName: String

    public init(executable: URL, majorVersion: Int, architecture: String, displayName: String) {
        self.executable = executable
        self.majorVersion = majorVersion
        self.architecture = architecture
        self.displayName = displayName
    }
}

public struct LaunchConfiguration: Sendable {
    public var gameDirectory: URL
    public var versionID: String
    public var playerName: String
    public var javaExecutable: URL
    public var memoryMB: Int
    public var width: Int
    public var height: Int
    public var extraJVMArguments: String
    public var offlineSkinModel: OfflineSkinModel
    public var runtimeGameDirectory: URL?
    public var extraGameArguments: String
    public var serverAddress: String
    public var customInfo: String
    public var garbageCollector: String
    public var javaMajorVersion: Int?

    public init(gameDirectory: URL, versionID: String, playerName: String, javaExecutable: URL, memoryMB: Int, width: Int = 1100, height: Int = 700, extraJVMArguments: String = "", offlineSkinModel: OfflineSkinModel = .automatic, runtimeGameDirectory: URL? = nil, extraGameArguments: String = "", serverAddress: String = "", customInfo: String = "", garbageCollector: String = "global", javaMajorVersion: Int? = nil) {
        self.gameDirectory = gameDirectory
        self.versionID = versionID
        self.playerName = playerName
        self.javaExecutable = javaExecutable
        self.memoryMB = memoryMB
        self.width = width
        self.height = height
        self.extraJVMArguments = extraJVMArguments
        self.offlineSkinModel = offlineSkinModel
        self.runtimeGameDirectory = runtimeGameDirectory
        self.extraGameArguments = extraGameArguments
        self.serverAddress = serverAddress
        self.customInfo = customInfo
        self.garbageCollector = garbageCollector
        self.javaMajorVersion = javaMajorVersion
    }
}

public enum LauncherError: LocalizedError {
    case invalidVersion(String)
    case missingFile(String)
    case unsupportedVersion(String)
    case invalidPlayerName
    case invalidJava(String)
    case invalidArguments(String)
    case alreadyRunning
    case extractionFailed(String)

    public var errorDescription: String? {
        switch self {
        case .invalidVersion(let reason): return "版本配置无效：\(reason)"
        case .missingFile(let path): return "缺少游戏文件，请重新安装或修复该版本：\(path)"
        case .unsupportedVersion(let reason): return "暂不支持此版本：\(reason)"
        case .invalidPlayerName: return "玩家名无效，请检查用户名。"
        case .invalidJava(let reason): return "Java 环境不可用：\(reason)"
        case .invalidArguments(let reason): return "启动参数无效：\(reason)"
        case .alreadyRunning: return "已有游戏正在运行，请先结束游戏。"
        case .extractionFailed(let reason): return "原生库解压失败：\(reason)"
        }
    }
}

public enum MinecraftVersionMetadata {
    public static func requiredJava(_ metadata: [String: Any]) -> Int {
        if let java = metadata["javaVersion"] as? [String: Any], let major = java["majorVersion"] as? Int { return major }
        let version = metadata["pclOriginalVersion"] as? String ?? metadata["inheritsFrom"] as? String ?? metadata["id"] as? String ?? ""
        let components = version.split(separator: ".").compactMap { Int($0) }
        if components.count >= 2, components[0] == 1 {
            if components[1] >= 21 { return 21 }
            if components[1] == 20, components.count > 2, components[2] >= 5 { return 21 }
            if components[1] >= 18 { return 17 }
            if components[1] == 17 { return 16 }
        }
        return 8
    }

    public static func validateVersionID(_ id: String) throws {
        guard !id.isEmpty, id != ".", id != "..", !id.contains("/"), !id.contains("\\"), !id.contains(":"), !id.contains("\0") else {
            throw LauncherError.invalidVersion("版本名称含有非法路径字符")
        }
    }

    public static func load(versionID: String, gameDirectory: URL) throws -> [String: Any] {
        try validateVersionID(versionID)
        let file = try safeFile(relativePath: "versions/\(versionID)/\(versionID).json", under: gameDirectory)
        guard FileManager.default.fileExists(atPath: file.path) else { throw LauncherError.missingFile(file.lastPathComponent) }
        let data = try Data(contentsOf: file)
        guard let metadata = try JSONSerialization.jsonObject(with: data) as? [String: Any] else { throw LauncherError.invalidVersion("JSON 格式错误") }
        return metadata
    }

    public static func libraryPath(_ library: [String: Any]) -> String? {
        if let downloads = library["downloads"] as? [String: Any], let artifact = downloads["artifact"] as? [String: Any], let path = artifact["path"] as? String { return path }
        guard let name = library["name"] as? String, !name.isEmpty else { return nil }
        let coordinate = name.split(separator: "@", maxSplits: 1, omittingEmptySubsequences: false).map(String.init)
        let coords = coordinate[0].split(separator: ":", omittingEmptySubsequences: false).map(String.init)
        guard (3...4).contains(coords.count), !coords.contains(where: \.isEmpty) else { return nil }
        let version = coords[2]
        let ext = coordinate.count > 1 ? coordinate[1] : "jar"
        guard !ext.isEmpty, ext.range(of: #"^[A-Za-z0-9._-]+$"#, options: .regularExpression) != nil else { return nil }
        let classifier = coords.count > 3 ? "-\(coords[3])" : ""
        return "\(coords[0].replacingOccurrences(of: ".", with: "/"))/\(coords[1])/\(version)/\(coords[1])-\(version)\(classifier).\(ext)"
    }

    public static func safeFile(relativePath: String, under root: URL) throws -> URL {
        let components = relativePath.split(separator: "/", omittingEmptySubsequences: false)
        guard !relativePath.hasPrefix("/"), !relativePath.contains("\\"), !relativePath.contains("\0"),
              !components.contains(where: { $0.isEmpty || $0 == "." || $0 == ".." }) else { throw LauncherError.invalidVersion("非法文件路径") }
        let normalizedRoot = try canonicalPath(root)
        let result = try canonicalPath(normalizedRoot.appendingPathComponent(relativePath))
        guard result.path.hasPrefix(normalizedRoot.path + "/") else { throw LauncherError.invalidVersion("文件路径越界") }
        return result
    }

    // URL.resolvingSymlinksInPath may leave symlink parents unresolved when a final leaf does
    // not exist. Walk each component so a future output file cannot escape via a parent link.
    private static func canonicalPath(_ url: URL, remainingLinks: Int = 40) throws -> URL {
        var resolved = URL(fileURLWithPath: "/", isDirectory: true)
        // Do not call standardizedFileURL here: Foundation rewrites /private/var back to
        // /var, which is itself a symlink and would make recursive resolution loop.
        for component in url.pathComponents.dropFirst() {
            if component == "." { continue }
            if component == ".." { resolved.deleteLastPathComponent(); continue }
            let candidate = resolved.appendingPathComponent(component)
            if let target = try? FileManager.default.destinationOfSymbolicLink(atPath: candidate.path) {
                guard remainingLinks > 0 else { throw LauncherError.invalidVersion("符号链接层数过多") }
                let targetURL = target.hasPrefix("/") ? URL(fileURLWithPath: target) : resolved.appendingPathComponent(target)
                resolved = try canonicalPath(targetURL, remainingLinks: remainingLinks - 1)
            } else { resolved = candidate }
        }
        return resolved
    }
}

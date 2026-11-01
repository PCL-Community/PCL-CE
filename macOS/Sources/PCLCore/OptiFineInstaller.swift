import CryptoKit
import Darwin
import Foundation

public struct OptiFineVersion: Identifiable, Codable, Hashable, Sendable {
    public var id: String { "optifine:\(minecraftVersion):\(version)" }
    public let minecraftVersion: String
    public let version: String
    public let filename: String
    public let requiredForgeVersion: String?
    public let preview: Bool
    public var displayName: String { version.replacingOccurrences(of: "HD_U_", with: "").replacingOccurrences(of: "_", with: " ") }
    public init(minecraftVersion: String, version: String, filename: String? = nil, requiredForgeVersion: String? = "", preview: Bool = false) {
        self.minecraftVersion = minecraftVersion; self.version = version
        self.filename = filename ?? ((preview ? "preview_" : "") + "OptiFine_\(minecraftVersion)_\(version).jar")
        self.requiredForgeVersion = requiredForgeVersion; self.preview = preview
    }
}

/// OptiFine remains an independent component: standalone LaunchWrapper, or a
/// verified Mod alongside Forge / Fabric + OptiFabric. Only prepared profiles
/// are changed; publishing and rollback belong to the enclosing transaction.
public actor OptiFineInstaller {
    private let session: URLSession
    private var applying = false
    public init(session: URLSession = .shared) {
        let configuration = session.configuration
        configuration.urlCredentialStorage = nil; configuration.httpCookieStorage = nil; configuration.httpShouldSetCookies = false
        configuration.httpAdditionalHeaders = configuration.httpAdditionalHeaders?.filter {
            !["authorization", "proxy-authorization", "cookie", "x-api-key"].contains(String(describing: $0.key).lowercased())
        }
        self.session = URLSession(configuration: configuration)
    }
    deinit { session.invalidateAndCancel() }

    public nonisolated static func detect(metadata: [String: Any], modsDirectory: URL? = nil) throws -> OptiFineVersion? {
        var recorded: OptiFineVersion?
        if let marker = metadata["pclOptiFine"] {
            let value = try JSONDecoder().decode(OptiFineVersion.self, from: JSONSerialization.data(withJSONObject: marker))
            try validate(value)
            recorded = value
        }
        for library in metadata["libraries"] as? [[String: Any]] ?? [] {
            guard let name = library["name"] as? String, name.hasPrefix("optifine:OptiFine:") else { continue }
            let coordinate = String(name.dropFirst("optifine:OptiFine:".count))
            if let value = versionFromIdentity("OptiFine_" + coordinate) {
                return recorded?.minecraftVersion == value.minecraftVersion && recorded?.version == value.version ? recorded : value
            }
        }
        guard let modsDirectory else { return recorded }
        guard FileManager.default.fileExists(atPath: modsDirectory.path) else { return nil }
        var found: [OptiFineVersion] = []
        for file in try FileManager.default.contentsOfDirectory(at: modsDirectory, includingPropertiesForKeys: [.isRegularFileKey, .isSymbolicLinkKey])
            where file.pathExtension.lowercased() == "jar" {
            guard let archive = try? ModpackZIPArchive(url: file),
                  archive.member("optifine/OptiFineTweaker.class") != nil || archive.member("optifine/OptiFineForgeTweaker.class") != nil,
                  let entry = ["notch/net/optifine/Config.class", "srg/net/optifine/Config.class", "net/optifine/Config.class", "Config.class"].compactMap({ archive.member($0) }).first,
                  let bytes = try? archive.data(for: entry, maximumBytes: 4 * 1024 * 1024) else { continue }
            let text = String(decoding: bytes, as: UTF8.self)
            guard let range = text.range(of: #"OptiFine_[A-Za-z0-9_.]+"#, options: .regularExpression),
                  let value = versionFromIdentity(String(text[range])) else { continue }
            found.append(recorded?.minecraftVersion == value.minecraftVersion && recorded?.version == value.version ? recorded! : value)
        }
        guard found.count <= 1 else { throw MinecraftInstallError.invalidMetadata("发现多个 OptiFine 文件，请先在 Mod 管理中保留一个启用版本") }
        return found.first
    }

    private nonisolated static func versionFromIdentity(_ identity: String) -> OptiFineVersion? {
        let value = identity.replacingOccurrences(of: "OptiFine_", with: "")
        guard let separator = value.firstIndex(of: "_") else { return nil }
        let mc = normalizedMinecraft(String(value[..<separator])), edition = String(value[value.index(after: separator)...])
        guard !mc.isEmpty, !edition.isEmpty else { return nil }
        return OptiFineVersion(minecraftVersion: mc, version: edition, preview: edition.lowercased().contains("pre"))
    }

    public func fetchVersions(minecraftVersion: String) async throws -> [OptiFineVersion] {
        try MinecraftInstaller.validateComponent(minecraftVersion)
        do {
            let mc = Self.remoteMinecraft(minecraftVersion)
            let data = try await MinecraftInstaller.fetchData(url: URL(string: "https://bmclapi2.bangbang93.com/optifine/\(mc)")!, session: session)
            return try Self.parseMirror(data, minecraftVersion: minecraftVersion)
        } catch {
            try Task.checkCancellation()
            let data = try await MinecraftInstaller.fetchData(url: URL(string: "https://optifine.net/downloads")!, session: session)
            return try Self.parseOfficial(data, minecraftVersion: minecraftVersion)
        }
    }

    public func prepare(version: OptiFineVersion, gameDirectory: URL, javaExecutable: URL,
                        progress: @escaping @Sendable (InstallProgress) -> Void) async throws -> URL {
        try Self.validate(version)
        let root = gameDirectory.resolvingSymlinksInPath().standardizedFileURL
        let folder = try Self.safe("PCL/installers/optifine/\(version.minecraftVersion)/\(version.version)", root: root)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let target = try Self.safe(version.filename, root: folder)
        let digestFile = try Self.safe(version.filename + ".sha256", root: folder)
        if let digest = try? String(contentsOf: digestFile, encoding: .utf8),
           let data = try? Self.regularData(target), Self.sha256(data) == digest.trimmingCharacters(in: .whitespacesAndNewlines),
           (try? Self.validateArchive(target, version: version)) != nil {
            progress(.init(message: "复用 OptiFine \(version.displayName)", completed: 1, total: 1))
            return target
        }
        progress(.init(message: "获取 OptiFine 下载地址", completed: 0, total: 100))
        var errors: [String] = []
        // The same official BMCLAPI route used by the upstream launcher. The
        // mirror's redirect normally advertises the exact SHA-1 in its path.
        let (type, patch) = try Self.remoteParts(version)
        let mirror = URL(string: "https://bmclapi2.bangbang93.com/optifine/\(Self.remoteMinecraft(version.minecraftVersion))/\(type)/\(patch)")!
        for source in 0..<2 {
            let url: URL
            if source == 0 { url = mirror }
            else {
                // Resolve the official link only after a verified mirror attempt
                // fails. A slow official page must not delay a working mirror.
                do {
                    var components = URLComponents(string: "https://optifine.net/adloadx")!
                    components.queryItems = [.init(name: "f", value: version.filename)]
                    let html = String(decoding: try await MinecraftInstaller.fetchData(url: components.url!, session: session), as: UTF8.self)
                    guard let match = html.range(of: #"downloadx\?f=[^\"'<>\s]+"#, options: .regularExpression),
                          let official = URL(string: "https://optifine.net/" + html[match].replacingOccurrences(of: "&amp;", with: "&")) else {
                        throw ModLoaderInstallError.unavailable("未找到 OptiFine 官方下载地址")
                    }
                    url = official
                } catch { try Task.checkCancellation(); errors.append(error.localizedDescription); continue }
            }
            do {
                var request = URLRequest(url: url); request.timeoutInterval = 90
                let (temporary, response) = try await PCLHTTPS.download(for: request, session: session)
                defer { try? FileManager.default.removeItem(at: temporary) }
                guard let http = response as? HTTPURLResponse, (200..<300).contains(http.statusCode), http.url?.scheme == "https" else {
                    throw MinecraftInstallError.http((response as? HTTPURLResponse)?.statusCode ?? 0, version.filename)
                }
                let data = try Self.regularData(temporary)
                guard data.count >= 300 * 1024, data.count <= 128 * 1024 * 1024 else { throw MinecraftInstallError.integrity(version.filename) }
                if let advertised = http.url?.lastPathComponent, MinecraftInstaller.validSHA1(advertised),
                   MinecraftInstaller.hash(data) != advertised.lowercased() { throw MinecraftInstallError.integrity(version.filename) }
                try Self.validateArchive(temporary, version: version)
                try Task.checkCancellation()
                try Self.replace(data, at: target)
                try Self.replace(Data((Self.sha256(data) + "\n").utf8), at: digestFile)
                progress(.init(message: "OptiFine 下载完成", completed: 100, total: 100))
                return target
            } catch { try Task.checkCancellation(); errors.append(error.localizedDescription) }
        }
        throw ModLoaderInstallError.unavailable("无法获取 OptiFine \(version.displayName)：\n" + errors.joined(separator: "\n"))
    }

    public func apply(version: OptiFineVersion, toProfile profileID: String, gameDirectory: URL, javaExecutable: URL,
                      asMod: Bool, progress: @escaping @Sendable (InstallProgress) -> Void) async throws {
        guard !applying else { throw MinecraftInstallError.alreadyInstalling }
        applying = true; defer { applying = false }
        try MinecraftInstaller.validateProfileID(profileID)
        let root = gameDirectory.resolvingSymlinksInPath().standardizedFileURL
        let json = try Self.safe("versions/\(profileID)/\(profileID).json", root: root)
        let original = try Self.regularData(json)
        var profile = try MinecraftVersionMetadata.resolve(versionID: profileID, gameDirectory: root)
        guard (profile["pclOriginalVersion"] as? String ?? profile["id"] as? String) == version.minecraftVersion else {
            throw MinecraftInstallError.invalidMetadata("OptiFine 与准备安装的 Minecraft 版本不匹配")
        }
        let jar = try await prepare(version: version, gameDirectory: root, javaExecutable: javaExecutable, progress: progress)
        try Task.checkCancellation()
        if asMod {
            let folder = try Self.safe("versions/\(profileID)/mods", root: root)
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            let destination = try Self.safe(version.filename, root: folder)
            if FileManager.default.fileExists(atPath: destination.path) {
                guard try Self.regularData(destination) == Self.regularData(jar) else { throw CommunityInstallError.collision(destination.lastPathComponent) }
            } else { try ModpackImporter.cloneOrCopy(jar, to: destination) }
        } else if Self.modern(version.minecraftVersion) {
            profile = try await modernProfile(version: version, jar: jar, profile: profile, root: root, java: javaExecutable, progress: progress)
        } else {
            profile = try await legacyProfile(version: version, jar: jar, profile: profile, root: root, progress: progress)
        }
        profile["id"] = profileID
        profile["jar"] = (try MinecraftVersionMetadata.resolve(versionID: profileID, gameDirectory: root))["jar"] ?? profileID
        profile["pclOriginalVersion"] = version.minecraftVersion
        profile["pclOptiFine"] = try JSONSerialization.jsonObject(with: JSONEncoder().encode(version))
        profile.removeValue(forKey: "inheritsFrom")
        try Task.checkCancellation()
        guard try Self.regularData(json) == original else { throw MinecraftInstallError.invalidMetadata("临时版本配置已改变，请重新安装") }
        try Self.replace(try JSONSerialization.data(withJSONObject: profile, options: [.prettyPrinted, .sortedKeys]), at: json)
        progress(.init(message: "OptiFine \(version.displayName) 安装完成", completed: 100, total: 100))
    }

    private func modernProfile(version: OptiFineVersion, jar: URL, profile: [String: Any], root: URL, java: URL,
                               progress: @escaping @Sendable (InstallProgress) -> Void) async throws -> [String: Any] {
        let stage = try Self.safe("PCL/installers/.optifine-stage-\(UUID().uuidString)", root: root)
        try FileManager.default.createDirectory(at: stage, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: stage) }
        let mc = version.minecraftVersion
        let baseFolder = stage.appendingPathComponent("versions").appendingPathComponent(mc)
        try FileManager.default.createDirectory(at: baseFolder, withIntermediateDirectories: true)
        var base = profile; base["id"] = mc; base["jar"] = mc; base.removeValue(forKey: "inheritsFrom")
        try JSONSerialization.data(withJSONObject: base, options: [.prettyPrinted, .sortedKeys]).write(to: baseFolder.appendingPathComponent(mc + ".json"))
        let clientID = profile["jar"] as? String ?? (profile["id"] as? String ?? mc)
        let client = try Self.safe("versions/\(clientID)/\(clientID).jar", root: root)
        try ModpackImporter.cloneOrCopy(client, to: baseFolder.appendingPathComponent(mc + ".jar"))
        try Data("{\"profiles\":{}}".utf8).write(to: stage.appendingPathComponent("launcher_profiles.json"))
        let helper = stage.appendingPathComponent("PCLOptiFineInstaller.class")
        try Data(base64Encoded: Self.helperClass)!.write(to: helper)
        let classVersion = try Self.installerJavaMajor(jar)
        guard let runtime = JavaDiscovery.inspect(executable: java), runtime.majorVersion >= classVersion else {
            throw ModLoaderInstallError.invalidJava("此 OptiFine 安装器需要 Java \(classVersion) 或更新版本")
        }
        progress(.init(message: "运行 OptiFine 安装器", completed: 45, total: 100))
        let result = try await OptiFineProcess.run(executable: java,
            arguments: ["-Djava.awt.headless=true", "-cp", jar.path + ":" + stage.path, "PCLOptiFineInstaller", stage.path],
            directory: stage, log: stage.appendingPathComponent("installer.log"))
        guard result.status == 0 else { throw ModLoaderInstallError.installerFailed(String(result.output.suffix(6000))) }
        let generatedID = mc + "-OptiFine_" + version.version
        let generated = try Self.safe("versions/\(generatedID)/\(generatedID).json", root: stage)
        guard let generatedProfile = try JSONSerialization.jsonObject(with: Self.regularData(generated)) as? [String: Any],
              generatedProfile["mainClass"] as? String != nil else { throw ModLoaderInstallError.installerFailed("OptiFine 安装器未生成客户端配置") }
        // Current installers emit an inherited child containing only the two
        // OptiFine libraries and tweak arguments. Flatten the actual parent
        // before removing inheritsFrom; otherwise vanilla dependencies vanish.
        var resultProfile = try Self.mergeGeneratedProfile(base: profile, layer: generatedProfile)
        try Self.copyGeneratedLibraries(from: stage, to: root)
        for (key, value) in profile where key.hasPrefix("pcl") { resultProfile[key] = value }
        return resultProfile
    }

    static func mergeGeneratedProfile(base: [String: Any], layer: [String: Any]) throws -> [String: Any] {
        var result = base.merging(layer) { _, child in child }
        let additions = layer["libraries"] as? [[String: Any]] ?? []
        let inherited = base["libraries"] as? [[String: Any]] ?? []
        let overrides = Set(additions.filter { InstallPlatform.libraryAllowed($0) }.compactMap(libraryIdentity))
        result["libraries"] = additions + inherited.filter { library in
            guard let identity = libraryIdentity(library) else { return true }
            return !overrides.contains(identity)
        }
        var arguments = base["arguments"] as? [String: Any] ?? [:]
        if arguments["game"] == nil, let legacy = base["minecraftArguments"] as? String {
            arguments["game"] = try MinecraftArguments.tokenize(legacy)
        }
        let childArguments = layer["arguments"] as? [String: Any] ?? [:]
        for (name, value) in childArguments {
            if let parent = arguments[name] as? [Any], let child = value as? [Any] {
                // Some historical installers emit an already-complete array.
                let includesParent = child.count >= parent.count && NSArray(array: Array(child.prefix(parent.count))).isEqual(to: parent)
                arguments[name] = includesParent ? child : parent + child
            } else { arguments[name] = value }
        }
        if layer["minecraftArguments"] is String, childArguments["game"] == nil { arguments.removeValue(forKey: "game") }
        if !arguments.isEmpty { result["arguments"] = arguments }
        result["id"] = base["id"]
        result["jar"] = base["jar"] ?? base["id"]
        result.removeValue(forKey: "inheritsFrom")
        return result
    }

    private static func libraryIdentity(_ library: [String: Any]) -> String? {
        guard let coordinate = library["name"] as? String else { return nil }
        let parts = coordinate.split(separator: "@", maxSplits: 1, omittingEmptySubsequences: false)
        let fields = parts[0].split(separator: ":", omittingEmptySubsequences: false)
        guard fields.count == 3 || fields.count == 4 else { return nil }
        return "\(fields[0]):\(fields[1]):\(fields.count == 4 ? String(fields[3]) : "")@\(parts.count > 1 ? String(parts[1]) : "jar")"
    }

    private static func copyGeneratedLibraries(from stage: URL, to root: URL) throws {
        let libs = stage.appendingPathComponent("libraries")
        if let enumerator = FileManager.default.enumerator(at: libs, includingPropertiesForKeys: [.isRegularFileKey, .isSymbolicLinkKey]) {
            for case let file as URL in enumerator {
                let values = try file.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey])
                guard values.isSymbolicLink != true else { throw MinecraftInstallError.unsafePath(file.path) }
                guard values.isRegularFile == true else { continue }
                let relative = String(file.path.dropFirst(libs.path.count + 1))
                let destination = try Self.safe("libraries/" + relative, root: root)
                try Self.replace(try Self.regularData(file), at: destination)
            }
        }
    }

    private func legacyProfile(version: OptiFineVersion, jar: URL, profile: [String: Any], root: URL,
                               progress: @escaping @Sendable (InstallProgress) -> Void) async throws -> [String: Any] {
        var profile = profile
        let coordinate = "optifine:OptiFine:\(version.minecraftVersion)_\(version.version)"
        let lib: [String: Any] = ["name": coordinate]
        let path = MinecraftVersionMetadata.libraryPath(lib)!
        let destination = try Self.safe("libraries/" + path, root: root)
        try Self.replace(try Self.regularData(jar), at: destination)
        let wrapperPath = "net/minecraft/launchwrapper/1.12/launchwrapper-1.12.jar"
        let wrapperURL = URL(string: "https://libraries.minecraft.net/" + wrapperPath)!
        let hashData = try await MinecraftInstaller.fetchData(url: wrapperURL.appendingPathExtension("sha1"), session: session)
        guard let hash = String(data: hashData, encoding: .utf8)?.split(whereSeparator: \.isWhitespace).first.map(String.init),
              MinecraftInstaller.validSHA1(hash) else { throw MinecraftInstallError.invalidMetadata("LaunchWrapper 缺少校验值") }
        let wrapperDestination = try Self.safe("libraries/" + wrapperPath, root: root)
        try await MinecraftInstaller.download(.init(url: wrapperURL, destination: wrapperDestination, sha1: hash, size: nil), session: session)
        let wrapper: [String: Any] = ["name": "net.minecraft:launchwrapper:1.12"]
        var libraries = profile["libraries"] as? [[String: Any]] ?? []
        libraries.removeAll { ($0["name"] as? String)?.hasPrefix("optifine:") == true || ($0["name"] as? String)?.hasPrefix("net.minecraft:launchwrapper:") == true }
        profile["libraries"] = [lib, wrapper] + libraries
        try Self.appendLegacyTweaker(to: &profile)
        profile["mainClass"] = "net.minecraft.launchwrapper.Launch"
        progress(.init(message: "安装 OptiFine 启动组件", completed: 90, total: 100))
        return profile
    }

    /// Upstream ModLaunch places OptiFineForgeTweaker last for a Forge/Lite
    /// combination. The plain tweaker would inject vanilla options twice.
    static func appendLegacyTweaker(to profile: inout [String: Any]) throws {
        let plain = "optifine.OptiFineTweaker", combined = "optifine.OptiFineForgeTweaker"
        let arguments = profile["arguments"] as? [String: Any]
        let modernGame = arguments?["game"] as? [Any]
        let legacy = try (profile["minecraftArguments"] as? String).map { try MinecraftArguments.tokenize($0) }
        let strings = modernGame?.compactMap { $0 as? String } ?? legacy ?? []
        let libraries = profile["libraries"] as? [[String: Any]] ?? []
        let hasLiteMarker = profile["pclLiteLoader"].map { !($0 is NSNull) } ?? false
        let hasOtherTweaker = strings.contains { $0.contains("LiteLoaderTweaker") || $0.contains("FMLTweaker") }
        let hasOtherLibrary = libraries.contains {
            let name = $0["name"] as? String ?? ""
            return name.hasPrefix("com.mumfrey:liteloader:") || name.hasPrefix("net.minecraftforge:forge:") || name.hasPrefix("net.minecraftforge:minecraftforge:")
        }
        let useCombined = hasLiteMarker || hasOtherTweaker || hasOtherLibrary || MinecraftVersionMetadata.modLoader(in: profile)?.kind == .forge
        let tweaker = useCombined ? combined : plain
        func cleaned(_ game: [Any]) -> [Any] {
            var result: [Any] = [], index = 0
            while index < game.count {
                if game[index] as? String == "--tweakClass", index + 1 < game.count,
                   let value = game[index + 1] as? String, value == plain || value == combined { index += 2; continue }
                if let value = game[index] as? String, value == "--tweakClass=" + plain || value == "--tweakClass=" + combined { index += 1; continue }
                result.append(game[index]); index += 1
            }
            return result + ["--tweakClass", tweaker]
        }
        if let game = modernGame {
            var arguments = arguments ?? [:]; arguments["game"] = cleaned(game); profile["arguments"] = arguments
        } else {
            func quoted(_ token: String) -> String {
                if !token.isEmpty, !token.contains(where: { $0.isWhitespace || $0 == "\"" || $0 == "'" || $0 == "\\" }) { return token }
                return "\"" + token.replacingOccurrences(of: "\\", with: "\\\\").replacingOccurrences(of: "\"", with: "\\\"") + "\""
            }
            profile["minecraftArguments"] = cleaned(legacy ?? []).compactMap { $0 as? String }.map(quoted).joined(separator: " ")
        }
    }

    static func parseMirror(_ data: Data, minecraftVersion: String) throws -> [OptiFineVersion] {
        guard let rows = try JSONSerialization.jsonObject(with: data) as? [[String: Any]] else { throw MinecraftInstallError.invalidMetadata("OptiFine 版本列表无效") }
        var seen = Set<String>()
        return try rows.compactMap { row -> OptiFineVersion? in
            guard let mc = row["mcversion"] as? String, Self.normalizedMinecraft(mc) == minecraftVersion,
                  let type = row["type"] as? String, let patch = row["patch"] as? String,
                  let filename = row["filename"] as? String else { return nil }
            let required = (row["forge"] as? String ?? "").replacingOccurrences(of: "Forge ", with: "").replacingOccurrences(of: "#", with: "")
            let value = OptiFineVersion(minecraftVersion: minecraftVersion, version: type + "_" + patch, filename: filename,
                requiredForgeVersion: required.contains("N/A") ? nil : required, preview: patch.lowercased().contains("pre"))
            try validate(value)
            return seen.insert(value.id).inserted ? value : nil
        }.sorted { a, b in
            if a.preview != b.preview { return !a.preview }
            return a.version.compare(b.version, options: .numeric) == .orderedDescending
        }
    }

    static func parseOfficial(_ data: Data, minecraftVersion: String) throws -> [OptiFineVersion] {
        let text = String(decoding: data, as: UTF8.self)
        let regex = try NSRegularExpression(pattern: #"(?s)<tr\b[^>]*>(.*?)</tr>"#)
        var result: [OptiFineVersion] = [], seen = Set<String>()
        for match in regex.matches(in: text, range: NSRange(text.startIndex..., in: text)) {
            let row = (text as NSString).substring(with: match.range(at: 1))
            guard let fileRange = row.range(of: #"(?:preview_)?OptiFine_[A-Za-z0-9_.]+\.jar"#, options: .regularExpression) else { continue }
            let filename = String(row[fileRange])
            let stripped = filename.replacingOccurrences(of: "preview_", with: "").replacingOccurrences(of: "OptiFine_", with: "").replacingOccurrences(of: ".jar", with: "")
            guard let underscore = stripped.firstIndex(of: "_"), normalizedMinecraft(String(stripped[..<underscore])) == minecraftVersion else { continue }
            let edition = String(stripped[stripped.index(after: underscore)...])
            let forgeRange = row.range(of: #"colForge['\"][^>]*>[^<]*"#, options: .regularExpression)
            let forge = forgeRange.map { String(row[$0]).split(separator: ">").dropFirst().joined(separator: ">").trimmingCharacters(in: .whitespacesAndNewlines) } ?? ""
            let required = forge.replacingOccurrences(of: "Forge ", with: "").replacingOccurrences(of: "#", with: "")
            let version = OptiFineVersion(minecraftVersion: minecraftVersion, version: edition, filename: filename,
                requiredForgeVersion: required.contains("N/A") ? nil : required, preview: filename.hasPrefix("preview_"))
            try validate(version)
            if seen.insert(version.id).inserted { result.append(version) }
        }
        return result.sorted { a, b in a.preview != b.preview ? !a.preview : a.version.compare(b.version, options: .numeric) == .orderedDescending }
    }

    static func validate(_ version: OptiFineVersion) throws {
        try MinecraftInstaller.validateComponent(version.minecraftVersion); try MinecraftInstaller.validateComponent(version.version)
        guard version.filename == (version.preview ? "preview_" : "") + "OptiFine_\(remoteMinecraft(version.minecraftVersion))_\(version.version).jar"
            || version.filename == (version.preview ? "preview_" : "") + "OptiFine_\(version.minecraftVersion)_\(version.version).jar" else {
            throw MinecraftInstallError.invalidMetadata("OptiFine 文件名与组件身份不一致")
        }
    }
    static func validateArchive(_ url: URL, version: OptiFineVersion) throws {
        let archive = try ModpackZIPArchive(url: url)
        guard archive.member("optifine/OptiFineTweaker.class") != nil || archive.member("optifine/OptiFineForgeTweaker.class") != nil,
              let config = ["notch/net/optifine/Config.class", "srg/net/optifine/Config.class", "net/optifine/Config.class", "Config.class"].compactMap({ archive.member($0) }).first else {
            throw MinecraftInstallError.integrity(version.filename)
        }
        let bytes = try archive.data(for: config, maximumBytes: 4 * 1024 * 1024)
        guard bytes.range(of: Data("OptiFine_\(remoteMinecraft(version.minecraftVersion))_\(version.version)".utf8)) != nil
            || bytes.range(of: Data("OptiFine_\(version.minecraftVersion)_\(version.version)".utf8)) != nil else { throw MinecraftInstallError.integrity(version.filename) }
    }
    private static func installerJavaMajor(_ url: URL) throws -> Int {
        let archive = try ModpackZIPArchive(url: url)
        guard let entry = archive.member("optifine/Installer.class") else { throw MinecraftInstallError.integrity(url.lastPathComponent) }
        let bytes = try archive.data(for: entry)
        guard bytes.count >= 8, Array(bytes.prefix(4)) == [0xca, 0xfe, 0xba, 0xbe] else { throw MinecraftInstallError.integrity("Installer.class") }
        return max(8, Int(bytes[6]) * 256 + Int(bytes[7]) - 44)
    }
    private static func modern(_ mc: String) -> Bool { let parts = mc.split(separator: "."); return parts.count >= 2 && (Int(parts[0]) ?? 0 > 1 || Int(parts[1]) ?? 0 >= 14) }
    private static func normalizedMinecraft(_ mc: String) -> String { mc == "1.8.0" ? "1.8" : mc == "1.9.0" ? "1.9" : mc }
    private static func remoteMinecraft(_ mc: String) -> String { mc == "1.8" || mc == "1.9" ? mc + ".0" : mc }
    private static func remoteParts(_ version: OptiFineVersion) throws -> (String, String) {
        guard let separator = version.version.lastIndex(of: "_") else { throw MinecraftInstallError.invalidMetadata("OptiFine 版本格式错误") }
        return (String(version.version[..<separator]), String(version.version[version.version.index(after: separator)...]))
    }
    private static func safe(_ path: String, root: URL) throws -> URL {
        let canonicalRoot = try MinecraftVersionMetadata.safeFile(relativePath: ".pcl-optifine-path-probe", under: root).deletingLastPathComponent()
        var raw = canonicalRoot
        for component in path.split(separator: "/") {
            raw.appendPathComponent(String(component))
            guard (try? FileManager.default.destinationOfSymbolicLink(atPath: raw.path)) == nil else { throw MinecraftInstallError.unsafePath(path) }
        }
        let safe = try MinecraftVersionMetadata.safeFile(relativePath: path, under: canonicalRoot)
        guard raw.path == safe.path else { throw MinecraftInstallError.unsafePath(path) }
        return safe
    }
    private static func regularData(_ url: URL) throws -> Data {
        let values = try url.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey, .linkCountKey, .fileSizeKey])
        guard values.isRegularFile == true, values.isSymbolicLink != true, values.linkCount == 1,
              (values.fileSize ?? 0) <= 128 * 1024 * 1024 else { throw MinecraftInstallError.unsafePath(url.path) }
        return try Data(contentsOf: url)
    }
    private static func sha256(_ data: Data) -> String { SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined() }
    private static func replace(_ data: Data, at destination: URL) throws {
        if FileManager.default.fileExists(atPath: destination.path) { _ = try regularData(destination) }
        try FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
        try data.write(to: destination, options: .atomic)
    }
    private static let helperClass = "yv66vgAAADQAPQoAAgADBwAEDAAFAAYBABBqYXZhL2xhbmcvT2JqZWN0AQAGPGluaXQ+AQADKClWCAAIAQASb3B0aWZpbmUuSW5zdGFsbGVyCgAKAAsHAAwMAA0ADgEAD2phdmEvbGFuZy9DbGFzcwEAB2Zvck5hbWUBACUoTGphdmEvbGFuZy9TdHJpbmc7KUxqYXZhL2xhbmcvQ2xhc3M7CAAQAQAJZG9JbnN0YWxsBwASAQAMamF2YS9pby9GaWxlCgAKABQMABUAFgEACWdldE1ldGhvZAEAQChMamF2YS9sYW5nL1N0cmluZztbTGphdmEvbGFuZy9DbGFzczspTGphdmEvbGFuZy9yZWZsZWN0L01ldGhvZDsKABEAGAwABQAZAQAVKExqYXZhL2xhbmcvU3RyaW5nOylWCgAbABwHAB0MAB4AHwEAGGphdmEvbGFuZy9yZWZsZWN0L01ldGhvZAEABmludm9rZQEAOShMamF2YS9sYW5nL09iamVjdDtbTGphdmEvbGFuZy9PYmplY3Q7KUxqYXZhL2xhbmcvT2JqZWN0OwcAIQEAK2phdmEvbGFuZy9yZWZsZWN0L0ludm9jYXRpb25UYXJnZXRFeGNlcHRpb24KACAAIwwAJAAlAQAIZ2V0Q2F1c2UBABcoKUxqYXZhL2xhbmcvVGhyb3dhYmxlOwoAJwAoBwApDAAqAAYBABNqYXZhL2xhbmcvVGhyb3dhYmxlAQAPcHJpbnRTdGFja1RyYWNlCgAsAC0HAC4MAC8AMAEAEGphdmEvbGFuZy9TeXN0ZW0BAARleGl0AQAEKEkpVgcAMgEAFFBDTE9wdGlGaW5lSW5zdGFsbGVyAQAEQ29kZQEAD0xpbmVOdW1iZXJUYWJsZQEABG1haW4BABYoW0xqYXZhL2xhbmcvU3RyaW5nOylWAQANU3RhY2tNYXBUYWJsZQEACkV4Y2VwdGlvbnMHADoBABNqYXZhL2xhbmcvRXhjZXB0aW9uAQAKU291cmNlRmlsZQEAGVBDTE9wdGlGaW5lSW5zdGFsbGVyLmphdmEAMQAxAAIAAAAAAAIAAQAFAAYAAQAzAAAAHQABAAEAAAAFKrcAAbEAAAABADQAAAAGAAEAAAADAAkANQA2AAIAMwAAAHAACQADAAAAOxIHuAAJEg8EvQAKWQMSEVO2ABMBBL0AAlkDuwARWSoDMrcAF1O2ABpXpwARTCu2ACJNLLYAJgS4ACuxAAEAAAApACwAIAACADQAAAAOAAMAAAAFACkABgA6AAcANwAAAAcAAmwHACANADgAAAAEAAEAOQABADsAAAACADw="
}

private final class OptiFineProcess: @unchecked Sendable {
    struct Result: Sendable { let status: Int32; let output: String }
    private let lock = NSLock()
    private var process: Process?
    private var cancelled = false
    private func start(_ process: Process) throws {
        lock.lock(); defer { lock.unlock() }
        if cancelled { throw CancellationError() }
        try process.run(); self.process = process
    }
    private func cancel() { lock.lock(); cancelled = true; let running = process; lock.unlock(); if running?.isRunning == true { running?.terminate() } }
    private func checkCancellation() throws {
        lock.lock(); let value = cancelled; lock.unlock()
        if value { throw CancellationError() }
    }
    static func run(executable: URL, arguments: [String], directory: URL, log: URL) async throws -> Result {
        let control = OptiFineProcess()
        return try await withTaskCancellationHandler {
            try await Task.detached(priority: .userInitiated) {
                let process = Process(); process.executableURL = executable; process.arguments = arguments; process.currentDirectoryURL = directory
                let pipe = Pipe(); process.standardOutput = pipe; process.standardError = pipe; process.standardInput = FileHandle.nullDevice
                try control.start(process)
                let timeout = DispatchWorkItem { if process.isRunning { process.terminate() } }
                DispatchQueue.global().asyncAfter(deadline: .now() + 180, execute: timeout)
                let bytes = pipe.fileHandleForReading.readDataToEndOfFile(); process.waitUntilExit(); timeout.cancel()
                try? bytes.write(to: log, options: .atomic)
                try control.checkCancellation()
                return Result(status: process.terminationStatus, output: String(decoding: bytes, as: UTF8.self))
            }.value
        } onCancel: { control.cancel() }
    }
}

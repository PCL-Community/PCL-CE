import CryptoKit
import Foundation

public struct LiteLoaderVersion: Codable, Hashable, Sendable, Identifiable {
    public var id: String { "liteloader:\(minecraftVersion):\(version):\(md5)" }
    public let minecraftVersion: String
    public let version: String
    public let displayName: String
    public let filename: String
    public let metadata: Data
    public let md5: String
    public let preview: Bool
    public let published: Date?

    public init(minecraftVersion: String, version: String, displayName: String? = nil, filename: String, metadata: Data,
                md5: String, preview: Bool = false, published: Date? = nil) {
        self.minecraftVersion = minecraftVersion; self.version = version; self.displayName = displayName ?? version
        self.filename = filename; self.metadata = metadata; self.md5 = md5.lowercased(); self.preview = preview; self.published = published
    }
}

/// Layers the original LiteLoader tweaker and its verified libraries onto a private
/// vanilla/Forge profile. The caller publishes/renames that profile after success.
public actor LiteLoaderInstaller {
    private let session: URLSession
    private var applying = false
    private static let catalogueURLs = ["https://dl.liteloader.com/versions/versions.json",
                                        "https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json"]
    private static let tweaker = "com.mumfrey.liteloader.launch.LiteLoaderTweaker"

    public init(session: URLSession = .shared) {
        let configuration = session.configuration
        configuration.httpAdditionalHeaders = configuration.httpAdditionalHeaders?.filter {
            !["authorization", "cookie", "x-api-key"].contains(String(describing: $0.key).lowercased())
        }
        configuration.urlCredentialStorage = nil; configuration.httpCookieStorage = nil; configuration.httpShouldSetCookies = false
        self.session = URLSession(configuration: configuration)
    }
    deinit { session.invalidateAndCancel() }

    public func fetchVersions(minecraftVersion: String) async throws -> [LiteLoaderVersion] {
        try MinecraftInstaller.validateComponent(minecraftVersion)
        // The original catalogue explicitly excludes its unsupported 1.5/1.6 entries.
        if minecraftVersion.hasPrefix("1.5.") || minecraftVersion.hasPrefix("1.6.") { return [] }
        var lastError: Error?
        for raw in Self.catalogueURLs {
            do {
                let data = try await fetch(URL(string: raw)!)
                return try Self.parseVersions(data, minecraftVersion: minecraftVersion)
            } catch { try Task.checkCancellation(); lastError = error }
        }
        throw lastError ?? ModLoaderInstallError.unavailable("无法获取 LiteLoader 版本列表")
    }

    static func parseVersions(_ data: Data, minecraftVersion: String) throws -> [LiteLoaderVersion] {
        guard let catalogue = try JSONSerialization.jsonObject(with: data) as? [String: Any],
              let versions = catalogue["versions"] as? [String: Any] else { throw MinecraftInstallError.invalidMetadata("LiteLoader 版本列表格式错误") }
        guard let entry = versions[minecraftVersion] as? [String: Any] else { return [] }
        // Match the source launcher: prefer release artefacts when present, otherwise snapshots.
        guard let stream = (entry["artefacts"] ?? entry["snapshots"]) as? [String: Any],
              let artefacts = stream["com.mumfrey:liteloader"] as? [String: Any] else { return [] }
        var result: [LiteLoaderVersion] = [], seen = Set<String>()
        let keys = ["latest"] + artefacts.keys.filter { $0 != "latest" }.sorted()
        for key in keys {
            guard let token = artefacts[key] as? [String: Any], let version = token["version"] as? String,
                  let file = token["file"] as? String, let digest = token["md5"] as? String,
                  token["tweakClass"] as? String == tweaker else { continue }
            try MinecraftInstaller.validateComponent(version)
            try validateFilename(file)
            guard digest.range(of: "^[0-9a-fA-F]{32}$", options: .regularExpression) != nil else { throw MinecraftInstallError.invalidMetadata("LiteLoader MD5 无效") }
            guard seen.insert(digest.lowercased()).inserted else { continue }
            let timestamp = (token["timestamp"] as? String).flatMap(Double.init) ?? (token["timestamp"] as? NSNumber)?.doubleValue
            result.append(.init(minecraftVersion: minecraftVersion, version: version, filename: file,
                                metadata: try JSONSerialization.data(withJSONObject: token, options: [.sortedKeys]), md5: digest,
                                preview: (token["stream"] as? String)?.lowercased() == "snapshot", published: timestamp.map(Date.init(timeIntervalSince1970:))))
        }
        return result.sorted {
            if $0.version != $1.version { return $0.version.compare($1.version, options: .numeric) == .orderedDescending }
            return ($0.published ?? .distantPast) > ($1.published ?? .distantPast)
        }
    }

    /// Existing profiles can be inspected without contacting discontinued hosts.
    /// Unmarked older profiles carry a blank MD5 until installation resolves the
    /// exact historical catalogue entry, rather than trusting a guessed checksum.
    public nonisolated static func detect(metadata: [String: Any]) throws -> LiteLoaderVersion? {
        if let marker = metadata["pclLiteLoader"] {
            return try JSONDecoder().decode(LiteLoaderVersion.self, from: JSONSerialization.data(withJSONObject: marker))
        }
        let libraries = metadata["libraries"] as? [[String: Any]] ?? []
        guard let library = libraries.first(where: { ($0["name"] as? String)?.hasPrefix("com.mumfrey:liteloader:") == true }),
              let name = library["name"] as? String, let version = name.split(separator: ":").dropFirst(2).first.map(String.init) else { return nil }
        let base = metadata["pclOriginalVersion"] as? String ?? metadata["inheritsFrom"] as? String
            ?? version.range(of: #"^1\.[0-9]+(?:\.[0-9]+)?"#, options: .regularExpression).map { String(version[$0]) } ?? ""
        try MinecraftInstaller.validateComponent(base)
        let filename = "liteloader-\(version).jar"
        let dependencies = libraries.filter {
            guard let name = $0["name"] as? String else { return false }
            return name.hasPrefix("net.minecraft:launchwrapper:") || name.hasPrefix("org.ow2.asm:asm-all:")
        }
        let token: [String: Any] = ["version": version, "file": filename, "tweakClass": tweaker, "md5": "", "libraries": dependencies]
        return .init(minecraftVersion: base, version: version, filename: filename,
                     metadata: try JSONSerialization.data(withJSONObject: token, options: [.sortedKeys]), md5: "",
                     preview: version.lowercased().contains("snapshot"))
    }

    public func apply(version: LiteLoaderVersion, toProfile profileID: String, gameDirectory: URL, javaExecutable: URL,
                      progress: @escaping @Sendable (InstallProgress) -> Void) async throws {
        guard !applying else { throw MinecraftInstallError.alreadyInstalling }
        applying = true; defer { applying = false }
        var version = version
        if version.md5.isEmpty {
            guard let exact = try await fetchVersions(minecraftVersion: version.minecraftVersion).first(where: { $0.version == version.version }) else {
                throw ModLoaderInstallError.unavailable("LiteLoader 列表中没有已安装的 \(version.version)，无法安全重装")
            }
            version = exact
        }
        try MinecraftInstaller.validateProfileID(profileID)
        try MinecraftInstaller.validateComponent(version.minecraftVersion)
        try MinecraftInstaller.validateComponent(version.version)
        try Self.validateFilename(version.filename)
        guard version.md5.range(of: "^[0-9a-f]{32}$", options: .regularExpression) != nil,
              let token = try JSONSerialization.jsonObject(with: version.metadata) as? [String: Any],
              token["version"] as? String == version.version, (token["md5"] as? String)?.lowercased() == version.md5,
              token["file"] as? String == version.filename, token["tweakClass"] as? String == Self.tweaker else {
            throw MinecraftInstallError.invalidMetadata("LiteLoader 版本信息不完整或不匹配")
        }
        let root = try MinecraftVersionMetadata.safeFile(relativePath: ".pcl-lite-root-\(UUID().uuidString)", under: gameDirectory).deletingLastPathComponent()
        let target = try Self.destination(root: root, relative: "versions/\(profileID)/\(profileID).json")
        try Self.requireRegular(target)
        let original = try Data(contentsOf: target)
        var profile = try MinecraftVersionMetadata.resolve(versionID: profileID, gameDirectory: root)
        let base = profile["pclOriginalVersion"] as? String ?? profile["inheritsFrom"] as? String ?? profile["id"] as? String
        guard base == version.minecraftVersion else { throw MinecraftInstallError.invalidMetadata("LiteLoader 与目标 Minecraft 版本不匹配") }
        if let loader = MinecraftVersionMetadata.modLoader(in: profile), loader.kind != .forge {
            throw MinecraftInstallError.invalidMetadata("LiteLoader 不能与 \(loader.kind.displayName) 同时安装")
        }
        progress(.init(message: "下载 LiteLoader \(version.version)", completed: 0, total: 100))
        let coordinate = "com.mumfrey:liteloader:\(version.version)"
        let litePath = try Self.path(["name": coordinate])
        let liteDestination = try Self.destination(root: root, relative: "libraries/" + litePath)
        let candidates = ["https://dl.liteloader.com/versions/" + litePath,
                          "https://bmclapi2.bangbang93.com/maven/" + litePath]
        let liteBytes = try await downloadMD5(candidates, to: liteDestination, md5: version.md5)
        let liteArchive = try ModpackZIPArchive(url: liteDestination)
        guard liteArchive.member(Self.tweaker.replacingOccurrences(of: ".", with: "/") + ".class") != nil else {
            throw MinecraftInstallError.invalidMetadata("LiteLoader 文件缺少启动入口")
        }
        let liteLibrary: [String: Any] = ["name": coordinate, "downloads": ["artifact": [
            "path": litePath, "url": candidates[0], "sha1": MinecraftInstaller.hash(liteBytes), "size": liteBytes.count]]]
        var additions = token["libraries"] as? [[String: Any]] ?? []
        guard !additions.isEmpty else { throw MinecraftInstallError.invalidMetadata("LiteLoader 缺少依赖列表") }
        additions.append(liteLibrary)
        let oldLibraries = profile["libraries"] as? [[String: Any]] ?? []
        let libraries = try Self.mergedLibraries(existing: oldLibraries, additions: additions)
        var final: [[String: Any]] = []
        let addedIdentities = Set(additions.compactMap(Self.identity))
        for (index, library) in libraries.enumerated() {
            try Task.checkCancellation()
            if let identity = Self.identity(library), addedIdentities.contains(identity), InstallPlatform.libraryAllowed(library), identity != "com.mumfrey:liteloader:" {
                final.append(try await completeLibrary(library, root: root))
            } else { final.append(library) }
            progress(.init(message: "下载 LiteLoader 支持文件", completed: 10 + (index + 1) * 80 / max(1, libraries.count), total: 100))
        }
        profile["libraries"] = final
        profile["mainClass"] = "net.minecraft.launchwrapper.Launch"
        profile["id"] = profileID; profile["pclOriginalVersion"] = version.minecraftVersion
        profile.removeValue(forKey: "inheritsFrom") // Already flattened; do not append inherited arguments twice.
        profile["minimumLauncherVersion"] = max(profile["minimumLauncherVersion"] as? Int ?? 0, 18)
        profile["pclLiteLoader"] = try JSONSerialization.jsonObject(with: JSONEncoder().encode(version))
        try Self.appendTweaker(to: &profile)
        try Task.checkCancellation()
        guard try Data(contentsOf: target) == original else { throw MinecraftInstallError.invalidMetadata("临时版本配置已改变，未提交 LiteLoader") }
        try JSONSerialization.data(withJSONObject: profile, options: [.prettyPrinted, .sortedKeys]).write(to: target, options: .atomic)
        progress(.init(message: "LiteLoader \(version.version) 安装完成", completed: 100, total: 100))
    }

    private func completeLibrary(_ value: [String: Any], root: URL) async throws -> [String: Any] {
        var result = value
        let path = try Self.path(value)
        let destination = try Self.destination(root: root, relative: "libraries/" + path)
        let artifact = (value["downloads"] as? [String: Any])?["artifact"] as? [String: Any]
        let repository = value["url"] as? String ?? "https://libraries.minecraft.net/"
        let raw = artifact?["url"] as? String ?? repository + (repository.hasSuffix("/") ? "" : "/") + path
        let primary = try Self.httpsURL(raw)
        var urls = [primary]
        let mirror = URL(string: "https://bmclapi2.bangbang93.com/maven/" + path)!
        if mirror != primary { urls.append(mirror) }
        if path.hasPrefix("org/ow2/asm/") || path.hasPrefix("org/apache/") || path.hasPrefix("com/google/") {
            let central = URL(string: "https://repo.maven.apache.org/maven2/" + path)!
            if !urls.contains(central) { urls.insert(central, at: 0) }
        }
        var sha1 = artifact?["sha1"] as? String ?? value["sha1"] as? String
        if sha1 == nil || !MinecraftInstaller.validSHA1(sha1!) {
            var failure: Error?
            for url in urls {
                do {
                    let text = String(decoding: try await fetch(url.appendingPathExtension("sha1")), as: UTF8.self)
                    let digest = text.split(whereSeparator: \.isWhitespace).first.map(String.init) ?? ""
                    guard MinecraftInstaller.validSHA1(digest) else { throw MinecraftInstallError.invalidMetadata("LiteLoader 依赖校验值无效：\(path)") }
                    sha1 = digest.lowercased(); break
                } catch { try Task.checkCancellation(); failure = error }
            }
            if sha1 == nil { throw failure ?? MinecraftInstallError.integrity(path) }
        }
        let size = (artifact?["size"] as? NSNumber)?.int64Value
        var chosen = primary
        if !(try MinecraftInstaller.matches(destination, sha1: sha1!, size: size)) {
            var failure: Error?, completed = false
            for url in urls {
                do {
                    try await MinecraftInstaller.download(.init(url: url, destination: destination, sha1: sha1!, size: size), session: session)
                    chosen = url; completed = true; break
                } catch { try Task.checkCancellation(); failure = error }
            }
            guard completed else { throw failure ?? MinecraftInstallError.integrity(path) }
        }
        try Self.requireRegular(destination)
        let actualSize = try destination.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
        result["downloads"] = ["artifact": ["path": path, "url": chosen.absoluteString, "sha1": sha1!, "size": actualSize]]
        return result
    }

    private func downloadMD5(_ candidates: [String], to destination: URL, md5: String) async throws -> Data {
        if FileManager.default.fileExists(atPath: destination.path) {
            try Self.requireRegular(destination)
            let data = try Data(contentsOf: destination)
            if Self.md5(data) == md5 { return data }
        }
        var failure: Error?
        for raw in candidates {
            do {
                let data = try await fetchArtifact(Self.httpsURL(raw))
                guard data.count <= 64 * 1024 * 1024, Self.md5(data) == md5 else { throw MinecraftInstallError.integrity(destination.lastPathComponent) }
                try FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
                try data.write(to: destination, options: .atomic)
                return data
            } catch { try Task.checkCancellation(); failure = error }
        }
        throw failure ?? MinecraftInstallError.integrity(destination.lastPathComponent)
    }

    private func fetch(_ url: URL) async throws -> Data {
        var request = URLRequest(url: url); request.timeoutInterval = 20
        request.setValue("PCL-Mac-Community/LiteLoader", forHTTPHeaderField: "User-Agent")
        let (data, response) = try await PCLHTTPS.data(for: request, session: session)
        guard let http = response as? HTTPURLResponse, (200..<300).contains(http.statusCode) else {
            throw MinecraftInstallError.http((response as? HTTPURLResponse)?.statusCode ?? 0, url.lastPathComponent)
        }
        try Task.checkCancellation()
        return data
    }

    private func fetchArtifact(_ url: URL) async throws -> Data {
        var request = URLRequest(url: url); request.timeoutInterval = 90
        request.setValue("PCL-Mac-Community/LiteLoader", forHTTPHeaderField: "User-Agent")
        let (temporary, response) = try await PCLHTTPS.download(for: request, session: session)
        defer { try? FileManager.default.removeItem(at: temporary) }
        guard let http = response as? HTTPURLResponse, (200..<300).contains(http.statusCode) else {
            throw MinecraftInstallError.http((response as? HTTPURLResponse)?.statusCode ?? 0, url.lastPathComponent)
        }
        guard (try temporary.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0) <= 64 * 1024 * 1024 else {
            throw MinecraftInstallError.invalidMetadata("LiteLoader 文件超过允许大小")
        }
        try Task.checkCancellation()
        return try Data(contentsOf: temporary)
    }

    private static func appendTweaker(to profile: inout [String: Any]) throws {
        if var arguments = profile["arguments"] as? [String: Any], var game = arguments["game"] as? [Any] {
            if !game.contains(where: { $0 as? String == tweaker }) { game += ["--tweakClass", tweaker] }
            arguments["game"] = game; profile["arguments"] = arguments
        } else if let legacy = profile["minecraftArguments"] as? String {
            if !(try MinecraftArguments.tokenize(legacy)).contains(tweaker) { profile["minecraftArguments"] = legacy + " --tweakClass " + tweaker }
        } else { throw MinecraftInstallError.invalidMetadata("临时版本缺少 Minecraft 启动参数") }
    }

    private static func mergedLibraries(existing: [[String: Any]], additions: [[String: Any]]) throws -> [[String: Any]] {
        var result = existing
        for next in additions {
            guard let key = identity(next), let coordinate = next["name"] as? String else { throw MinecraftInstallError.invalidMetadata("LiteLoader 依赖坐标无效") }
            _ = try path(next)
            if let index = result.firstIndex(where: { identity($0) == key }) {
                let oldCoordinate = result[index]["name"] as? String ?? ""
                let oldVersion = oldCoordinate.split(separator: ":").dropFirst(2).first.map(String.init) ?? ""
                let newVersion = coordinate.split(separator: ":").dropFirst(2).first.map(String.init) ?? ""
                if key == "com.mumfrey:liteloader:" || oldVersion.compare(newVersion, options: .numeric) == .orderedAscending { result[index] = next }
            } else { result.append(next) }
        }
        return result
    }
    private static func identity(_ library: [String: Any]) -> String? {
        guard let name = library["name"] as? String else { return nil }
        let fields = name.split(separator: ":", omittingEmptySubsequences: false)
        guard fields.count == 3 || fields.count == 4 else { return nil }
        return "\(fields[0]):\(fields[1]):\(fields.count == 4 ? fields[3] : "")"
    }
    private static func path(_ library: [String: Any]) throws -> String {
        guard let path = MinecraftVersionMetadata.libraryPath(library), !path.isEmpty else { throw MinecraftInstallError.invalidMetadata("LiteLoader 依赖路径无效") }
        _ = try ModpackZIPArchive.validatedPath(path)
        return path
    }
    private static func destination(root: URL, relative: String) throws -> URL {
        let raw = root.appendingPathComponent(relative)
        let safe = try MinecraftInstaller.safeDestination(root: root, relative: relative)
        guard raw == safe else { throw MinecraftInstallError.unsafePath(raw.path) }
        return safe
    }
    private static func validateFilename(_ value: String) throws {
        guard value.hasSuffix(".jar"), !value.contains("/"), !value.contains("\\"), value != ".jar" else { throw MinecraftInstallError.unsafePath(value) }
    }
    private static func requireRegular(_ file: URL) throws {
        let values = try file.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey])
        guard values.isRegularFile == true, values.isSymbolicLink != true else { throw MinecraftInstallError.unsafePath(file.path) }
    }
    private static func httpsURL(_ raw: String) throws -> URL {
        let upgraded: String
        if raw.hasPrefix("http://dl.liteloader.com/") || raw.hasPrefix("http://repo.mumfrey.com/")
            || raw.hasPrefix("http://repo.maven.apache.org/") || raw.hasPrefix("http://repo.spongepowered.org/") {
            upgraded = "https" + raw.dropFirst(4)
        } else { upgraded = raw }
        guard let url = URL(string: upgraded), url.scheme == "https", url.user == nil, url.password == nil else {
            throw MinecraftInstallError.invalidMetadata("LiteLoader 地址必须使用 HTTPS")
        }
        return url
    }
    private static func md5(_ data: Data) -> String { Insecure.MD5.hash(data: data).map { String(format: "%02x", $0) }.joined() }
}

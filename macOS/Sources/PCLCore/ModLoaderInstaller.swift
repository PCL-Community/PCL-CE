import Foundation
import Darwin

public enum ModLoaderKind: String, CaseIterable, Codable, Sendable {
    case forge, neoForge, fabric, quilt
    public var displayName: String {
        switch self { case .forge: return "Forge"; case .neoForge: return "NeoForge"; case .fabric: return "Fabric"; case .quilt: return "Quilt" }
    }
}

public struct ModLoaderVersion: Identifiable, Hashable, Codable, Sendable {
    public let id: String
    public let kind: ModLoaderKind
    public let minecraftVersion: String
    public let version: String
    public let stable: Bool
    public let recommended: Bool
    public var displayName: String { version }
    public init(id: String? = nil, kind: ModLoaderKind, minecraftVersion: String, version: String, stable: Bool = true, recommended: Bool = false) {
        self.id = id ?? "\(kind.rawValue):\(minecraftVersion):\(version)"
        self.kind = kind; self.minecraftVersion = minecraftVersion; self.version = version
        self.stable = stable; self.recommended = recommended
    }
}

public enum ModLoaderInstallError: LocalizedError {
    case unavailable(String), installerFailed(String), invalidJava(String)
    public var errorDescription: String? {
        switch self {
        case .unavailable(let message): return message
        case .installerFailed(let output): return "加载器安装失败：\(output)"
        case .invalidJava(let message): return "Java 不适合安装此加载器：\(message)"
        }
    }
}

/// Profiles are published last. Downloaded libraries remain a reusable cache on cancellation.
public actor ModLoaderInstaller {
    private let session: URLSession
    private let vanillaInstaller: MinecraftInstaller
    private var installing = false
    private var forgeInstallerHashes: [String: String] = [:]
    private var preferredMirrorHosts: Set<String> = []
    public init(session: URLSession = .shared, vanillaInstaller: MinecraftInstaller? = nil) {
        self.session = session
        self.vanillaInstaller = vanillaInstaller ?? MinecraftInstaller(session: session)
    }

    public func fetchVersions(kind: ModLoaderKind, minecraftVersion: String) async throws -> [ModLoaderVersion] {
        try MinecraftInstaller.validateComponent(minecraftVersion)
        switch kind {
        case .fabric, .quilt:
            let host = kind == .fabric ? "https://meta.fabricmc.net/v2" : "https://meta.quiltmc.org/v3"
            let data = try await fetch(URL(string: "\(host)/versions/loader/\(minecraftVersion)")!)
            guard let values = try JSONSerialization.jsonObject(with: data) as? [[String: Any]] else { throw MinecraftInstallError.invalidMetadata("加载器版本列表格式错误") }
            var seen = Set<String>()
            return values.compactMap { entry in
                guard let loader = entry["loader"] as? [String: Any], let version = loader["version"] as? String,
                      seen.insert(version).inserted else { return nil }
                let stable = loader["stable"] as? Bool ?? !Self.isPrerelease(version)
                return ModLoaderVersion(kind: kind, minecraftVersion: minecraftVersion, version: version, stable: stable)
            }.sorted { Self.versionIsNewer($0.version, than: $1.version) }
        case .forge:
            // Match upstream's source fallback without waiting several full TLS
            // retries before a reachable mirror can supply a valid catalogue.
            return try await withThrowingTaskGroup(of: [ModLoaderVersion].self) { group in
                group.addTask { try await self.officialForgeVersions(minecraftVersion) }
                group.addTask {
                    try await Task.sleep(nanoseconds: 500_000_000)
                    return try await self.forgeMirrorVersions(minecraftVersion)
                }
                var failures: [String] = [], receivedEmpty = false
                while !group.isEmpty {
                    do {
                        guard let versions = try await group.next() else { break }
                        try Task.checkCancellation()
                        if !versions.isEmpty { group.cancelAll(); return versions }
                        receivedEmpty = true
                    } catch {
                        try Task.checkCancellation()
                        failures.append(error.localizedDescription)
                    }
                }
                if receivedEmpty { return [] }
                throw ModLoaderInstallError.unavailable(failures.joined(separator: "\n"))
            }
        case .neoForge:
            let legacy = minecraftVersion == "1.20.1"
            let artifact = legacy ? "forge" : "neoforge"
            let xml = try await fetch(URL(string: "https://maven.neoforged.net/releases/net/neoforged/\(artifact)/maven-metadata.xml")!)
            let prefix = Self.neoForgePrefix(minecraftVersion)
            return try Self.mavenVersions(xml).filter { value in
                legacy ? value.hasPrefix(minecraftVersion + "-") : (prefix.map { value.hasPrefix($0) } ?? false)
            }.map { value in
                let version = legacy ? String(value.dropFirst(minecraftVersion.count + 1)) : value
                return ModLoaderVersion(kind: kind, minecraftVersion: minecraftVersion, version: version, stable: !Self.isPrerelease(version))
            }.sorted { Self.versionIsNewer($0.version, than: $1.version) }
        }
    }

    public func install(kind: ModLoaderKind, version: ModLoaderVersion, minecraftVersion: RemoteVersion,
                        gameDirectory: URL, profileID: String, javaExecutable: URL,
                        progress: @escaping @Sendable (InstallProgress) -> Void) async throws {
        guard !installing else { throw MinecraftInstallError.alreadyInstalling }
        guard kind == version.kind, version.minecraftVersion == minecraftVersion.id else { throw MinecraftInstallError.invalidMetadata("加载器与 Minecraft 版本不匹配") }
        try MinecraftInstaller.validateProfileID(profileID)
        try MinecraftInstaller.validateComponent(version.version)
        guard profileID != minecraftVersion.id else { throw MinecraftInstallError.invalidMetadata("加载器版本需要一个独立的版本名称") }
        installing = true
        defer { installing = false }
        let root = gameDirectory.standardizedFileURL
        let target = try MinecraftInstaller.safeDestination(root: root, relative: "versions/\(profileID)/\(profileID).json")
        var provenance: [String: Any] = [:]
        if FileManager.default.fileExists(atPath: target.path) {
            let existing = try JSONSerialization.jsonObject(with: Data(contentsOf: target)) as? [String: Any]
            let marker = existing?["pclLoader"] as? [String: String]
            let existingKind = existing?["pclLoaderKind"] as? String ?? marker?["kind"]
            let existingVersion = existing?["pclLoaderVersion"] as? String ?? marker?["version"]
            guard existingKind == kind.rawValue, existingVersion == version.version,
                  existing?["pclOriginalVersion"] as? String == minecraftVersion.id else {
                throw MinecraftInstallError.invalidMetadata("目标版本已存在，不能覆盖其它游戏配置")
            }
            provenance = existing?.filter { $0.key.hasPrefix("pcl") } ?? [:]
        }
        let monitor = InstallProgressContext.current
        var currentStage = "loaderMetadata"
        do {
            progress(.init(message: "准备 \(minecraftVersion.id) 原版文件", completed: 0, total: 100))
            try await vanillaInstaller.install(version: minecraftVersion, gameDirectory: root) { value in
                progress(.init(message: value.message, completed: Int(value.fraction * 60), total: 100))
            }
            try Task.checkCancellation()
            monitor?.begin("loaderMetadata")
            var profile: [String: Any]
            if kind == .fabric || kind == .quilt {
                let host = kind == .fabric ? "https://meta.fabricmc.net/v2" : "https://meta.quiltmc.org/v3"
                let data = try await fetch(URL(string: "\(host)/versions/loader/\(minecraftVersion.id)/\(version.version)/profile/json")!)
                guard let value = try JSONSerialization.jsonObject(with: data) as? [String: Any],
                      value["inheritsFrom"] as? String == minecraftVersion.id,
                      value["mainClass"] as? String != nil else { throw MinecraftInstallError.invalidMetadata("加载器配置缺少基础版本或主类") }
                profile = value
                monitor?.complete("loaderMetadata")
                currentStage = "loaderFiles"
                monitor?.begin("loaderFiles")
                progress(.init(message: "下载 \(kind.displayName) 依赖库", completed: 62, total: 100))
                try await InstallProgressContext.$stageID.withValue("loaderFiles") {
                    try await installLibraries(profile["libraries"] as? [[String: Any]] ?? [], root: root, progress: progress)
                }
                monitor?.complete("loaderFiles")
            } else {
                profile = try await installJavaLoader(version: version, minecraftVersion: minecraftVersion, root: root, javaExecutable: javaExecutable, progress: progress)
            }
            try Task.checkCancellation()
            currentStage = "install"
            monitor?.begin("install")
            for (key, value) in provenance { profile[key] = value }
            profile["id"] = profileID
            profile["inheritsFrom"] = minecraftVersion.id
            profile["jar"] = minecraftVersion.id
            profile["pclOriginalVersion"] = minecraftVersion.id
            profile["pclLoaderKind"] = kind.rawValue
            profile["pclLoaderVersion"] = version.version
            profile["pclLoader"] = ["kind": kind.rawValue, "version": version.version, "minecraftVersion": minecraftVersion.id]
            try FileManager.default.createDirectory(at: target.deletingLastPathComponent(), withIntermediateDirectories: true)
            let local = InstalledVersion(id: profileID, type: "release", requiredJava: 17, directory: target.deletingLastPathComponent(), inheritsFrom: minecraftVersion.id)
            if !FileManager.default.fileExists(atPath: try VersionSettingsRepository.settingsURL(version: local).path) {
                var settings = VersionProfileSettings()
                settings.category = "modded"
                settings.isolateGameDirectory = true
                settings.iconName = kind.displayName
                settings.displayDescription = "Minecraft \(minecraftVersion.id) · \(kind.displayName) \(version.version)"
                try VersionSettingsRepository.save(settings, for: local)
            }
            try JSONSerialization.data(withJSONObject: profile, options: [.prettyPrinted, .sortedKeys]).write(to: target, options: .atomic)
            monitor?.complete("install")
            progress(.init(message: "\(kind.displayName) \(version.version) 安装完成", completed: 100, total: 100))
        } catch {
            if !Task.isCancelled, monitor?.snapshot.stages.contains(where: { $0.state == .failed }) != true {
                let knownStage = monitor?.snapshot.stages.first(where: {
                    ["loaderMetadata", "loaderFiles", "install"].contains($0.id) && $0.state == .running
                })?.id ?? currentStage
                monitor?.fail(knownStage, message: error.localizedDescription)
            }
            throw error
        }
    }

    public func install(version: ModLoaderVersion, minecraftVersion: RemoteVersion, gameDirectory: URL,
                        profileID: String, javaExecutable: URL, progress: @escaping @Sendable (InstallProgress) -> Void) async throws {
        try await install(kind: version.kind, version: version, minecraftVersion: minecraftVersion, gameDirectory: gameDirectory,
                          profileID: profileID, javaExecutable: javaExecutable, progress: progress)
    }

    private func officialForgeVersions(_ minecraftVersion: String) async throws -> [ModLoaderVersion] {
        let xml = try await fetch(URL(string: "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml")!)
        let versions = try Self.mavenVersions(xml).filter { $0.hasPrefix(minecraftVersion + "-") }
        guard !versions.isEmpty else { return [] }
        var recommended: String?
        if let data = try? await fetch(URL(string: "https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json")!),
           let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any], let promos = object["promos"] as? [String: String] {
            recommended = promos[minecraftVersion + "-recommended"]
        }
        try Task.checkCancellation()
        return versions.map { coordinate in
            let version = String(coordinate.dropFirst(minecraftVersion.count + 1))
            return ModLoaderVersion(kind: .forge, minecraftVersion: minecraftVersion, version: version,
                                    stable: !Self.isPrerelease(version), recommended: version == recommended)
        }.sorted { Self.versionIsNewer($0.version, than: $1.version) }
    }

    private func forgeMirrorVersions(_ minecraftVersion: String) async throws -> [ModLoaderVersion] {
        let data = try await fetch(URL(string: "https://bmclapi2.bangbang93.com/forge/minecraft/\(minecraftVersion)")!)
        guard let values = try JSONSerialization.jsonObject(with: data) as? [[String: Any]] else { throw MinecraftInstallError.invalidMetadata("Forge 镜像版本列表无效") }
        return values.compactMap { entry in
            guard let version = entry["version"] as? String, let files = entry["files"] as? [[String: Any]],
                  let installer = files.first(where: { $0["category"] as? String == "installer" && $0["format"] as? String == "jar" }),
                  let hash = installer["hash"] as? String, MinecraftInstaller.validSHA1(hash) else { return nil }
            forgeInstallerHashes[minecraftVersion + "-" + version] = hash
            return ModLoaderVersion(kind: .forge, minecraftVersion: minecraftVersion, version: version, stable: !Self.isPrerelease(version))
        }.sorted { Self.versionIsNewer($0.version, than: $1.version) }
    }

    private func installJavaLoader(version: ModLoaderVersion, minecraftVersion: RemoteVersion, root: URL, javaExecutable: URL,
                                   progress: @escaping @Sendable (InstallProgress) -> Void) async throws -> [String: Any] {
        guard let java = JavaDiscovery.inspect(executable: javaExecutable) else { throw ModLoaderInstallError.invalidJava("请选择有效的 Java") }
        let baseJSON = try MinecraftInstaller.safeDestination(root: root, relative: "versions/\(minecraftVersion.id)/\(minecraftVersion.id).json")
        let base = try JSONSerialization.jsonObject(with: Data(contentsOf: baseJSON)) as? [String: Any] ?? [:]
        let required = MinecraftVersionMetadata.requiredJava(base)
        guard java.majorVersion >= required else { throw ModLoaderInstallError.invalidJava("此版本需要 Java \(required)，当前为 Java \(java.majorVersion)") }
        let descriptor = try Self.installerDescriptor(version)
        let jar = try MinecraftInstaller.safeDestination(root: root, relative: "PCL/installers/\(descriptor.file)")
        var expected: String?
        do { expected = try await checksum(descriptor.url.appendingPathExtension("sha1")) }
        catch {
            try Task.checkCancellation()
            if version.kind == .forge {
                if forgeInstallerHashes[descriptor.coordinate] == nil { _ = try await forgeMirrorVersions(minecraftVersion.id) }
                expected = forgeInstallerHashes[descriptor.coordinate]
            } else { throw error }
        }
        guard let expected else { throw MinecraftInstallError.invalidMetadata("没有可验证的官方安装器 SHA-1") }
        let monitor = InstallProgressContext.current
        monitor?.complete("loaderMetadata")
        monitor?.begin("loaderFiles")
        monitor?.setRemainingFiles(1, for: "loader-installer")
        progress(.init(message: "下载并校验 \(version.kind.displayName) 安装器", completed: 61, total: 100))
        try await InstallProgressContext.$stageID.withValue("loaderFiles") {
            try await downloadVerified(url: descriptor.url, destination: jar, hash: expected, size: nil)
        }
        monitor?.setRemainingFiles(0, for: "loader-installer")
        let stage = try MinecraftInstaller.safeDestination(root: root, relative: "PCL/install-staging/\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: stage, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: stage) }
        let baseTarget = stage.appendingPathComponent("versions/\(minecraftVersion.id)")
        try FileManager.default.createDirectory(at: baseTarget, withIntermediateDirectories: true)
        for ext in ["json", "jar"] {
            let source = try MinecraftInstaller.safeDestination(root: root, relative: "versions/\(minecraftVersion.id)/\(minecraftVersion.id).\(ext)")
            try FileManager.default.copyItem(at: source, to: baseTarget.appendingPathComponent("\(minecraftVersion.id).\(ext)"))
        }
        let libraries = try MinecraftInstaller.safeDestination(root: root, relative: "libraries")
        try FileManager.default.createDirectory(at: libraries, withIntermediateDirectories: true)
        try FileManager.default.createSymbolicLink(at: stage.appendingPathComponent("libraries"), withDestinationURL: libraries)
        try Data(#"{"profiles":{}}"#.utf8).write(to: stage.appendingPathComponent("launcher_profiles.json"))
        let install = try Self.readArchiveJSON(jar, entry: "install_profile.json")
        let launcher: [String: Any]
        if let jsonPath = install["json"] as? String { launcher = try Self.readArchiveJSON(jar, entry: String(jsonPath.drop(while: { $0 == "/" }))) }
        else if let legacy = install["versionInfo"] as? [String: Any] { launcher = legacy }
        else { launcher = try Self.readArchiveJSON(jar, entry: "version.json") }
        let allLibraries = (install["libraries"] as? [[String: Any]] ?? []) + (launcher["libraries"] as? [[String: Any]] ?? [])
        progress(.init(message: "下载 \(version.kind.displayName) 安装依赖", completed: 64, total: 100))
        try await installLibraries(allLibraries, root: root, includeAllPlatforms: true, embeddedArchive: jar, progress: progress)
        monitor?.complete("loaderFiles")
        monitor?.begin("install")
        if java.majorVersion >= 11 {
            do {
                try await prepareMojangMappings(install: install, base: base, root: root, stage: stage, javaExecutable: javaExecutable, progress: progress)
            } catch {
                try Task.checkCancellation()
                // A JRE may omit source compilation. The installer can still prepare the
                // same output itself and validates its declared processor checksums.
                progress(.init(message: "使用官方安装器准备映射文件", completed: 91, total: 100))
            }
        }
        progress(.init(message: "执行 \(version.kind.displayName) 官方安装器（补丁处理可能需要数分钟）", completed: 92, total: 100))
        let result = try await LoaderJavaProcess.run(executable: javaExecutable,
            arguments: ["-Djava.awt.headless=true", "-jar", jar.path, "--installClient", stage.path, "--offline"],
            directory: stage, log: stage.appendingPathComponent("installer-output.log"))
        guard result.status == 0 else { throw ModLoaderInstallError.installerFailed(String(result.output.suffix(5000))) }
        let versionsRoot = stage.appendingPathComponent("versions")
        let candidates = try FileManager.default.contentsOfDirectory(at: versionsRoot, includingPropertiesForKeys: nil)
        for candidate in candidates where candidate.lastPathComponent != minecraftVersion.id {
            let json = candidate.appendingPathComponent(candidate.lastPathComponent + ".json")
            guard let data = try? Data(contentsOf: json), var profile = try JSONSerialization.jsonObject(with: data) as? [String: Any] else { continue }
            guard profile["inheritsFrom"] as? String == minecraftVersion.id else { continue }
            // Installer-produced artifacts with an empty URL must exist after processors.
            for library in profile["libraries"] as? [[String: Any]] ?? [] where InstallPlatform.libraryAllowed(library) {
                guard let path = MinecraftVersionMetadata.libraryPath(library) else { continue }
                let artifact = try MinecraftInstaller.safeDestination(root: libraries, relative: path)
                guard FileManager.default.fileExists(atPath: artifact.path) else { throw MinecraftInstallError.invalidMetadata("安装器未生成依赖：\(path)") }
            }
            profile["pclInstallerVersion"] = candidate.lastPathComponent
            return profile
        }
        throw ModLoaderInstallError.installerFailed("安装器未生成可用的客户端版本。旧版 Forge 安装器可能不支持无界面客户端安装。\n" + String(result.output.suffix(3000)))
    }

    /// The Java installer has its own HTTP stack. Prepare its checksummed mapping output
    /// with the selected session so network/proxy behavior matches all other downloads.
    private func prepareMojangMappings(install: [String: Any], base: [String: Any], root: URL, stage: URL, javaExecutable: URL,
                                      progress: @escaping @Sendable (InstallProgress) -> Void) async throws {
        let data = install["data"] as? [String: [String: String]] ?? [:]
        func resolve(_ value: String, depth: Int = 0) -> String {
            guard depth < 16 else { return value }
            if value.hasPrefix("{"), value.hasSuffix("}"), let client = data[String(value.dropFirst().dropLast())]?["client"] { return resolve(client, depth: depth + 1) }
            if value.hasPrefix("'"), value.hasSuffix("'") { return String(value.dropFirst().dropLast()) }
            return value
        }
        for processor in install["processors"] as? [[String: Any]] ?? [] {
            let args = processor["args"] as? [String] ?? []
            guard args.contains("DOWNLOAD_MOJMAPS"), (processor["sides"] as? [String] ?? ["client"]).contains("client"),
                  let outputIndex = args.firstIndex(of: "--output"), outputIndex + 1 < args.count else { continue }
            let outputToken = args[outputIndex + 1]
            let coordinate = resolve(outputToken)
            guard coordinate.hasPrefix("["), coordinate.hasSuffix("]"),
                  let path = MinecraftVersionMetadata.libraryPath(["name": String(coordinate.dropFirst().dropLast())]),
                  let outputs = processor["outputs"] as? [String: String],
                  let hashExpression = outputs[outputToken] ?? outputs.first(where: { resolve($0.key) == coordinate })?.value else { continue }
            let hash = resolve(hashExpression)
            guard MinecraftInstaller.validSHA1(hash) else { continue }
            let destination = try MinecraftInstaller.safeDestination(root: root, relative: "libraries/" + path)
            if try MinecraftInstaller.matches(destination, sha1: hash, size: nil) { continue }
            guard let downloads = base["downloads"] as? [String: Any], let mappings = downloads["client_mappings"] as? [String: Any],
                  let baseID = base["pclOriginalVersion"] as? String ?? base["id"] as? String else { continue }
            try MinecraftInstaller.validateComponent(baseID)
            let source = try MinecraftInstaller.safeDestination(root: root, relative: "PCL/mappings/\(baseID)-client.txt")
            let mappingArtifact = try MinecraftInstaller.artifact(mappings, destination: source)
            progress(.init(message: "准备 Mojang 官方映射文件", completed: 90, total: 100))
            try await MinecraftInstaller.download(mappingArtifact, session: session)
            let coordinates = [processor["jar"] as? String].compactMap { $0 } + (processor["classpath"] as? [String] ?? [])
            let jars = try coordinates.map { name -> String in
                guard let path = MinecraftVersionMetadata.libraryPath(["name": name]) else { throw MinecraftInstallError.invalidMetadata("安装处理器的 Maven 坐标无效") }
                return try MinecraftInstaller.safeDestination(root: root, relative: "libraries/" + path).path
            }
            let namespace = coordinates.contains(where: { $0.hasPrefix("net.neoforged:srgutils:") }) ? "net.neoforged" : "net.minecraftforge"
            let helper = stage.appendingPathComponent("PCLMappingPreparation.java")
            let javaSource = """
                import java.io.File;
                import java.nio.file.Paths;
                import \(namespace).srgutils.IMappingFile;
                class PCLMappingPreparation {
                    public static void main(String[] args) throws Exception {
                        IMappingFile.load(new File(args[0])).write(Paths.get(args[1]), IMappingFile.Format.valueOf(args[2]), false);
                    }
                }
                """
            try Data(javaSource.utf8).write(to: helper)
            try FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
            let staging = destination.deletingLastPathComponent().appendingPathComponent(".\(UUID().uuidString).mapping")
            defer { try? FileManager.default.removeItem(at: staging) }
            let formatIndex = args.firstIndex(of: "--format")
            let format = formatIndex.flatMap { $0 + 1 < args.count ? args[$0 + 1] : nil } ?? "TSRG"
            let result = try await LoaderJavaProcess.run(executable: javaExecutable,
                arguments: ["--class-path", jars.joined(separator: ":"), helper.path, source.path, staging.path, format],
                directory: stage, log: stage.appendingPathComponent("mapping-output.log"))
            guard result.status == 0 else { throw ModLoaderInstallError.installerFailed(String(result.output.suffix(3000))) }
            guard try MinecraftInstaller.matches(staging, sha1: hash, size: nil) else { throw MinecraftInstallError.integrity(destination.lastPathComponent) }
            try MinecraftInstaller.commit(staging, to: destination)
        }
    }

    private func installLibraries(_ values: [[String: Any]], root: URL, includeAllPlatforms: Bool = false,
                                  embeddedArchive: URL? = nil, progress: @escaping @Sendable (InstallProgress) -> Void) async throws {
        let libraryRoot = try MinecraftInstaller.safeDestination(root: root, relative: "libraries")
        var seen = Set<String>()
        var items: [([String: Any], String)] = []
        for value in values where includeAllPlatforms || InstallPlatform.libraryAllowed(value) {
            guard let path = MinecraftVersionMetadata.libraryPath(value) else { throw MinecraftInstallError.invalidMetadata("加载器依赖缺少有效的 Maven 坐标或路径") }
            guard seen.insert(path).inserted else { continue }
            _ = try MinecraftInstaller.safeDestination(root: libraryRoot, relative: path)
            items.append((value, path))
        }
        let monitor = InstallProgressContext.current
        monitor?.setRemainingFiles(items.count, for: "loader-libraries")
        for (index, item) in items.enumerated() {
            var itemCompleted = false
            defer {
                if itemCompleted {
                    monitor?.setRemainingFiles(items.count - index - 1, for: "loader-libraries")
                    monitor?.update("loaderFiles", fraction: Double(index + 1) / Double(max(1, items.count)))
                }
            }
            try Task.checkCancellation()
            let (library, path) = item
            let destination = try MinecraftInstaller.safeDestination(root: libraryRoot, relative: path)
            let artifact = (library["downloads"] as? [String: Any])?["artifact"] as? [String: Any]
            let size = (artifact?["size"] as? NSNumber)?.int64Value ?? (library["size"] as? NSNumber)?.int64Value
            var digest = artifact?["sha1"] as? String ?? library["sha1"] as? String
            if let digest, MinecraftInstaller.validSHA1(digest), try MinecraftInstaller.matches(destination, sha1: digest, size: size) { itemCompleted = true; continue }
            if let embeddedArchive, let data = try? Self.readArchiveEntry(embeddedArchive, entry: "maven/" + path), !data.isEmpty {
                if let digest, MinecraftInstaller.validSHA1(digest), MinecraftInstaller.hash(data) != digest.lowercased() { throw MinecraftInstallError.integrity(path) }
                try FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
                try data.write(to: destination, options: .atomic)
                itemCompleted = true
                continue
            }
            let rawURL: String
            if let artifactURL = artifact?["url"] as? String {
                if artifactURL.isEmpty { itemCompleted = true; continue } // A generated processor output, checked after installation.
                rawURL = artifactURL
            } else {
                let repository = library["url"] as? String ?? "https://libraries.minecraft.net/"
                rawURL = repository + (repository.hasSuffix("/") ? "" : "/") + path
            }
            guard let url = URL(string: rawURL), url.scheme == "https" else { throw MinecraftInstallError.invalidMetadata("依赖库地址必须使用 HTTPS") }
            if digest == nil || !MinecraftInstaller.validSHA1(digest!) { digest = try await checksum(url.appendingPathExtension("sha1")) }
            try await InstallProgressContext.$stageID.withValue("loaderFiles") {
                try await downloadVerified(url: url, destination: destination, hash: digest!, size: size)
            }
            itemCompleted = true
            progress(.init(message: "下载依赖 \(index + 1)/\(items.count) · \(destination.lastPathComponent)", completed: 64 + (items.isEmpty ? 0 : index * 25 / items.count), total: 100))
        }
    }

    private func checksum(_ url: URL) async throws -> String {
        guard let mirror = Self.mirrorURL(url) else { return try Self.parseChecksum(try await fetch(url)) }
        let transport = session
        let result = try await withThrowingTaskGroup(of: (String, Bool).self) { group in
            group.addTask { (try Self.parseChecksum(await MinecraftInstaller.fetchData(url: url, session: transport)), false) }
            group.addTask {
                try await Task.sleep(nanoseconds: 500_000_000)
                return (try Self.parseChecksum(await MinecraftInstaller.fetchData(url: mirror, session: transport)), true)
            }
            var failures: [String] = []
            while !group.isEmpty {
                do {
                    if let result = try await group.next() { try Task.checkCancellation(); group.cancelAll(); return result }
                } catch { try Task.checkCancellation(); failures.append(error.localizedDescription) }
            }
            throw MinecraftInstallError.invalidMetadata("无法获取安装器校验值：" + failures.joined(separator: "\n"))
        }
        if result.1, let host = url.host { preferredMirrorHosts.insert(host.lowercased()) }
        return result.0
    }

    private nonisolated static func parseChecksum(_ data: Data) throws -> String {
        guard let text = String(data: data, encoding: .utf8), let digest = text.split(whereSeparator: \.isWhitespace).first.map(String.init), MinecraftInstaller.validSHA1(digest) else {
            throw MinecraftInstallError.invalidMetadata("Maven SHA-1 校验文件无效")
        }
        return digest.lowercased()
    }

    private func fetch(_ url: URL) async throws -> Data {
        do { return try await MinecraftInstaller.fetchData(url: url, session: session) }
        catch {
            try Task.checkCancellation()
            if let mirror = Self.mirrorURL(url) { return try await MinecraftInstaller.fetchData(url: mirror, session: session) }
            throw error
        }
    }

    private func downloadVerified(url: URL, destination: URL, hash: String, size: Int64?) async throws {
        let mirror = Self.mirrorURL(url)
        let candidates: [URL]
        if let mirror, preferredMirrorHosts.contains(url.host?.lowercased() ?? "") { candidates = [mirror, url] }
        else { candidates = [url] + (mirror.map { [$0] } ?? []) }
        var lastError: Error?
        for source in candidates {
            do { try await MinecraftInstaller.download(InstallArtifact(url: source, destination: destination, sha1: hash, size: size), session: session); return }
            catch { try Task.checkCancellation(); lastError = error }
        }
        throw lastError ?? MinecraftInstallError.invalidMetadata("没有可用的下载地址")
    }

    static func mirrorURL(_ url: URL) -> URL? {
        guard ["maven.minecraftforge.net", "files.minecraftforge.net"].contains(url.host?.lowercased() ?? "") else { return nil }
        let path = url.path.hasPrefix("/maven/") ? String(url.path.dropFirst(7)) : String(url.path.dropFirst())
        return URL(string: "https://bmclapi2.bangbang93.com/maven/" + path)
    }

    static func neoForgePrefix(_ minecraft: String) -> String? {
        let parts = minecraft.split(separator: ".")
        if parts.count >= 2, parts[0] == "1" {
            return "\(parts[1]).\(parts.count > 2 ? parts[2] : "0")."
        }
        if parts.count >= 2 { return "\(parts[0]).\(parts[1])." }
        return nil
    }

    static func installerDescriptor(_ value: ModLoaderVersion) throws -> (url: URL, file: String, coordinate: String) {
        try MinecraftInstaller.validateComponent(value.minecraftVersion)
        try MinecraftInstaller.validateComponent(value.version)
        let prefix = value.minecraftVersion + "-"
        let loader = value.version.hasPrefix(prefix) ? String(value.version.dropFirst(prefix.count)) : value.version
        let coordinate: String
        let root: String
        let artifact: String
        if value.kind == .forge {
            coordinate = prefix + loader; root = "https://maven.minecraftforge.net/net/minecraftforge/forge"; artifact = "forge"
        } else if value.kind == .neoForge {
            let legacy = value.minecraftVersion == "1.20.1"
            coordinate = legacy ? prefix + loader : loader
            artifact = legacy ? "forge" : "neoforge"
            root = "https://maven.neoforged.net/releases/net/neoforged/\(artifact)"
        } else { throw MinecraftInstallError.invalidMetadata("此加载器没有 Java 安装器") }
        let file = "\(artifact)-\(coordinate)-installer.jar"
        guard let url = URL(string: "\(root)/\(coordinate)/\(file)") else { throw MinecraftInstallError.invalidMetadata("加载器版本号无效") }
        return (url, file, coordinate)
    }

    static func mavenVersions(_ data: Data) throws -> [String] {
        let delegate = MavenVersionsParser()
        let parser = XMLParser(data: data)
        parser.shouldResolveExternalEntities = false
        parser.delegate = delegate
        guard parser.parse() else { throw MinecraftInstallError.invalidMetadata("Maven 版本列表 XML 无效") }
        return delegate.versions
    }

    static func versionIsNewer(_ first: String, than second: String) -> Bool { first.compare(second, options: .numeric) == .orderedDescending }
    private static func isPrerelease(_ version: String) -> Bool {
        let lower = version.lowercased()
        return ["alpha", "beta", "snapshot", "pre", "rc"].contains { lower.contains($0) }
    }

    private static func readArchiveJSON(_ archive: URL, entry: String) throws -> [String: Any] {
        let data = try readArchiveEntry(archive, entry: entry)
        guard let object = try JSONSerialization.jsonObject(with: data) as? [String: Any] else { throw MinecraftInstallError.invalidMetadata("安装器中的 \(entry) 无效") }
        return object
    }

    private static func readArchiveEntry(_ archive: URL, entry: String) throws -> Data {
        guard !entry.hasPrefix("/"), !entry.split(separator: "/").contains(".."), !entry.contains("\\") else { throw MinecraftInstallError.unsafePath(entry) }
        let result = try NativeProcess.capture(URL(fileURLWithPath: "/usr/bin/unzip"), ["-p", archive.path, entry], timeout: 30)
        guard result.status == 0 else { throw MinecraftInstallError.invalidMetadata("安装器中没有 \(entry)") }
        return result.output
    }
}

private final class MavenVersionsParser: NSObject, XMLParserDelegate {
    var versions: [String] = []
    private var insideVersion = false
    private var value = ""
    func parser(_ parser: XMLParser, didStartElement elementName: String, namespaceURI: String?, qualifiedName qName: String?, attributes attributeDict: [String : String] = [:]) {
        insideVersion = elementName == "version"
        if insideVersion { value = "" }
    }
    func parser(_ parser: XMLParser, foundCharacters string: String) { if insideVersion { value += string } }
    func parser(_ parser: XMLParser, didEndElement elementName: String, namespaceURI: String?, qualifiedName qName: String?) {
        if elementName == "version" { versions.append(value.trimmingCharacters(in: .whitespacesAndNewlines)); insideVersion = false }
    }
}

private final class LoaderJavaProcess: @unchecked Sendable {
    struct Result: Sendable { let status: Int32; let output: String }
    private let lock = NSLock()
    private var process: Process?
    private var cancelled = false
    private func start(_ process: Process) throws {
        lock.lock(); defer { lock.unlock() }
        if cancelled { throw CancellationError() }
        self.process = process
        try process.run()
    }
    private func cancel() {
        lock.lock(); cancelled = true; let task = process; lock.unlock()
        if let task, task.isRunning {
            task.terminate()
            DispatchQueue.global().asyncAfter(deadline: .now() + 2) { if task.isRunning { kill(task.processIdentifier, SIGKILL) } }
        }
    }
    static func run(executable: URL, arguments: [String], directory: URL, log: URL) async throws -> Result {
        let holder = LoaderJavaProcess()
        return try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { continuation in
                DispatchQueue.global(qos: .utility).async {
                    do {
                        _ = FileManager.default.createFile(atPath: log.path, contents: nil)
                        let output = try FileHandle(forWritingTo: log)
                        defer { try? output.close() }
                        let task = Process()
                        task.executableURL = executable; task.arguments = arguments; task.currentDirectoryURL = directory
                        task.standardInput = FileHandle.nullDevice; task.standardOutput = output; task.standardError = output
                        try holder.start(task)
                        let timeout = DispatchWorkItem { holder.cancel() }
                        DispatchQueue.global().asyncAfter(deadline: .now() + 1800, execute: timeout)
                        task.waitUntilExit(); timeout.cancel()
                        let reader = try FileHandle(forReadingFrom: log)
                        defer { try? reader.close() }
                        let length = try reader.seekToEnd()
                        try reader.seek(toOffset: length > 128_000 ? length - 128_000 : 0)
                        let data = try reader.readToEnd() ?? Data()
                        holder.lock.lock(); let cancelled = holder.cancelled; holder.process = nil; holder.lock.unlock()
                        if cancelled { throw CancellationError() }
                        continuation.resume(returning: Result(status: task.terminationStatus, output: String(decoding: data, as: UTF8.self)))
                    } catch { continuation.resume(throwing: error) }
                }
            }
        } onCancel: { holder.cancel() }
    }
}

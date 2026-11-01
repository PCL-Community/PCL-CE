import Foundation
import Darwin

public struct LaunchPlan: Sendable {
    public let executable: URL
    public let arguments: [String]
    public let workingDirectory: URL
}

enum MinecraftArguments {
    static func offlineUUID(for name: String) -> String {
        OfflineLogin.uuid(for: name)
    }

    static func tokenize(_ input: String) throws -> [String] {
        var arguments: [String] = [], token = ""
        var quote: Character?, escaped = false, started = false
        for character in input {
            if escaped { token.append(character); escaped = false; started = true; continue }
            if character == "\\", quote != "'" { escaped = true; started = true; continue }
            if let active = quote {
                if character == active { quote = nil } else { token.append(character) }
                continue
            }
            if character == "\"" || character == "'" { quote = character; started = true; continue }
            if character.isWhitespace {
                if started { arguments.append(token); token = ""; started = false }
            } else { token.append(character); started = true }
        }
        guard quote == nil, !escaped else { throw LauncherError.invalidArguments("引号或转义字符没有闭合") }
        if started { arguments.append(token) }
        return arguments
    }

    static func resolve(_ values: [Any], variables: [String: String], features: [String: Bool]) throws -> [String] {
        var resolved: [String] = []
        for value in values {
            let strings: [String]
            if let text = value as? String { strings = [text] }
            else if let argument = value as? [String: Any] {
                if let rules = argument["rules"] as? [[String: Any]], !InstallPlatform.rulesAllowed(rules, features: features) { continue }
                if let text = argument["value"] as? String { strings = [text] }
                else { strings = argument["value"] as? [String] ?? [] }
            } else { continue }
            for string in strings {
                var replaced = string
                for (key, value) in variables { replaced = replaced.replacingOccurrences(of: "${\(key)}", with: value) }
                guard !replaced.contains("${") else { throw LauncherError.invalidArguments("无法解析 \(string)") }
                resolved.append(replaced)
            }
        }
        return resolved
    }

    static func build(metadata: [String: Any], configuration: LaunchConfiguration, classpath: [URL], nativesDirectory: URL) throws -> [String] {
        let originalVersion = metadata["pclOriginalVersion"] as? String ?? metadata["id"] as? String ?? configuration.versionID
        let playerName = try OfflineLogin.validateName(configuration.playerName, versionID: originalVersion)
        let indexID = (metadata["assetIndex"] as? [String: Any])?["id"] as? String ?? metadata["assets"] as? String ?? "legacy"
        try MinecraftVersionMetadata.validateVersionID(indexID)
        let assetsDirectory = try MinecraftVersionMetadata.safeFile(relativePath: "assets", under: configuration.gameDirectory)
        var gameAssetsDirectory = assetsDirectory
        let indexFile = try MinecraftVersionMetadata.safeFile(relativePath: "indexes/\(indexID).json", under: assetsDirectory)
        if let data = try? Data(contentsOf: indexFile), let index = try? JSONSerialization.jsonObject(with: data) as? [String: Any], index["virtual"] as? Bool == true {
            gameAssetsDirectory = try MinecraftVersionMetadata.safeFile(relativePath: "virtual/\(indexID)", under: assetsDirectory)
        }
        let uuid = OfflineLogin.uuid(for: playerName, skinModel: configuration.offlineSkinModel)
        let variables = [
            "auth_player_name": playerName,
            "version_name": configuration.versionID,
            "game_directory": (configuration.runtimeGameDirectory ?? configuration.gameDirectory).path,
            "assets_root": assetsDirectory.path,
            "game_assets": gameAssetsDirectory.path,
            "assets_index_name": indexID,
            "auth_uuid": uuid,
            "auth_access_token": uuid,
            "access_token": uuid,
            "auth_session": uuid,
            "user_type": "msa",
            "user_properties": "{}",
            "version_type": configuration.customInfo.isEmpty ? (metadata["type"] as? String ?? "release") : configuration.customInfo,
            "natives_directory": nativesDirectory.path,
            "launcher_name": "PCL-SwiftUI-Community",
            "launcher_version": "0.1.0",
            "classpath": classpath.map(\.path).joined(separator: ":"),
            "classpath_separator": ":",
            "library_directory": configuration.gameDirectory.appendingPathComponent("libraries").path,
            "primary_jar": configuration.gameDirectory.appendingPathComponent("versions/\(metadata["jar"] as? String ?? configuration.versionID)/\(metadata["jar"] as? String ?? configuration.versionID).jar").path,
            "resolution_width": String(configuration.width),
            "resolution_height": String(configuration.height),
            "auth_xuid": "0",
            "clientid": ""
        ]
        let features = ["has_custom_resolution": true, "is_demo_user": false, "has_quick_plays_support": false]
        let modern = metadata["arguments"] as? [String: Any]
        var jvm: [String]
        if let arguments = modern?["jvm"] as? [Any] {
            jvm = try resolve(arguments, variables: variables, features: features)
        } else {
            jvm = ["-Djava.library.path=\(nativesDirectory.path)", "-cp", variables["classpath"]!]
        }
        // NeoForge excludes the client JAR from its module discovery. An inherited
        // profile uses its parent's JAR even when version_name is a custom name.
        let clientJarName = (metadata["jar"] as? String ?? configuration.versionID) + ".jar"
        jvm = jvm.map { argument in
            guard argument.hasPrefix("-DignoreList=") else { return argument }
            let entries = argument.dropFirst("-DignoreList=".count).split(separator: ",").map(String.init)
            return entries.contains(clientJarName) ? argument : argument + "," + clientJarName
        }
        if !jvm.contains("-XstartOnFirstThread") { jvm.insert("-XstartOnFirstThread", at: 0) }
        jvm.insert(contentsOf: ["-Xms256M", "-Xmx\(configuration.memoryMB)M", "-Dfile.encoding=UTF-8", "-Dlog4j2.formatMsgNoLookups=true"], at: 0)
        var extraJVM = try tokenize(configuration.extraJVMArguments)
        if configuration.garbageCollector != "global" {
            guard ["g1", "g1tuned", "zgc", "generational-zgc", "none"].contains(configuration.garbageCollector) else { throw LauncherError.invalidArguments("未知的内存管理方式") }
            let isCollector: (String) -> Bool = { $0.range(of: #"^-XX:[+-]?(Use\w+GC|ZGenerational|UseCompactObjectHeaders|G1\w+Percent|G1\w+Size|(Max|Min)(GCPauseMillis|HeapFreeRatio))"#, options: .regularExpression) != nil }
            jvm.removeAll(where: isCollector)
            if configuration.garbageCollector != "none" {
                extraJVM.removeAll(where: isCollector)
                let major = configuration.javaMajorVersion ?? MinecraftVersionMetadata.requiredJava(metadata)
                let useZGC = configuration.garbageCollector == "zgc" ? major >= 15 : configuration.garbageCollector == "generational-zgc" && major >= 21
                jvm.append(useZGC ? "-XX:+UseZGC" : "-XX:+UseG1GC")
                if useZGC && (21...22).contains(major) { jvm.append("-XX:+ZGenerational") }
                if !useZGC {
                    if !jvm.contains("-XX:+UnlockExperimentalVMOptions") && !extraJVM.contains("-XX:+UnlockExperimentalVMOptions") { jvm.insert("-XX:+UnlockExperimentalVMOptions", at: 0) }
                    jvm += ["-XX:G1NewSizePercent=20", "-XX:G1ReservePercent=20", "-XX:G1HeapRegionSize=32M", "-XX:MaxGCPauseMillis=50"]
                    if configuration.garbageCollector == "g1tuned" {
                        if major == 8 { jvm.append("-XX:+ParallelRefProcEnabled") }
                        if major >= 12 { jvm += ["-XX:MinHeapFreeRatio=25", "-XX:MaxHeapFreeRatio=40"] }
                    }
                }
            }
        }
        jvm += extraJVM
        if let compatibility = try LegacyOpenGLCompatibility.javaAgentArgument(
            metadata: metadata, javaMajorVersion: configuration.javaMajorVersion ?? MinecraftVersionMetadata.requiredJava(metadata),
            gameDirectory: configuration.gameDirectory), !jvm.contains(compatibility) {
            jvm.append(compatibility)
        }
        if let logging = metadata["logging"] as? [String: Any], let client = logging["client"] as? [String: Any],
           let file = client["file"] as? [String: Any], let id = file["id"] as? String, let argument = client["argument"] as? String {
            let logFile = try MinecraftVersionMetadata.safeFile(relativePath: id, under: assetsDirectory.appendingPathComponent("log_configs"))
            guard FileManager.default.fileExists(atPath: logFile.path) else { throw LauncherError.missingFile("assets/log_configs/\(id)") }
            jvm.append(argument.replacingOccurrences(of: "${path}", with: logFile.path))
        }
        guard let mainClass = metadata["mainClass"] as? String, !mainClass.isEmpty else { throw LauncherError.invalidVersion("缺少 mainClass") }
        var game: [String]
        if let arguments = modern?["game"] as? [Any] { game = try resolve(arguments, variables: variables, features: features) }
        else if let legacy = metadata["minecraftArguments"] as? String {
            // Split before substitution so paths with spaces remain a single Java argument.
            game = try resolve(tokenize(legacy), variables: variables, features: features)
        } else { throw LauncherError.unsupportedVersion("缺少 Minecraft 启动参数") }
        game += try resolve(tokenize(configuration.extraGameArguments), variables: variables, features: features)
        // ModLaunch.vb: LiteLoader/Forge must use OptiFine's cooperative
        // tweaker last, otherwise both tweakers supply --version twice.
        let libraries = (metadata["libraries"] as? [[String: Any]] ?? []).compactMap { $0["name"] as? String }
        let cooperativeLoader = libraries.contains { $0.hasPrefix("com.mumfrey:liteloader:") }
            || MinecraftVersionMetadata.modLoader(in: metadata)?.kind == .forge
        if cooperativeLoader && (game.contains("optifine.OptiFineTweaker") || game.contains("optifine.OptiFineForgeTweaker")) {
            var normalized: [String] = [], index = 0
            while index < game.count {
                if game[index] == "--tweakClass", index + 1 < game.count,
                   ["optifine.OptiFineTweaker", "optifine.OptiFineForgeTweaker"].contains(game[index + 1]) { index += 2 }
                else { normalized.append(game[index]); index += 1 }
            }
            game = normalized + ["--tweakClass", "optifine.OptiFineForgeTweaker"]
        }
        if !configuration.serverAddress.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            let server = try MinecraftServerAddress(configuration.serverAddress)
            var supportsQuickPlay = false
            if let released = metadata["releaseTime"] as? String {
                let formatter = ISO8601DateFormatter()
                let date = formatter.date(from: released)
                formatter.formatOptions.insert(.withFractionalSeconds)
                if let date = date ?? formatter.date(from: released) { supportsQuickPlay = date > Date(timeIntervalSince1970: 1_680_566_400) }
            } else {
                let pieces = originalVersion.split(separator: ".").compactMap { Int($0) }
                supportsQuickPlay = pieces.count >= 2 && (pieces[0] > 1 || pieces[1] >= 20)
            }
            if supportsQuickPlay { game += ["--quickPlayMultiplayer", server.address] }
            else { game += ["--server", server.host, "--port", String(server.port)] }
        }
        guard !(jvm + game + [configuration.customInfo]).contains(where: { $0.contains("\0") }) else { throw LauncherError.invalidArguments("参数中不能包含空字符") }
        return jvm + [mainClass] + game
    }
}

public final class MinecraftLauncher: @unchecked Sendable {
    private let lock = NSLock()
    private var process: Process?
    private var preparing = false

    public init() {}

    public var isRunning: Bool {
        lock.lock(); defer { lock.unlock() }
        return preparing || process?.isRunning == true
    }

    public static func makeLaunchPlan(configuration: LaunchConfiguration) throws -> LaunchPlan {
        var configuration = configuration
        // Reject an empty/quoted name before touching the filesystem. The base-version
        // length limit is checked again after reading metadata for custom profile names.
        configuration.playerName = try OfflineLogin.validateName(configuration.playerName)
        guard configuration.memoryMB >= 256, configuration.width >= 320, configuration.height >= 240 else { throw LauncherError.invalidArguments("内存或窗口大小超出允许范围") }
        guard !configuration.gameDirectory.path.contains(":") else { throw LauncherError.invalidArguments("游戏目录不能包含冒号（Java 类路径分隔符）。") }
        let metadata = try MinecraftVersionMetadata.resolve(versionID: configuration.versionID, gameDirectory: configuration.gameDirectory)
        let originalVersion = metadata["pclOriginalVersion"] as? String ?? metadata["id"] as? String ?? configuration.versionID
        configuration.playerName = try OfflineLogin.validateName(configuration.playerName, versionID: originalVersion)
        let runtimeDirectory: URL
        if let requested = configuration.runtimeGameDirectory {
            let root = configuration.gameDirectory.standardizedFileURL
            if requested.standardizedFileURL == root { runtimeDirectory = root }
            else {
                let rootPath = root.path + "/"
                guard requested.standardizedFileURL.path.hasPrefix(rootPath) else { throw LauncherError.invalidArguments("独立游戏目录必须位于当前 Minecraft 文件夹内。") }
                runtimeDirectory = try MinecraftVersionMetadata.safeFile(relativePath: String(requested.standardizedFileURL.path.dropFirst(rootPath.count)), under: root)
            }
        } else { runtimeDirectory = configuration.gameDirectory }
        try FileManager.default.createDirectory(at: runtimeDirectory, withIntermediateDirectories: true)
        configuration.runtimeGameDirectory = runtimeDirectory
        guard let java = JavaDiscovery.inspect(executable: configuration.javaExecutable) else { throw LauncherError.invalidJava("请选择可执行的 Java 8 或更新版本") }
        configuration.javaMajorVersion = java.majorVersion
        let required = MinecraftVersionMetadata.requiredJava(metadata)
        guard java.majorVersion >= required else { throw LauncherError.invalidJava("此版本需要 Java \(required)，当前选择的是 Java \(java.majorVersion)") }
        let libraries = metadata["libraries"] as? [[String: Any]] ?? []
        let hasArchitectureSpecificNatives = libraries.contains {
            let name = $0["name"] as? String ?? ""
            return name.contains(":natives-macos-arm64") || name.contains(":natives-osx-arm64")
        }
        if hasArchitectureSpecificNatives, java.architecture != InstallPlatform.architecture {
            throw LauncherError.invalidJava("此版本已按 \(InstallPlatform.architecture) 下载原生库，请选择相同架构的 Java；当前为 \(java.architecture)。")
        }
        let nativesPath = "versions/\(configuration.versionID)/natives-macos-\(java.architecture)"
        // This directory is rebuilt below. Even a link to another directory inside
        // the game root must not turn native cleanup into deletion of user files.
        var nativeComponent = configuration.gameDirectory
        for component in nativesPath.split(separator: "/") {
            nativeComponent.appendPathComponent(String(component))
            if (try? FileManager.default.destinationOfSymbolicLink(atPath: nativeComponent.path)) != nil {
                throw LauncherError.invalidVersion("原生库生成目录不能使用符号链接")
            }
        }
        let natives = try MinecraftVersionMetadata.safeFile(relativePath: nativesPath, under: configuration.gameDirectory)
        try FileManager.default.createDirectory(at: natives, withIntermediateDirectories: true)
        var classpath: [URL] = []
        var nativeArchives: [URL] = []
        let libraryRoot = configuration.gameDirectory.appendingPathComponent("libraries")
        for library in libraries where InstallPlatform.libraryAllowed(library) {
            let downloads = library["downloads"] as? [String: Any]
            if downloads == nil || downloads?["artifact"] != nil, let path = MinecraftVersionMetadata.libraryPath(library) {
                let file = try MinecraftVersionMetadata.safeFile(relativePath: path, under: libraryRoot)
                guard FileManager.default.fileExists(atPath: file.path) else { throw LauncherError.missingFile(path) }
                classpath.append(file)
                let classifier = (library["name"] as? String)?.split(separator: ":").dropFirst(3).first
                if classifier?.hasPrefix("natives-") == true { nativeArchives.append(file) }
            }
            if let classifier = InstallPlatform.nativeClassifier(for: library),
               let classifiers = downloads?["classifiers"] as? [String: Any], let artifact = classifiers[classifier] as? [String: Any], let path = artifact["path"] as? String {
                let file = try MinecraftVersionMetadata.safeFile(relativePath: path, under: libraryRoot)
                guard FileManager.default.fileExists(atPath: file.path) else { throw LauncherError.missingFile(path) }
                nativeArchives.append(file)
            }
        }
        // Rebuild only this generated directory; never allow stale dylibs to shadow a new version.
        for item in try FileManager.default.contentsOfDirectory(at: natives, includingPropertiesForKeys: nil) {
            if ["dylib", "jnilib", "so"].contains(item.pathExtension) { try FileManager.default.removeItem(at: item) }
        }
        for archive in nativeArchives { try extractNatives(from: archive, into: natives) }
        for native in try FileManager.default.contentsOfDirectory(at: natives, includingPropertiesForKeys: nil) where ["dylib", "jnilib"].contains(native.pathExtension) {
            let architectures = try NativeLibraryArchitecture.architectures(in: native)
            guard architectures.contains(java.architecture) else {
                throw LauncherError.invalidJava("原生库 \(native.lastPathComponent) 与 \(java.architecture) Java 不兼容。旧版 Minecraft 的 Intel 原生库需要 Intel Java 和 Rosetta；新版请使用与本机一致的 Java。")
            }
        }
        let jarID = metadata["jar"] as? String ?? configuration.versionID
        try MinecraftVersionMetadata.validateVersionID(jarID)
        let jar = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(jarID)/\(jarID).jar", under: configuration.gameDirectory)
        guard FileManager.default.fileExists(atPath: jar.path) else { throw LauncherError.missingFile(jar.lastPathComponent) }
        classpath.append(jar)
        let arguments = try MinecraftArguments.build(metadata: metadata, configuration: configuration, classpath: classpath, nativesDirectory: natives)
        return LaunchPlan(executable: configuration.javaExecutable, arguments: arguments, workingDirectory: runtimeDirectory)
    }

    private static func extractNatives(from archive: URL, into destination: URL) throws {
        let listing = try NativeProcess.capture(URL(fileURLWithPath: "/usr/bin/unzip"), ["-Z1", archive.path], timeout: 20)
        guard listing.status == 0 else { throw LauncherError.extractionFailed(archive.lastPathComponent) }
        for entry in listing.text.split(separator: "\n").map(String.init) {
            let name = (entry as NSString).lastPathComponent
            let ext = (name as NSString).pathExtension
            guard ["dylib", "jnilib", "so"].contains(ext), !entry.hasPrefix("META-INF/"), !name.isEmpty else { continue }
            // Read each member as bytes and flatten its name: archive paths and symlinks cannot escape.
            let extracted = try NativeProcess.capture(URL(fileURLWithPath: "/usr/bin/unzip"), ["-p", archive.path, entry], timeout: 20)
            guard extracted.status == 0 else { throw LauncherError.extractionFailed(name) }
            let target = destination.appendingPathComponent(name)
            try extracted.output.write(to: target, options: .atomic)
        }
    }

    public func launch(configuration: LaunchConfiguration, onOutput: @escaping @Sendable (String) -> Void, onExit: @escaping @Sendable (Int32) -> Void) throws {
        lock.lock()
        if preparing || process?.isRunning == true { lock.unlock(); throw LauncherError.alreadyRunning }
        preparing = true
        lock.unlock()
        defer { lock.lock(); preparing = false; lock.unlock() }
        onOutput("正在检查版本、Java 与原生库……")
        let plan = try Self.makeLaunchPlan(configuration: configuration)
        let child = Process()
        let output = Pipe()
        child.executableURL = plan.executable
        child.arguments = plan.arguments
        child.currentDirectoryURL = plan.workingDirectory
        child.standardInput = FileHandle.nullDevice
        child.standardOutput = output
        child.standardError = output
        let buffer = GameOutputBuffer(callback: onOutput)
        output.fileHandleForReading.readabilityHandler = { handle in
            let data = handle.availableData
            if !data.isEmpty { buffer.append(data) }
        }
        child.terminationHandler = { [weak self] process in
            output.fileHandleForReading.readabilityHandler = nil
            let remaining = output.fileHandleForReading.readDataToEndOfFile()
            if !remaining.isEmpty { buffer.append(remaining) }
            buffer.finish()
            if let self {
                self.lock.lock()
                if self.process === process { self.process = nil }
                self.lock.unlock()
            }
            onExit(process.terminationStatus)
        }
        lock.lock(); process = child; lock.unlock()
        do { try child.run() }
        catch {
            output.fileHandleForReading.readabilityHandler = nil
            lock.lock(); process = nil; lock.unlock()
            throw error
        }
        onOutput("已启动 Minecraft \(configuration.versionID)（PID \(child.processIdentifier)）。")
    }

    public func stop() {
        lock.lock(); let running = process; lock.unlock()
        if running?.isRunning == true { running?.terminate() }
    }
}

private final class GameOutputBuffer: @unchecked Sendable {
    private var pending = Data()
    private let lock = NSLock()
    private let callback: @Sendable (String) -> Void
    init(callback: @escaping @Sendable (String) -> Void) { self.callback = callback }

    func append(_ data: Data) {
        lock.lock()
        pending.append(data)
        var lines: [String] = []
        while let newline = pending.firstIndex(of: 10) {
            lines.append(String(decoding: pending[..<newline], as: UTF8.self))
            pending.removeSubrange(...newline)
        }
        if pending.count > 64 * 1024 { lines.append(String(decoding: pending, as: UTF8.self)); pending.removeAll(keepingCapacity: true) }
        lock.unlock()
        lines.forEach(callback)
    }

    func finish() {
        lock.lock(); let tail = pending; pending.removeAll(); lock.unlock()
        if !tail.isEmpty { callback(String(decoding: tail, as: UTF8.self)) }
    }
}

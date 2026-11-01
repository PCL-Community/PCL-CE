import Foundation
import XCTest
import CryptoKit
import Darwin
@testable import PCLCore

/// Explicitly opted-in validation of the user's Utopia archive. The ordinary
/// suite does not download mods, change preferences, or open Minecraft.
final class UtopiaSmokeTests: XCTestCase {
    func testUtopiaImportAndOfflineRenderer() async throws {
        let environment = ProcessInfo.processInfo.environment
        guard let archivePath = environment["PCL_UTOPIA_ARCHIVE"], !archivePath.isEmpty,
              let gamePath = environment["PCL_UTOPIA_GAME_DIR"], !gamePath.isEmpty else {
            throw XCTSkip("Set PCL_UTOPIA_ARCHIVE and PCL_UTOPIA_GAME_DIR to opt into the real Utopia import/render test.")
        }
        let archive = URL(fileURLWithPath: archivePath)
        let gameDirectory = URL(fileURLWithPath: gamePath, isDirectory: true)
        let id = environment["PCL_UTOPIA_PROFILE"] ?? "乌托邦探险之旅3.5.2"
        try MinecraftInstaller.validateProfileID(id)
        let fm = FileManager.default
        try fm.createDirectory(at: gameDirectory, withIntermediateDirectories: true)
        let profile = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(id)", under: gameDirectory)
        let jsonFile = profile.appendingPathComponent("\(id).json")
        let existingGames = try Self.runningGamePIDs(profile: profile)
        if !existingGames.isEmpty {
            throw XCTSkip("A user-owned Minecraft process is already using this profile (PID \(existingGames.map(String.init).joined(separator: ","))). Observe its existing logs; do not rebuild natives or launch another game.")
        }
        let readyFile = try MinecraftVersionMetadata.safeFile(relativePath: "PCL/utopia-renderer-stable.json", under: profile)
        if fm.fileExists(atPath: readyFile.path) { try fm.removeItem(at: readyFile) }
        let rendererOnly = environment["PCL_UTOPIA_RENDER_ONLY"] == "1"
        let prepareOnly = environment["PCL_UTOPIA_PREPARE_ONLY"] == "1"
        let directNetwork = environment["PCL_UTOPIA_DIRECT"] == "1"
        let customSession: URLSession?
        if directNetwork {
            let configuration = URLSessionConfiguration.ephemeral
            configuration.connectionProxyDictionary = [:]
            customSession = URLSession(configuration: configuration)
        } else { customSession = nil }
        defer { customSession?.invalidateAndCancel() }
        // An explicit empty configured key also prevents a developer-shell key
        // from accidentally making this no-key integration test pass.
        let importer = ModpackImporter(session: customSession ?? .shared, curseForgeAPIKey: "")
        let descriptor = try await importer.inspect(archive: archive)
        try Self.require(descriptor.format == .curseForge && descriptor.minecraftVersion == "1.20.1" &&
                         descriptor.loaderKind == .fabric && descriptor.loaderVersion == "0.18.4",
                         "This targeted smoke test expects Utopia 3.5.2 (Minecraft 1.20.1 / Fabric 0.18.4)")
        try Self.require(!descriptor.needsAPIKey, "Inspection must not require the user to supply a CurseForge API key")
        Self.marker("INSPECT", "name=\(descriptor.name) format=\(descriptor.format.rawValue) minecraft=\(descriptor.minecraftVersion) loader=\(descriptor.loaderKind?.rawValue ?? "none")-\(descriptor.loaderVersion ?? "none") directNetwork=\(directNetwork)")
        let java = try XCTUnwrap(JavaDiscovery.discover().first { $0.majorVersion == 17 && $0.architecture == InstallPlatform.architecture },
                                 "Utopia requires a Java 17 runtime matching the current application architecture")
        let wasCommitted = fm.fileExists(atPath: jsonFile.path)
        if !wasCommitted {
            guard !rendererOnly else { throw LauncherError.missingFile(jsonFile.path) }
            Self.marker("IMPORT_BEGIN", "profile=\(id) root=\(gameDirectory.path) java=\(java.executable.path)")
            let progressFile: URL
            if let path = environment["PCL_UTOPIA_PROGRESS_FILE"] ?? environment["UTOPIA_PROGRESS_FILE"], !path.isEmpty {
                progressFile = URL(fileURLWithPath: path)
            } else {
                progressFile = try MinecraftVersionMetadata.safeFile(relativePath: "PCL/utopia-smoke-import-progress.log", under: gameDirectory)
            }
            let progress = try UtopiaImportProgress(log: progressFile)
            Self.marker("IMPORT_PROGRESS_FILE", progressFile.path)
            // No API key is passed or injected by this harness. Resolve/download
            // behavior is exactly the production importer's default behavior.
            try await importer.importPack(archive: archive, gameDirectory: gameDirectory, profileID: id,
                javaExecutable: java.executable, progress: { progress.report($0) })
            Self.marker("IMPORT_COMMITTED", profile.path)
        } else {
            Self.marker("IMPORT_REUSED_PROFILE", "existing committed profile retained: \(profile.path)")
        }
        try Task.checkCancellation()

        let zip = try ModpackZIPArchive(url: archive)
        let manifestEntry = try XCTUnwrap(zip.member("manifest.json"))
        let manifest = try XCTUnwrap(JSONSerialization.jsonObject(with: zip.data(for: manifestEntry)) as? [String: Any])
        let manifestFiles = try XCTUnwrap(manifest["files"] as? [[String: Any]])
        let requiredCount = manifestFiles.filter { $0["required"] as? Bool != false }.count
        let bundledMods = zip.entries.filter { !$0.isDirectory && $0.path.hasPrefix("overrides/mods/") && $0.path.lowercased().hasSuffix(".jar") }
        try Self.require(bundledMods.count == 52, "The supplied Utopia 3.5.2 archive must contain 52 bundled mod JARs")
        for entry in bundledMods {
            try Task.checkCancellation()
            let relative = String(entry.path.dropFirst("overrides/".count))
            let destination = try MinecraftVersionMetadata.safeFile(relativePath: relative, under: profile)
            let expected = try zip.data(for: entry, maximumBytes: entry.size)
            let actual = try Data(contentsOf: destination, options: [.mappedIfSafe])
            try Self.require(SHA256.hash(data: actual) == SHA256.hash(data: expected), "Bundled mod must be retained byte-for-byte: \(relative)")
        }
        let reportFile = try MinecraftVersionMetadata.safeFile(relativePath: "PCL/import-report.json", under: profile)
        let report = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: reportFile)) as? [String: Any])
        let resolved = try XCTUnwrap((report["resolvedFiles"] as? NSNumber)?.intValue)
        let reused = try XCTUnwrap((report["reusedFiles"] as? NSNumber)?.intValue)
        let downloaded = try XCTUnwrap((report["downloadedFiles"] as? NSNumber)?.intValue)
        try Self.require(report["format"] as? String == "curseForge" && report["sourceArchive"] as? String == archive.lastPathComponent,
                         "The committed import report must identify this exact archive")
        try Self.require((report["bundledModJars"] as? NSNumber)?.intValue == bundledMods.count, "Import report bundled mod count differs from the ZIP")
        try Self.require(resolved == requiredCount, "Every required manifest reference must be resolved")
        try Self.require(reused >= 0 && downloaded >= 0 && reused + downloaded == resolved,
                         "Every resolved file must be reused or downloaded")
        Self.marker("MODS_VERIFIED", "bundled=\(bundledMods.count) requiredReferences=\(requiredCount) resolved=\(resolved) reused=\(reused) downloaded=\(downloaded)")

        let local = try XCTUnwrap(LocalVersionScanner.scan(gameDirectory: gameDirectory).first { $0.id == id })
        let settings = try VersionSettingsRepository.load(version: local)
        try Self.require(settings.isolateGameDirectory, "The imported pack must use an isolated game directory")
        try Self.require(!settings.useGlobalLaunchSettings && settings.memoryMB == 8192,
                         "The archive's 8 GB memory recommendation must be persisted as an independent profile setting")
        let metadata = try MinecraftVersionMetadata.resolve(versionID: id, gameDirectory: gameDirectory)
        try Self.require(metadata["pclOriginalVersion"] as? String == descriptor.minecraftVersion &&
                         metadata["pclLoaderKind"] as? String == "fabric" && metadata["pclLoaderVersion"] as? String == descriptor.loaderVersion,
                         "The committed version metadata must preserve the requested Minecraft and Fabric versions")
        let assetID = try XCTUnwrap((metadata["assetIndex"] as? [String: Any])?["id"] as? String)
        try MinecraftVersionMetadata.validateVersionID(assetID)
        let indexFile = try MinecraftVersionMetadata.safeFile(relativePath: "assets/indexes/\(assetID).json", under: gameDirectory)
        let index = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: indexFile)) as? [String: Any])
        let objects = try XCTUnwrap(index["objects"] as? [String: [String: Any]])
        try Self.require(!objects.isEmpty, "The asset index must contain game objects")
        for (name, info) in objects {
            try Task.checkCancellation()
            let hash = try XCTUnwrap(info["hash"] as? String)
            let size = try XCTUnwrap((info["size"] as? NSNumber)?.int64Value)
            try Self.require(MinecraftInstaller.validSHA1(hash), "Malformed asset hash: \(name)")
            let file = try MinecraftVersionMetadata.safeFile(relativePath: "assets/objects/\(hash.prefix(2))/\(hash)", under: gameDirectory)
            let attributes = try fm.attributesOfItem(atPath: file.path)
            try Self.require((attributes[.size] as? NSNumber)?.int64Value == size, "Missing/truncated asset: \(name)")
        }
        let configuration = LaunchConfiguration(gameDirectory: gameDirectory, versionID: id, playerName: "PCLUtopiaSmoke",
            javaExecutable: java.executable, memoryMB: 8192, width: 1100, height: 700, runtimeGameDirectory: profile)
        let lateGames = try Self.runningGamePIDs(profile: profile)
        if !lateGames.isEmpty {
            throw XCTSkip("The user started this profile during validation (PID \(lateGames.map(String.init).joined(separator: ","))); native files and the running game are retained.")
        }
        let plan = try MinecraftLauncher.makeLaunchPlan(configuration: configuration)
        try Self.require(plan.arguments.contains("net.fabricmc.loader.impl.launch.knot.KnotClient") && plan.arguments.contains("-Xmx8192M"),
                         "The launch plan must run the Fabric client with 8 GB of memory")
        try Self.require(!plan.arguments.contains { $0.contains("${") }, "Launch arguments must not contain unresolved placeholders")
        let gameIndex = try XCTUnwrap(plan.arguments.firstIndex(of: "--gameDir"))
        try Self.require(plan.arguments[gameIndex + 1] == profile.path && plan.workingDirectory.path == profile.path,
                         "Both --gameDir and the process working directory must be isolated")
        Self.marker("LAUNCH_PLAN_VERIFIED", "java=17 arch=\(java.architecture) memory=8192 isolated=true assetObjects=\(objects.count) profile=\(profile.path)")
        if prepareOnly {
            Self.marker("PREPARE_ONLY_COMPLETE", "Import and file validation completed; game was not launched")
            return
        }

        let log = try MinecraftVersionMetadata.safeFile(relativePath: "PCL/utopia-smoke-game.log", under: profile)
        let output = try UtopiaGameOutput(log: log)
        let launcher = MinecraftLauncher()
        defer { launcher.stop() }
        Self.marker("LAUNCH_BEGIN", "log=\(log.path)")
        try launcher.launch(configuration: configuration, onOutput: { output.append($0) }, onExit: { output.exited($0) })
        let limit = min(1200, max(180, Double(environment["PCL_UTOPIA_RENDER_TIMEOUT"] ?? "600") ?? 600))
        let stability = min(60, max(10, Double(environment["PCL_UTOPIA_STABLE_SECONDS"] ?? "30") ?? 30))
        let deadline = Date().addingTimeInterval(limit)
        var rendererSince: Date?, lastHeartbeat = Date.distantPast
        while Date() < deadline, output.exitStatus == nil {
            try Task.checkCancellation()
            if output.rendererInitialized {
                if rendererSince == nil {
                    rendererSince = Date()
                    Self.marker("RENDERER_INITIALIZED", "atlasCount=\(output.atlasCount); observing \(Int(stability)) seconds of continued process health")
                }
                if Date().timeIntervalSince(rendererSince!) >= stability { break }
            } else if rendererSince != nil {
                rendererSince = nil
                Self.marker("RENDERER_RELOADING", "Resource reload detected; restarting the stability interval")
            }
            if Date().timeIntervalSince(lastHeartbeat) >= 20 {
                Self.marker("LAUNCH_WAIT", "atlasCount=\(output.atlasCount) backend=\(output.backendSeen) elapsed=\(Int(limit - deadline.timeIntervalSinceNow))s")
                lastHeartbeat = Date()
            }
            try await Task.sleep(nanoseconds: 500_000_000)
        }
        let stable = rendererSince.map { Date().timeIntervalSince($0) >= stability } == true && output.rendererInitialized
        let exitedEarly = output.exitStatus
        let tail = output.tail.joined(separator: "\n")
        launcher.stop()
        for _ in 0..<120 where output.exitStatus == nil { try await Task.sleep(nanoseconds: 250_000_000) }
        XCTAssertTrue(stable, "Utopia did not reach a stable initialized renderer within \(Int(limit))s. See \(log.path)\n\(tail)")
        XCTAssertNil(exitedEarly, "Utopia exited before renderer stability; see \(log.path)\n\(tail)")
        XCTAssertNotNil(output.exitStatus, "The test's own Minecraft process did not stop after SIGTERM")
        XCTAssertNil(output.writeFailure, "Game log write failed: \(output.writeFailure ?? "")")
        if stable, exitedEarly == nil, output.exitStatus != nil, output.writeFailure == nil {
            let result: [String: Any] = ["profile": id, "minecraft": descriptor.minecraftVersion,
                "loader": "Fabric \(descriptor.loaderVersion ?? "")", "bundledModsVerified": bundledMods.count,
                "resolvedFiles": resolved, "reusedFiles": reused, "downloadedFiles": downloaded,
                "atlasCount": output.atlasCount, "stableSeconds": stability,
                "exitStatusAfterRequestedStop": output.exitStatus!, "log": log.path,
                "downloadAuthorization": "automatic resolver; no user API key",
                "scope": "texture atlases initialized and process remained alive; world loading and interactive main-menu behavior not asserted"]
            try JSONSerialization.data(withJSONObject: result, options: [.prettyPrinted, .sortedKeys]).write(to: readyFile, options: .atomic)
            Self.marker("RENDERER_STABLE", "atlasCount=\(output.atlasCount) stopped=\(output.exitStatus!) log=\(log.path)")
        }
    }

    private static func marker(_ stage: String, _ message: String) {
        print("UTOPIA_\(stage) \(message)")
        fflush(stdout)
    }

    private static func require(_ condition: Bool, _ message: String) throws {
        guard condition else { throw NSError(domain: "PCLUtopiaSmoke", code: 1, userInfo: [NSLocalizedDescriptionKey: message]) }
    }

    private static func runningGamePIDs(profile: URL) throws -> [Int32] {
        let result = try NativeProcess.capture(URL(fileURLWithPath: "/bin/ps"), ["-ww", "-axo", "pid=,command="], timeout: 5)
        guard result.status == 0 else { throw NSError(domain: "PCLUtopiaSmoke", code: 2, userInfo: [NSLocalizedDescriptionKey: "Could not check whether this profile already has a running game"] ) }
        var matching: [Int32] = []
        for line in result.text.split(separator: "\n") where line.contains("--gameDir " + profile.path) {
            let fields = line.split(maxSplits: 1, whereSeparator: \.isWhitespace)
            guard let first = fields.first, let pid = Int32(first) else { continue }
            let command = try NativeProcess.capture(URL(fileURLWithPath: "/bin/ps"), ["-p", String(pid), "-o", "comm="], timeout: 3)
            if command.status == 0 && command.text.trimmingCharacters(in: .whitespacesAndNewlines).hasSuffix("/bin/java") { matching.append(pid) }
        }
        // Only PIDs are returned or printed; process arguments can contain tokens.
        return matching.sorted()
    }
}

private final class UtopiaImportProgress: @unchecked Sendable {
    private let lock = NSLock()
    private let file: FileHandle
    private var bucket = -1
    private var stageStates = ""
    private var previous = Date.distantPast
    init(log: URL) throws {
        try FileManager.default.createDirectory(at: log.deletingLastPathComponent(), withIntermediateDirectories: true)
        _ = FileManager.default.createFile(atPath: log.path, contents: nil)
        file = try FileHandle(forWritingTo: log)
    }
    deinit { try? file.close() }
    func report(_ value: InstallProgress) {
        lock.lock(); defer { lock.unlock() }
        let next = Int(value.fraction * 50)
        let states = value.snapshot?.stages.map { $0.id + "=" + $0.state.rawValue }.joined(separator: ",") ?? ""
        guard next != bucket || states != stageStates || Date().timeIntervalSince(previous) >= 10 else { return }
        bucket = next; previous = Date(); stageStates = states
        let statistics = value.snapshot.map { " revision=\($0.revision) bytes=\($0.downloadedBytes) pending=\($0.remainingFiles) [\(states)]" } ?? ""
        let line = "UTOPIA_IMPORT \(Int(value.fraction * 100))% \(value.message)\(statistics)"
        try? file.write(contentsOf: Data((line + "\n").utf8))
        print(line)
        fflush(stdout)
    }
}

private final class UtopiaGameOutput: @unchecked Sendable {
    private let lock = NSLock()
    private let file: FileHandle
    private var atlases = Set<String>()
    private var backend = false
    private var status: Int32?
    private var recent: [String] = []
    private var failure: String?
    init(log: URL) throws {
        try FileManager.default.createDirectory(at: log.deletingLastPathComponent(), withIntermediateDirectories: true)
        _ = FileManager.default.createFile(atPath: log.path, contents: nil)
        file = try FileHandle(forWritingTo: log)
    }
    deinit { try? file.close() }
    var atlasCount: Int { lock.lock(); defer { lock.unlock() }; return atlases.count }
    var backendSeen: Bool { lock.lock(); defer { lock.unlock() }; return backend }
    var rendererInitialized: Bool { lock.lock(); defer { lock.unlock() }; return backend && atlases.count >= 3 }
    var exitStatus: Int32? { lock.lock(); defer { lock.unlock() }; return status }
    var tail: [String] { lock.lock(); defer { lock.unlock() }; return recent }
    var writeFailure: String? { lock.lock(); defer { lock.unlock() }; return failure }
    func append(_ line: String) {
        lock.lock(); defer { lock.unlock() }
        if line.contains("Reloading ResourceManager:") { atlases.removeAll() }
        if line.contains("Created:"), let range = line.range(of: "textures/atlas/") {
            atlases.insert(String(line[range.lowerBound...].split(whereSeparator: \.isWhitespace).first ?? ""))
        }
        if line.localizedCaseInsensitiveContains("Backend library: LWJGL") || line.contains("LWJGL Version:") || line.contains("OpenGL Vendor:") || line.contains("OpenGL Version:") { backend = true }
        recent.append(line)
        if recent.count > 60 { recent.removeFirst(recent.count - 60) }
        do { try file.write(contentsOf: Data((line + "\n").utf8)) }
        catch { failure = error.localizedDescription }
        if line.contains("Created:") && line.contains("textures/atlas/") ||
            line.contains("Incompatible mods found") || line.contains("FormattedException") || line.contains("FATAL") || line.hasPrefix("PROCESS_EXIT=") {
            print("UTOPIA_GAME \(line)")
            fflush(stdout)
        }
    }
    func exited(_ value: Int32) {
        lock.lock(); status = value; lock.unlock()
        append("PROCESS_EXIT=\(value)")
    }
}

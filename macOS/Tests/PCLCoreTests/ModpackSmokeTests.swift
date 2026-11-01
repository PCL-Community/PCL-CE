import Foundation
import XCTest
import CryptoKit
import Darwin
import Security
@testable import PCLCore

/// Opt-in network/render check; the ordinary suite does not download or launch modpacks.
/// Set PCL_REAL_MODPACK_ARCHIVE and PCL_REAL_MODPACK_GAME_DIR to isolated test paths.
/// Optional PCL_REAL_MODPACK_SEED_DIR reuses another isolated installation's verified cache.
final class ModpackSmokeTests: XCTestCase {
    func testRealModrinthImportAndRender() async throws {
        let environment = ProcessInfo.processInfo.environment
        guard let archivePath = environment["PCL_REAL_MODPACK_ARCHIVE"],
              let gamePath = environment["PCL_REAL_MODPACK_GAME_DIR"], !archivePath.isEmpty, !gamePath.isEmpty else {
            throw XCTSkip("Set PCL_REAL_MODPACK_ARCHIVE and PCL_REAL_MODPACK_GAME_DIR to opt into real modpack import/render.")
        }
        let archive = URL(fileURLWithPath: archivePath)
        let gameDirectory = URL(fileURLWithPath: gamePath, isDirectory: true)
        let id = environment["PCL_REAL_MODPACK_PROFILE"] ?? "SO-Modrinth-SMOKE"
        let fm = FileManager.default
        try fm.createDirectory(at: gameDirectory, withIntermediateDirectories: true)
        if let seed = environment["PCL_REAL_MODPACK_SEED_DIR"] {
            for directory in ["assets", "libraries", "versions/1.20.4"] {
                try Self.seedCache(URL(fileURLWithPath: seed).appendingPathComponent(directory), to: gameDirectory.appendingPathComponent(directory))
            }
        }
        let network = URLSessionConfiguration.ephemeral
        if environment["PCL_REAL_MODPACK_DIRECT_NETWORK"] == "1" { network.connectionProxyDictionary = [:] }
        if environment["PCL_REAL_MODPACK_TLS12"] == "1" { network.tlsMaximumSupportedProtocolVersion = .TLSv12 }
        let session = URLSession(configuration: network)
        defer { session.invalidateAndCancel() }
        let importer = ModpackImporter(session: session)
        let descriptor = try await importer.inspect(archive: archive)
        print("MODPACK_INSPECT name=\(descriptor.name) format=\(descriptor.format.rawValue) minecraft=\(descriptor.minecraftVersion) loader=\(descriptor.loaderKind?.rawValue ?? "none")-\(descriptor.loaderVersion ?? "none")")
        let runtime = try XCTUnwrap(JavaDiscovery.discover().first { $0.majorVersion == 17 && $0.architecture == InstallPlatform.architecture })
        let profile = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(id)", under: gameDirectory)
        let previouslyImported = fm.fileExists(atPath: profile.appendingPathComponent("\(id).json").path)
        if !previouslyImported {
            let report = ModpackSmokeProgress()
            let attempts = max(1, min(8, Int(environment["PCL_REAL_MODPACK_ATTEMPTS"] ?? "1") ?? 1))
            for attempt in 1...attempts {
                do {
                    try await importer.importPack(archive: archive, gameDirectory: gameDirectory, profileID: id, javaExecutable: runtime.executable, progress: { report.report($0) })
                    break
                } catch {
                    let failure = error as NSError
                    guard attempt < attempts, failure.domain == NSURLErrorDomain,
                          [NSURLErrorSecureConnectionFailed, NSURLErrorNetworkConnectionLost, NSURLErrorTimedOut].contains(failure.code) else { throw error }
                    print("MODPACK_NETWORK_RETRY attempt=\(attempt) code=\(failure.code)")
                    fflush(stdout)
                }
            }
        }
        let zip = try ModpackZIPArchive(url: archive)
        let index = try XCTUnwrap(zip.member("modrinth.index.json"))
        let manifest = try XCTUnwrap(JSONSerialization.jsonObject(with: zip.data(for: index)) as? [String: Any])
        let files = try XCTUnwrap(manifest["files"] as? [[String: Any]])
        let expectedClient = files.filter { ($0["env"] as? [String: String])?["client"] != "unsupported" }
        for file in expectedClient {
            let path = try XCTUnwrap(file["path"] as? String)
            let data = try Data(contentsOf: profile.appendingPathComponent(path), options: [.mappedIfSafe])
            let hashes = try XCTUnwrap(file["hashes"] as? [String: String])
            XCTAssertEqual(Insecure.SHA1.hash(data: data).map { String(format: "%02x", $0) }.joined(), hashes["sha1"])
            XCTAssertEqual(SHA512.hash(data: data).map { String(format: "%02x", $0) }.joined(), hashes["sha512"])
        }
        for entry in zip.entries where !entry.isDirectory && entry.path.hasPrefix("overrides/") {
            let path = String(entry.path.dropFirst("overrides/".count))
            if previouslyImported {
                // A previous render may legitimately save options or migrate mod config.
                XCTAssertTrue(fm.fileExists(atPath: profile.appendingPathComponent(path).path), path)
            } else {
                XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent(path)), try zip.data(for: entry), path)
            }
        }
        let modCount = try fm.contentsOfDirectory(atPath: profile.appendingPathComponent("mods").path).filter { $0.hasSuffix(".jar") }.count
        XCTAssertEqual(modCount, expectedClient.count)
        let local = try XCTUnwrap(LocalVersionScanner.scan(gameDirectory: gameDirectory).first { $0.id == id })
        let settings = try VersionSettingsRepository.load(version: local)
        XCTAssertTrue(settings.isolateGameDirectory)
        let metadata = try MinecraftVersionMetadata.load(versionID: id, gameDirectory: gameDirectory)
        XCTAssertNil(metadata["inheritsFrom"])
        XCTAssertEqual(metadata["jar"] as? String, id)
        XCTAssertEqual(metadata["pclLoaderKind"] as? String, "fabric")
        XCTAssertEqual(metadata["pclOriginalVersion"] as? String, descriptor.minecraftVersion)
        let configuration = LaunchConfiguration(gameDirectory: gameDirectory, versionID: id, playerName: "PCLModpackSmoke", javaExecutable: runtime.executable, memoryMB: 2048, runtimeGameDirectory: profile)
        let plan = try MinecraftLauncher.makeLaunchPlan(configuration: configuration)
        XCTAssertTrue(plan.arguments.contains("net.fabricmc.loader.impl.launch.knot.KnotClient"))
        XCTAssertFalse(plan.arguments.contains { $0.contains("${") })
        let gameIndex = try XCTUnwrap(plan.arguments.firstIndex(of: "--gameDir"))
        XCTAssertEqual(plan.arguments[gameIndex + 1], profile.path)
        print("MODPACK_VERIFIED mods=\(modCount) isolated=true root=\(gameDirectory.path) profile=\(id)")
        fflush(stdout)
        if environment["PCL_REAL_MODPACK_PREPARE_ONLY"] == "1" { return }
        let output = ModpackSmokeOutput(log: gameDirectory.appendingPathComponent("modpack-smoke.log"))
        let launcher = MinecraftLauncher()
        defer { launcher.stop() }
        try launcher.launch(configuration: configuration, onOutput: { output.append($0) }, onExit: { output.exit($0) })
        let deadline = Date().addingTimeInterval(120)
        while Date() < deadline, !output.rendered, output.exitStatus == nil { try await Task.sleep(nanoseconds: 500_000_000) }
        XCTAssertTrue(output.rendered, "Modpack never initialized texture atlases; see \(gameDirectory.path)/modpack-smoke.log")
        XCTAssertNil(output.exitStatus, "Modpack exited before texture atlas initialization")
        if output.rendered {
            try Data("\(descriptor.name): \(modCount) verified mods, Fabric \(descriptor.loaderVersion ?? ""), Minecraft \(descriptor.minecraftVersion), texture atlases initialized.\n".utf8).write(to: gameDirectory.appendingPathComponent("modpack-rendering-ready"), options: .atomic)
            print("MODPACK_RENDERING_READY \(gameDirectory.path)")
            fflush(stdout)
        }
        launcher.stop()
        for _ in 0..<40 where output.exitStatus == nil { try await Task.sleep(nanoseconds: 250_000_000) }
        XCTAssertNotNil(output.exitStatus, "Modpack process did not stop after test")
    }

    private static func seedCache(_ source: URL, to destination: URL) throws {
        let fm = FileManager.default
        guard fm.fileExists(atPath: source.path), (try? fm.destinationOfSymbolicLink(atPath: source.path)) == nil else { return }
        guard let resolvedPath = realpath(source.path, nil) else { throw CocoaError(.fileReadUnknown) }
        let resolved = URL(fileURLWithPath: String(cString: resolvedPath))
        free(resolvedPath)
        guard let enumerator = fm.enumerator(at: resolved, includingPropertiesForKeys: [.isRegularFileKey, .isSymbolicLinkKey]) else { return }
        for case let file as URL in enumerator {
            let values = try file.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey])
            if values.isSymbolicLink == true { enumerator.skipDescendants(); continue }
            guard values.isRegularFile == true else { continue }
            let relative = String(file.path.dropFirst(resolved.path.count + 1))
            let target = try MinecraftInstaller.safeDestination(root: destination, relative: relative)
            if fm.fileExists(atPath: target.path) { continue }
            try fm.createDirectory(at: target.deletingLastPathComponent(), withIntermediateDirectories: true)
            try ModpackImporter.cloneOrCopy(file, to: target)
        }
    }
}
private final class ModpackSmokeProgress: @unchecked Sendable {
    private let lock = NSLock()
    private var bucket = -1
    func report(_ progress: InstallProgress) {
        lock.lock(); defer { lock.unlock() }
        let next = Int(progress.fraction * 20)
        if next != bucket {
            bucket = next
            print("MODPACK_IMPORT \(Int(progress.fraction * 100))% \(progress.message)")
            fflush(stdout)
        }
    }
}
private final class ModpackSmokeOutput: @unchecked Sendable {
    private let lock = NSLock()
    private var didRender = false
    private var status: Int32?
    private var lines: [String] = []
    private let log: URL
    init(log: URL) { self.log = log }
    var rendered: Bool { lock.lock(); defer { lock.unlock() }; return didRender }
    var exitStatus: Int32? { lock.lock(); defer { lock.unlock() }; return status }
    func append(_ line: String) {
        lock.lock(); defer { lock.unlock() }
        lines.append(line)
        if line.contains("Created:") && line.contains("textures/atlas/") { didRender = true }
        try? Data(lines.joined(separator: "\n").utf8).write(to: log, options: .atomic)
        print("MODPACK_GAME \(line)")
        fflush(stdout)
    }
    func exit(_ value: Int32) {
        lock.lock(); status = value; lock.unlock()
        append("PROCESS_EXIT=\(value)")
    }
}

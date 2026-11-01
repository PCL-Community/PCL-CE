import Foundation
import XCTest
@testable import PCLCore

/// Opt in with a separate, prepared Minecraft directory. This test does not install
/// packages, change preferences, or modify the user's normal Minecraft directory.
final class ModdedSmokeTests: XCTestCase {
    func testInstalledModLoadersInitializeRenderer() async throws {
        let environment = ProcessInfo.processInfo.environment
        guard let path = environment["PCL_MODDED_SMOKE_GAME_DIR"], !path.isEmpty else {
            throw XCTSkip("Set PCL_MODDED_SMOKE_GAME_DIR to a prepared, isolated directory to run real modded clients.")
        }
        let root = URL(fileURLWithPath: path, isDirectory: true)
        let ids = (environment["PCL_MODDED_SMOKE_VERSIONS"] ?? "Forge-SMOKE,NeoForge-SMOKE").split(separator: ",").map(String.init)
        let runtimes = JavaDiscovery.discover()
        for id in ids {
            try MinecraftVersionMetadata.validateVersionID(id)
            let metadata = try MinecraftVersionMetadata.resolve(versionID: id, gameDirectory: root)
            let major = MinecraftVersionMetadata.requiredJava(metadata)
            let java = try XCTUnwrap(runtimes.first { $0.majorVersion == major && $0.architecture == InstallPlatform.architecture })
            let log = try MinecraftVersionMetadata.safeFile(relativePath: "PCL/smoke-launch-\(id).log", under: root)
            let runtime = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(id)", under: root)
            let configuration = LaunchConfiguration(gameDirectory: root, versionID: id, playerName: "PCLSmoke", javaExecutable: java.executable, memoryMB: 2048, runtimeGameDirectory: runtime)
            let plan = try MinecraftLauncher.makeLaunchPlan(configuration: configuration)
            XCTAssertFalse(plan.arguments.contains { $0.contains("${") }, "All inherited loader arguments must be resolved")
            XCTAssertTrue(plan.arguments.contains("-XstartOnFirstThread"))
            print("MODDED_SMOKE_PREPARED \(id) java=\(java.majorVersion) arch=\(java.architecture) log=\(log.path)")
            fflush(stdout)
            let output = try ModdedSmokeOutput(log: log)
            let launcher = MinecraftLauncher()
            defer { launcher.stop() }
            try launcher.launch(configuration: configuration, onOutput: { output.append($0) }, onExit: { output.exited($0) })
            let deadline = Date().addingTimeInterval(180)
            while Date() < deadline && !output.rendered && output.exitStatus == nil {
                try await Task.sleep(nanoseconds: 250_000_000)
            }
            let rendered = output.rendered
            let exitedEarly = output.exitStatus
            launcher.stop()
            for _ in 0..<40 where output.exitStatus == nil { try await Task.sleep(nanoseconds: 250_000_000) }
            XCTAssertTrue(rendered, "\(id) did not create texture atlases; see \(log.path)")
            XCTAssertNil(exitedEarly, "\(id) exited before rendering; see \(log.path)")
            XCTAssertNotNil(output.exitStatus, "\(id) did not stop after the smoke test")
            if rendered { print("MODDED_SMOKE_RENDERING_READY \(id)") }
            fflush(stdout)
        }
    }
}

private final class ModdedSmokeOutput: @unchecked Sendable {
    private let lock = NSLock()
    private let file: FileHandle
    private var ready = false
    private var status: Int32?
    init(log: URL) throws {
        try FileManager.default.createDirectory(at: log.deletingLastPathComponent(), withIntermediateDirectories: true)
        _ = FileManager.default.createFile(atPath: log.path, contents: nil)
        file = try FileHandle(forWritingTo: log)
    }
    deinit { try? file.close() }
    var rendered: Bool { lock.lock(); defer { lock.unlock() }; return ready }
    var exitStatus: Int32? { lock.lock(); defer { lock.unlock() }; return status }
    func append(_ line: String) {
        lock.lock(); defer { lock.unlock() }
        if line.contains("Created:") && line.contains("textures/atlas/") { ready = true }
        try? file.write(contentsOf: Data((line + "\n").utf8))
        print("MODDED_GAME \(line)")
        fflush(stdout)
    }
    func exited(_ value: Int32) {
        lock.lock(); status = value; lock.unlock()
        append("PROCESS_EXIT=\(value)")
    }
}
